using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벌집(육각, offset "odd-r") 격자를 관리하는 매니저.
/// - (row, col) 격자 좌표 <-> 실제 월드 좌표 변환
/// - 어느 칸에 어떤 풍선이 있는지 관리 (Dictionary)
/// - 발사된 풍선이 닿았을 때 스냅(정렬)될 가장 가까운 빈 칸 탐색
/// - 같은 색 인접 풍선 탐색(BFS) 및 2개 이상이면 터뜨리는 매칭 로직
/// - 터짐 -> 밀려나기 -> 연쇄 매칭까지 전부 처리
/// 씬에 하나만 존재해야 하는 싱글턴으로 사용합니다.
/// </summary>
public class HexGridManager : MonoBehaviour
{
    public static HexGridManager Instance { get; private set; }

    [Header("격자 규격")]
    public int columns = 9;
    public float topRowY = 8.4f;
    public int maxRows = 14;

    [Header("매칭 설정")]
    public int minMatchCount = 3;

    [Header("터짐 이펙트")]
    public GameObject[] popEffectPrefabsByColor;
    public Canvas effectCanvas;

    [Header("게임오버 - 위험선")]
    public float dangerLineY = -6f;
    public System.Action OnDangerLineReached;

    public System.Action<int> OnBalloonsPopped;

    /// <summary>
    /// 매칭으로 풍선이 터질 때 "어떤 색 매칭이었는지"와 터진 개수를 함께 알려줍니다.
    /// (회색 매칭 시 전기 효과음처럼, 색에 따라 다른 연출을 붙일 때 사용)
    /// 점수 계산은 기존 OnBalloonsPopped를 그대로 사용합니다.
    /// </summary>
    public System.Action<BalloonColor, int> OnMatchPopped;

    public System.Action OnBalloonAttached;

    /// <summary>
    /// 이번 발사로 인한 배치+매칭+연쇄 반응이 "완전히 다" 끝났을 때 호출됩니다.
    /// (매칭이 없었으면 배치 직후 바로, 있었으면 모든 연쇄가 끝난 뒤에)
    /// BalloonSpawner가 이걸 기준으로 다음 발사를 허용합니다.
    /// </summary>
    public System.Action OnPlacementSettled;
    public System.Action<int> OnComboStep;

    [Header("연쇄 반응 연출")]
    public float pushSettleDelay = 0.2f;

    private int comboDepth = 0;

    public float CellWidth { get; private set; }
    public float CellHeight { get; private set; }

    private float leftEdge;
    private Camera mainCamera;

    private readonly Dictionary<Vector2Int, Balloon> grid = new Dictionary<Vector2Int, Balloon>();

    /// <summary>
    /// 격자 셀 크기 계산이 완료되어, 풍선 배치 등을 안전하게 할 수 있는 상태인지 여부.
    /// </summary>
    public bool IsReady { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        mainCamera = Camera.main;

        // 안드로이드 기기에 따라 앱 시작 직후 몇 프레임 동안 Screen.width/height가
        // 아직 정확한 값으로 안정되지 않은 경우가 있습니다(특히 화면 비율이 특이한 기기일수록
        // 더 잘 드러남). 이 상태에서 바로 셀 크기를 계산하면, 화면(레터박스)은 나중에 스스로
        // 보정되지만 격자 계산값은 잘못된 채로 세션 내내 고정되어버립니다.
        // 그래서 화면 크기가 몇 프레임 연속으로 변하지 않을 때까지 기다린 뒤에 계산합니다.
        StartCoroutine(WaitForStableScreenThenInit());
    }

    private IEnumerator WaitForStableScreenThenInit()
    {
        int lastWidth = -1;
        int lastHeight = -1;
        int stableFrames = 0;
        const int requiredStableFrames = 5;
        const int maxFramesToWait = 60; // 안전장치: 1초 정도(60fps 기준) 지나도 안정 안 되면 그냥 진행
        int waited = 0;

        while (stableFrames < requiredStableFrames && waited < maxFramesToWait)
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight)
            {
                stableFrames++;
            }
            else
            {
                stableFrames = 0;
                lastWidth = Screen.width;
                lastHeight = Screen.height;
            }

            waited++;
            yield return null;
        }

        CalculateCellSize();
        IsReady = true;
    }

    private void CalculateCellSize()
    {
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float totalWidth = halfWidth * 2f;

        CellWidth = totalWidth / columns;
        CellHeight = CellWidth * 0.8660254f;

        leftEdge = -halfWidth + CellWidth / 2f;
    }

    public Vector3 GridToWorld(int row, int col)
    {
        float rowOffset = (row % 2 != 0) ? CellWidth / 2f : 0f;
        float x = leftEdge + col * CellWidth + rowOffset;
        float y = topRowY - row * CellHeight;
        return new Vector3(x, y, 0f);
    }

    public Vector2Int WorldToNearestGrid(Vector3 worldPos)
    {
        int row = Mathf.RoundToInt((topRowY - worldPos.y) / CellHeight);
        row = Mathf.Max(row, 0);

        float rowOffset = (row % 2 != 0) ? CellWidth / 2f : 0f;
        int col = Mathf.RoundToInt((worldPos.x - leftEdge - rowOffset) / CellWidth);

        return new Vector2Int(row, col);
    }

    public List<Vector2Int> GetNeighborCoords(Vector2Int cell)
    {
        int row = cell.x;
        int col = cell.y;
        List<Vector2Int> neighbors = new List<Vector2Int>();

        if (row % 2 == 0)
        {
            neighbors.Add(new Vector2Int(row, col - 1));
            neighbors.Add(new Vector2Int(row, col + 1));
            neighbors.Add(new Vector2Int(row - 1, col - 1));
            neighbors.Add(new Vector2Int(row - 1, col));
            neighbors.Add(new Vector2Int(row + 1, col - 1));
            neighbors.Add(new Vector2Int(row + 1, col));
        }
        else
        {
            neighbors.Add(new Vector2Int(row, col - 1));
            neighbors.Add(new Vector2Int(row, col + 1));
            neighbors.Add(new Vector2Int(row - 1, col));
            neighbors.Add(new Vector2Int(row - 1, col + 1));
            neighbors.Add(new Vector2Int(row + 1, col));
            neighbors.Add(new Vector2Int(row + 1, col + 1));
        }

        return neighbors;
    }

    /// <summary>
    /// referenceCell(정확히 알고 있는 격자 좌표)의 6방향 이웃 중, 비어있으면서
    /// worldPos(지금 내 실제 위치)와 가장 가까운 칸을 찾습니다.
    /// 위치를 반올림해서 칸을 "추정"하는 WorldToNearestGrid보다 훨씬 정확합니다 -
    /// 이웃 후보가 정확히 6개로 정해져 있어서, 부동소수점 오차로 엉뚱한 칸이 선택될 여지가 없습니다.
    /// 이웃이 전부 막혀있으면(이론상 드묾) 기존 방식(FindNearestEmptyCell)으로 대체합니다.
    /// </summary>
    public Vector2Int FindNearestEmptyNeighborOf(Vector2Int referenceCell, Vector3 worldPos)
    {
        List<Vector2Int> neighbors = GetNeighborCoords(referenceCell);

        Vector2Int best = default;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (Vector2Int n in neighbors)
        {
            if (n.x < 0 || n.x >= maxRows) continue;

            Vector2Int clamped = ClampColumn(n);
            if (IsOccupied(clamped)) continue;

            float dist = Vector3.Distance(worldPos, GridToWorld(clamped.x, clamped.y));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = clamped;
                found = true;
            }
        }

        if (!found)
        {
            return FindNearestEmptyCell(worldPos);
        }

        return best;
    }

    public bool IsOccupied(Vector2Int cell) => grid.ContainsKey(cell);

    /// <summary>
    /// 특정 행(row)에 실제로 몇 개의 칸이 들어갈 수 있는지 반환합니다.
    /// 홀수 행은 반 칸만큼 밀려서 배치되므로 짝수 행보다 한 칸 적게 허용합니다.
    /// </summary>
    public int GetColumnCountForRow(int row) => (row % 2 != 0) ? columns - 1 : columns;

    public bool IsValidColumn(Vector2Int cell) => cell.y >= 0 && cell.y < GetColumnCountForRow(cell.x);

    public Vector2Int FindNearestEmptyCell(Vector3 worldPos)
    {
        Vector2Int approx = WorldToNearestGrid(worldPos);

        if (!IsOccupied(approx) && IsValidColumn(approx))
            return ClampColumn(approx);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(approx);
        visited.Add(approx);

        Vector2Int bestCell = approx;
        float bestDist = float.MaxValue;
        int safetyCounter = 0;

        while (queue.Count > 0 && safetyCounter < 200)
        {
            safetyCounter++;
            Vector2Int current = queue.Dequeue();
            Vector2Int clamped = ClampColumn(current);

            if (!IsOccupied(clamped))
            {
                float dist = Vector3.Distance(worldPos, GridToWorld(clamped.x, clamped.y));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = clamped;
                }
                continue;
            }

            foreach (Vector2Int n in GetNeighborCoords(current))
            {
                if (n.x < 0 || n.x >= maxRows) continue;
                if (visited.Contains(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        return bestCell;
    }

    private Vector2Int ClampColumn(Vector2Int cell)
    {
        int row = Mathf.Max(cell.x, 0);
        int maxCol = GetColumnCountForRow(row) - 1;
        int clampedCol = Mathf.Clamp(cell.y, 0, maxCol);
        return new Vector2Int(row, clampedCol);
    }

    public void PlaceBalloon(Balloon balloon, Vector2Int cell)
    {
        cell = ClampColumn(cell);

        grid[cell] = balloon;
        balloon.transform.position = GridToWorld(cell.x, cell.y);
        balloon.Stop();
        balloon.gridCell = cell;

        OnBalloonAttached?.Invoke();

        comboDepth = 0;

        List<Vector2Int> poppedCells = CheckAndPopMatches(cell);

        if (grid.TryGetValue(cell, out Balloon stillThere) && stillThere == balloon)
        {
            if (balloon.transform.position.y <= dangerLineY)
            {
                OnDangerLineReached?.Invoke();
            }
        }

        // 이번 배치로 매칭/연쇄가 시작됐으면, 그게 "완전히 다 끝날 때까지" 기다렸다가
        // OnPlacementSettled를 알려줍니다. 매칭이 아예 없었으면 즉시 알려줍니다.
        // BalloonSpawner는 이 신호를 받아야만 다음 발사를 허용해서, 연쇄가 진행 중인 상태에서
        // 새 풍선이 끼어들어 격자 데이터가 꼬이는(겹침/오매칭/밀림 후 복귀 등) 문제를 막습니다.
        if (poppedCells.Count > 0)
        {
            StartCoroutine(RunCascadeThenSettle(poppedCells));
        }
        else
        {
            OnPlacementSettled?.Invoke();
        }
    }

    /// <summary>
    /// 연쇄 반응(ProcessPopCascadeRoutine)이 재귀적으로 전부 끝날 때까지 기다린 뒤
    /// OnPlacementSettled를 발생시킵니다.
    /// </summary>
    private IEnumerator RunCascadeThenSettle(List<Vector2Int> poppedCells)
    {
        yield return ProcessPopCascadeRoutine(poppedCells);
        OnPlacementSettled?.Invoke();
    }

    private List<Vector2Int> CheckAndPopMatches(Vector2Int startCell)
    {
        List<Vector2Int> poppedCells = new List<Vector2Int>();

        if (!grid.TryGetValue(startCell, out Balloon startBalloon)) return poppedCells;

        BalloonColor targetColor = startBalloon.color;
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<Vector2Int> group = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            group.Add(current);

            foreach (Vector2Int neighbor in GetNeighborCoords(current))
            {
                if (visited.Contains(neighbor)) continue;
                if (!grid.TryGetValue(neighbor, out Balloon neighborBalloon)) continue;
                if (neighborBalloon.color != targetColor) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        if (group.Count >= minMatchCount)
        {
            // 색깔에 따라 실제로 터질 칸 목록이 달라집니다.
            // - Rainbow: 매칭된 그룹 + 그 주변에 붙어있는 모든 풍선(색깔 무관)까지 같이 터짐
            // - Gray: 매칭이 시작된 줄(row) 전체가 색깔 무관하게 다 터짐
            // - 그 외: 기존처럼 매칭된 그룹만 터짐
            List<Vector2Int> cellsToPop;

            if (targetColor == BalloonColor.Rainbow)
            {
                cellsToPop = ExpandWithAdjacentBalloons(group);
            }
            else if (targetColor == BalloonColor.Gray)
            {
                // 매칭된 그룹은 여러 행(row)에 걸쳐 있을 수 있으므로(벌집 격자 특성상
                // 화면에 세로로 붙어 보여도 내부적으로는 다른 행일 수 있음), 그룹의
                // "평균 행"을 중심으로 잡아 그 행 전체를 지웁니다. 다만 다른 행에 있는
                // 매칭된 회색 풍선까지 놓치지 않도록, 원래 매칭된 그룹도 반드시 같이 포함시킵니다.
                int centerRow = GetAverageRow(group);
                HashSet<Vector2Int> combined = new HashSet<Vector2Int>(group);
                foreach (Vector2Int cell in GetEntireRow(centerRow))
                {
                    combined.Add(cell);
                }
                cellsToPop = new List<Vector2Int>(combined);
            }
            else
            {
                cellsToPop = group;
            }

            foreach (Vector2Int cell in cellsToPop)
            {
                if (grid.TryGetValue(cell, out Balloon b))
                {
                    grid.Remove(cell);
                    SpawnPopEffect(b.transform.position, b.color);
                    Destroy(b.gameObject);
                    poppedCells.Add(cell);
                }
            }

            comboDepth++;
            OnBalloonsPopped?.Invoke(poppedCells.Count);
            OnMatchPopped?.Invoke(targetColor, poppedCells.Count);
            OnComboStep?.Invoke(comboDepth);
        }

        return poppedCells;
    }

    /// <summary>
    /// 레인보우 매칭 전용: 매칭된 칸들에 더해, 그 주변에 붙어있는 모든 풍선(색깔 무관)까지
    /// 전부 포함한 목록을 반환합니다. "밀어내는 대신 터뜨리는" 폭발 효과를 만듭니다.
    /// </summary>
    private List<Vector2Int> ExpandWithAdjacentBalloons(List<Vector2Int> baseCells)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>(baseCells);

        foreach (Vector2Int cell in baseCells)
        {
            foreach (Vector2Int neighbor in GetNeighborCoords(cell))
            {
                if (grid.ContainsKey(neighbor))
                {
                    result.Add(neighbor);
                }
            }
        }

        return new List<Vector2Int>(result);
    }

    /// <summary>
    /// 회색 매칭 전용: 지정한 행(row)에 실제로 풍선이 있는 칸들을 전부 반환합니다.
    /// (매칭이 시작된 줄을 기준으로 가로 한 줄 전체를 터뜨리는 효과에 사용)
    /// </summary>
    /// <summary>
    /// 매칭된 셀들의 "평균 행(row)"을 계산합니다. 회색 풍선 매칭 그룹이 여러 행에 걸쳐
    /// 있을 때, 어느 행을 "가운데"로 볼지 정하는 기준으로 사용합니다.
    /// </summary>
    private int GetAverageRow(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return 0;

        float sum = 0f;
        foreach (Vector2Int cell in cells)
        {
            sum += cell.x;
        }

        return Mathf.RoundToInt(sum / cells.Count);
    }

    private List<Vector2Int> GetEntireRow(int row)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        int colCount = GetColumnCountForRow(row);

        for (int col = 0; col < colCount; col++)
        {
            Vector2Int cell = new Vector2Int(row, col);
            if (grid.ContainsKey(cell))
            {
                result.Add(cell);
            }
        }

        return result;
    }

    /// <summary>
    /// 회색 스킬 전용: 맨 위 keepTopRows줄만 남기고, 그 아래 화면의 모든 풍선을 터뜨립니다.
    /// 터진 개수를 반환합니다. (점수 이벤트는 발생시키지 않으므로, 호출한 쪽에서 점수를 처리하세요)
    ///
    /// 격자 데이터에서는 "즉시" 전부 제거하고, 화면에서 터지는 연출만 윗줄부터 rowDelay 간격으로
    /// 차례로 보여줍니다. 연출이 끝나기 전에 다음 풍선을 쏘더라도, 사라질 풍선에 붙지 않도록
    /// 상태를 미리 Popped로 바꿔둡니다.
    ///
    /// useSingleEffect가 true면 풍선 색과 상관없이 전부 effectColor(기본: 회색 = 일렉트릭)의
    /// 이펙트 프리팹 하나로만 터집니다. 같은 프리팹/머티리얼만 쓰게 되어 렌더링이 가벼워집니다.
    ///
    /// overrideEffectPrefab을 넘기면 위 설정과 상관없이 모든 풍선이 그 프리팹으로 터집니다
    /// (스킬 전용 이펙트를 일반 회색 매칭 이펙트와 따로 쓰고 싶을 때).
    /// </summary>
    public int PopAllBelowTopRows(int keepTopRows = 1, float rowDelay = 0.04f,
                                  bool useSingleEffect = true, BalloonColor effectColor = BalloonColor.Gray,
                                  GameObject overrideEffectPrefab = null)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, Balloon> pair in grid)
        {
            if (pair.Key.x >= keepTopRows) cells.Add(pair.Key);
        }

        if (cells.Count == 0) return 0;

        // 윗줄부터, 같은 줄에서는 왼쪽부터
        cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        List<Balloon> targets = new List<Balloon>();
        List<int> targetRows = new List<int>();

        foreach (Vector2Int cell in cells)
        {
            Balloon b = grid[cell];
            grid.Remove(cell);

            if (b == null) continue;

            b.state = Balloon.BalloonState.Popped;
            targets.Add(b);
            targetRows.Add(cell.x);
        }

        StartCoroutine(PopWaveRoutine(targets, targetRows, rowDelay, useSingleEffect, effectColor, overrideEffectPrefab));
        return targets.Count;
    }

    private IEnumerator PopWaveRoutine(List<Balloon> targets, List<int> targetRows, float rowDelay,
                                       bool useSingleEffect, BalloonColor effectColor,
                                       GameObject overrideEffectPrefab)
    {
        int currentRow = targetRows.Count > 0 ? targetRows[0] : 0;

        for (int i = 0; i < targets.Count; i++)
        {
            if (rowDelay > 0f && targetRows[i] != currentRow)
            {
                currentRow = targetRows[i];
                yield return new WaitForSeconds(rowDelay);
            }

            Balloon b = targets[i];
            if (b == null) continue;

            if (overrideEffectPrefab != null)
                SpawnEffect(overrideEffectPrefab, b.transform.position);
            else
                SpawnPopEffect(b.transform.position, useSingleEffect ? effectColor : b.color);

            Destroy(b.gameObject);
        }
    }

    private IEnumerator ProcessPopCascadeRoutine(List<Vector2Int> poppedCells)
    {
        List<Vector2Int> movedFarCells = PerformPush(poppedCells);

        if (movedFarCells.Count == 0) yield break;

        yield return new WaitForSeconds(pushSettleDelay);

        List<Vector2Int> newlyPoppedCells = new List<Vector2Int>();

        foreach (Vector2Int farCell in movedFarCells)
        {
            if (!grid.ContainsKey(farCell)) continue;

            List<Vector2Int> popped = CheckAndPopMatches(farCell);
            if (popped.Count > 0)
            {
                newlyPoppedCells.AddRange(popped);
            }
        }

        if (newlyPoppedCells.Count > 0)
        {
            yield return ProcessPopCascadeRoutine(newlyPoppedCells);
        }
    }

    /// <summary>
    /// 방금 터진 칸들 각각에 대해, 바로 옆에 있던 풍선을 터진 방향의 반대쪽으로 한 칸 밀어냅니다.
    /// 이동할 수 없으면 제자리에서 반동만 재생합니다.
    ///
    /// 중요: 이 메서드는 poppedCells 목록을 순서대로 처리하는데, 처리 도중 어떤 poppedCell이
    /// (다른 poppedCell의 밀어내기로) 이미 다시 채워졌을 수 있습니다. 그런 칸은 더 이상
    /// "터진 빈 자리"가 아니므로, 그 자리를 기준으로 또 밀어내기를 시도하면 안 됩니다.
    /// 이 검사가 빠져있으면, 방금 자리 잡은 풍선이 같은 처리 안에서 다시 밀려나버려서
    /// (1) 매칭이 성립해야 할 자리에서 풍선이 사라져 매칭이 안 되거나,
    /// (2) 화면 끝까지 슬라이드했다가 다시 이동하는 것처럼 보이는 등의 원인이 됩니다.
    /// </summary>
    private List<Vector2Int> PerformPush(List<Vector2Int> poppedCells)
    {
        List<Vector2Int> movedFarCells = new List<Vector2Int>();

        foreach (Vector2Int poppedCell in poppedCells)
        {
            // 이 칸이 이미 다른 처리로 다시 채워졌다면, 더는 "터진 빈 자리"가 아니므로 건너뜀
            if (grid.ContainsKey(poppedCell)) continue;

            Vector3 poppedWorldPos = GridToWorld(poppedCell.x, poppedCell.y);

            foreach (Vector2Int neighborCell in GetNeighborCoords(poppedCell))
            {
                if (!grid.TryGetValue(neighborCell, out Balloon neighborBalloon)) continue;

                Vector2Int farCell = GetOppositeCell(poppedCell, neighborCell);

                bool outOfBounds = farCell.x < 0 || farCell.x >= maxRows
                    || farCell.y < 0 || farCell.y >= GetColumnCountForRow(farCell.x);
                bool blockedByBalloon = !outOfBounds && grid.ContainsKey(farCell);

                Vector3 neighborWorldPos = GridToWorld(neighborCell.x, neighborCell.y);
                Vector3 pushDir = (neighborWorldPos - poppedWorldPos).normalized;

                if (outOfBounds || blockedByBalloon)
                {
                    neighborBalloon.PlayRecoilBump(pushDir);
                    continue;
                }

                grid.Remove(neighborCell);
                grid[farCell] = neighborBalloon;
                neighborBalloon.gridCell = farCell;

                Vector3 targetPos = GridToWorld(farCell.x, farCell.y);
                neighborBalloon.StartSlide(targetPos);

                movedFarCells.Add(farCell);
            }
        }

        return movedFarCells;
    }

    private void SpawnPopEffect(Vector3 worldPosition, BalloonColor color)
    {
        if (popEffectPrefabsByColor == null) return;

        int idx = (int)color;
        if (idx < 0 || idx >= popEffectPrefabsByColor.Length) return;

        SpawnEffect(popEffectPrefabsByColor[idx], worldPosition);
    }

    /// <summary>
    /// 임의의 이펙트 프리팹을 월드 좌표 위치에 생성합니다. UI(RectTransform) 프리팹이면
    /// effectCanvas 아래에, 일반 프리팹이면 월드에 그대로 생성합니다.
    /// </summary>
    public void SpawnEffect(GameObject prefab, Vector3 worldPosition)
    {
        if (prefab == null) return;

        RectTransform prefabRect = prefab.GetComponent<RectTransform>();

        if (prefabRect != null && effectCanvas != null)
        {
            GameObject effect = Instantiate(prefab, effectCanvas.transform);
            RectTransform effectRect = effect.GetComponent<RectTransform>();

            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);

            Camera uiCamera = effectCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectCanvas.transform as RectTransform, screenPoint, uiCamera, out Vector2 localPoint);

            effectRect.anchoredPosition = localPoint;
        }
        else
        {
            Instantiate(prefab, worldPosition, Quaternion.identity);
        }
    }

    private Vector2Int GetOppositeCell(Vector2Int from, Vector2Int through)
    {
        Vector3Int fromCube = OffsetToCube(from);
        Vector3Int throughCube = OffsetToCube(through);
        Vector3Int direction = throughCube - fromCube;
        Vector3Int farCube = throughCube + direction;
        return CubeToOffset(farCube);
    }

    private Vector3Int OffsetToCube(Vector2Int cell)
    {
        int row = cell.x;
        int col = cell.y;
        int x = col - (row - (row & 1)) / 2;
        int z = row;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    private Vector2Int CubeToOffset(Vector3Int cube)
    {
        int col = cube.x + (cube.z - (cube.z & 1)) / 2;
        int row = cube.z;
        return new Vector2Int(row, col);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 dangerLeft = new Vector3(-10f, dangerLineY, 0f);
        Vector3 dangerRight = new Vector3(10f, dangerLineY, 0f);
        Gizmos.DrawLine(dangerLeft, dangerRight);

        Gizmos.color = Color.green;
        Vector3 topLeft = new Vector3(-10f, topRowY, 0f);
        Vector3 topRight = new Vector3(10f, topRowY, 0f);
        Gizmos.DrawLine(topLeft, topRight);
    }
}