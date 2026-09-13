using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벌집(육각, offset "odd-r") 격자를 관리하는 매니저.
/// - (row, col) 격자 좌표 <-> 실제 월드 좌표 변환
/// - 어느 칸에 어떤 풍선이 있는지 관리 (Dictionary)
/// - 발사된 풍선이 닿았을 때 스냅(정렬)될 가장 가까운 빈 칸 탐색
/// - 같은 색 인접 풍선 탐색(BFS) 및 2개 이상이면 터뜨리는 매칭 로직
/// 씬에 하나만 존재해야 하는 싱글턴으로 사용합니다.
/// </summary>
public class HexGridManager : MonoBehaviour
{
    public static HexGridManager Instance { get; private set; }

    [Header("격자 규격")]
    [Tooltip("한 줄에 배치할 풍선 개수")]
    public int columns = 9;

    [Tooltip("풍선이 배치될 최상단 줄의 y좌표 (Unity 유닛)")]
    public float topRowY = 8.4f;

    [Tooltip("격자 전체를 배치할 세로 줄 개수 (화면 위쪽 영역 확보용, 넉넉하게)")]
    public int maxRows = 14;

    [Header("매칭 설정")]
    [Tooltip("같은 색이 몇 개 이상 붙어야 터지는지")]
    public int minMatchCount = 3;

    [Header("터짐 이펙트")]
    [Tooltip("색깔별 터짐 파티클 프리팹. 순서는 BalloonColor enum 순서(Red, Blue, Green, Yellow, Purple)와 " +
             "정확히 일치해야 합니다. 각 프리팹의 Stop Action이 Destroy로 설정되어 있어야 자동으로 정리됩니다.")]
    public GameObject[] popEffectPrefabsByColor;

    [Tooltip("이펙트 프리팹이 UIParticle 같은 UI(RectTransform 기반) 파티클일 경우, 배치할 대상 Canvas. " +
             "일반 월드 파티클만 쓴다면 비워둬도 됩니다.")]
    public Canvas effectCanvas;

    // 계산된 셀 크기 (가로 폭 기준으로 자동 계산)
    public float CellWidth { get; private set; }
    public float CellHeight { get; private set; }

    private float leftEdge;   // 짝수 줄 기준 첫 칸 중심 x
    private Camera mainCamera;

    // 격자 좌표 -> 풍선 매핑
    private readonly Dictionary<Vector2Int, Balloon> grid = new Dictionary<Vector2Int, Balloon>();

    [Header("게임오버 - 위험선")]
    [Tooltip("풍선이 이 y좌표 이하로 배치되면 위험선에 도달한 것으로 간주합니다.")]
    public float dangerLineY = -6f;

    /// <summary>
    /// 풍선이 위험선에 도달했을 때 발생하는 이벤트. GameManager가 구독해서 게임오버 처리합니다.
    /// </summary>
    public System.Action OnDangerLineReached;

    /// <summary>
    /// 매칭으로 풍선이 터질 때마다 호출됩니다. 인자는 이번에 터진 풍선 개수.
    /// GameManager 등이 구독해서 점수/레벨업 계산에 사용합니다.
    /// </summary>
    public System.Action<int> OnBalloonsPopped;

    /// <summary>
    /// 발사된 풍선이 그리드에 붙을 때마다 호출됩니다 (매칭 성사 여부와 무관하게, 붙는 순간 항상 호출).
    /// AudioManager 등이 구독해서 "쿵" 소리 재생에 사용하면 됩니다.
    /// </summary>
    public System.Action OnBalloonAttached;

    /// <summary>
    /// 한 번의 발사(배치)로 시작된 연쇄 반응에서, 몇 번째 연쇄 단계인지 알려주는 이벤트.
    /// 1 = 첫 매칭(콤보 아님), 2 이상 = 연쇄로 이어진 콤보. ComboUI 등에서 구독해서 사용합니다.
    /// </summary>
    public System.Action<int> OnComboStep;

    // 이번 발사(배치) 사이클 동안의 연쇄 단계 카운터. PlaceBalloon 호출 시마다 리셋됩니다.
    private int comboDepth = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        mainCamera = Camera.main;
        CalculateCellSize();
    }

    private void CalculateCellSize()
    {
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float totalWidth = halfWidth * 2f;

        CellWidth = totalWidth / columns;
        CellHeight = CellWidth * 0.8660254f; // sqrt(3)/2 : 육각 격자 표준 세로 간격 비율

        leftEdge = -halfWidth + CellWidth / 2f;
    }

    /// <summary>
    /// 격자 좌표(row, col)를 실제 월드 좌표로 변환합니다.
    /// 홀수 행은 반 칸(CellWidth/2)만큼 오른쪽으로 밀려서 벌집 모양을 만듭니다.
    /// </summary>
    public Vector3 GridToWorld(int row, int col)
    {
        float rowOffset = (row % 2 != 0) ? CellWidth / 2f : 0f;
        float x = leftEdge + col * CellWidth + rowOffset;
        float y = topRowY - row * CellHeight;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// 월드 좌표에서 가장 가까운 격자 좌표(row, col)를 역산합니다. (근사값, 이후 보정 필요)
    /// </summary>
    public Vector2Int WorldToNearestGrid(Vector3 worldPos)
    {
        int row = Mathf.RoundToInt((topRowY - worldPos.y) / CellHeight);
        row = Mathf.Max(row, 0);

        float rowOffset = (row % 2 != 0) ? CellWidth / 2f : 0f;
        int col = Mathf.RoundToInt((worldPos.x - leftEdge - rowOffset) / CellWidth);

        return new Vector2Int(row, col);
    }

    /// <summary>
    /// 특정 육각 격자 좌표의 6방향 이웃 좌표를 반환합니다. (odd-r offset 기준)
    /// </summary>
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

    public bool IsOccupied(Vector2Int cell) => grid.ContainsKey(cell);

    /// <summary>
    /// 특정 행(row)에 실제로 몇 개의 칸이 들어갈 수 있는지 반환합니다.
    /// 홀수 행은 반 칸(CellWidth/2)만큼 오른쪽으로 밀려서 배치되므로, 짝수 행과 똑같은 개수를
    /// 허용하면 마지막 칸의 중심이 화면 가장자리 밖으로 나가버립니다. 그래서 홀수 행은 짝수 행보다
    /// 한 칸 적게 허용합니다.
    /// </summary>
    public int GetColumnCountForRow(int row) => (row % 2 != 0) ? columns - 1 : columns;

    public bool IsValidColumn(Vector2Int cell) => cell.y >= 0 && cell.y < GetColumnCountForRow(cell.x);

    /// <summary>
    /// 날아온 풍선이 부딪힌 지점(worldPos) 근처에서, 비어있는 가장 가까운 칸을 찾습니다.
    /// 부딪힌 지점 자체의 근사 칸부터 시작해서, 비어있지 않으면 이웃 칸들 중 가장 가까운 빈 칸을 탐색합니다.
    /// </summary>
    public Vector2Int FindNearestEmptyCell(Vector3 worldPos)
    {
        Vector2Int approx = WorldToNearestGrid(worldPos);

        if (!IsOccupied(approx) && IsValidColumn(approx))
            return ClampColumn(approx);

        // BFS로 가까운 빈 칸 탐색 (부딪힌 칸 기준 반경을 넓혀가며)
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
                continue; // 빈 칸을 찾았으니 이 경로는 더 확장할 필요 없음
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

    // Scene 뷰에서 위험선(빨강)과 풍선 최상단 배치 기준선(초록)을 표시 (에디터 확인용)
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

    /// <summary>
    /// 풍선을 특정 칸에 등록하고 위치를 그 칸의 정중앙으로 스냅시킵니다.
    /// 등록 직후 같은 색 매칭 검사까지 수행합니다.
    /// </summary>
    public void PlaceBalloon(Balloon balloon, Vector2Int cell)
    {
        cell = ClampColumn(cell);

        grid[cell] = balloon;
        balloon.transform.position = GridToWorld(cell.x, cell.y);
        balloon.Stop();
        balloon.gridCell = cell;

        OnBalloonAttached?.Invoke();

        comboDepth = 0; // 새 발사 사이클 시작 -> 연쇄 단계 카운터 초기화

        List<Vector2Int> poppedCells = CheckAndPopMatches(cell);

        if (poppedCells.Count > 0)
        {
            StartCoroutine(ProcessPopCascadeRoutine(poppedCells));
        }

        // 매칭으로 터지지 않고 그대로 남아있는 경우에만 위험선 도달 여부를 검사합니다.
        // (터진 풍선까지 게임오버 판정에 포함시키면 안 되므로 순서가 중요합니다.)
        if (grid.TryGetValue(cell, out Balloon stillThere) && stillThere == balloon)
        {
            if (balloon.transform.position.y <= dangerLineY)
            {
                OnDangerLineReached?.Invoke();
            }
        }
    }

    /// <summary>
    /// 특정 칸에서 시작해 같은 색으로 연결된 그룹을 BFS로 찾고,
    /// minMatchCount 이상이면 전부 터뜨립니다.
    /// 반환값: 이번에 터져서 비게 된 칸들의 목록 (터짐이 없었다면 빈 리스트)
    /// </summary>
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
            foreach (Vector2Int cell in group)
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
            OnBalloonsPopped?.Invoke(group.Count);
            OnComboStep?.Invoke(comboDepth);
        }

        return poppedCells;
    }

    /// <summary>
    /// 풍선이 터지는 위치에, 터진 풍선 색에 맞는 파티클 프리팹을 생성합니다.
    /// 프리팹이 RectTransform을 가진 UI 파티클(UIParticle 등)이면, 월드 좌표를 화면/캔버스 좌표로
    /// 변환해서 Canvas의 자식으로 정확한 위치에 배치합니다. 일반 월드 파티클이면 기존처럼 그대로 생성합니다.
    /// 해당 색의 프리팹이 비어있으면 아무 동작도 하지 않습니다.
    /// </summary>
    private void SpawnPopEffect(Vector3 worldPosition, BalloonColor color)
    {
        if (popEffectPrefabsByColor == null) return;

        int idx = (int)color;
        if (idx < 0 || idx >= popEffectPrefabsByColor.Length) return;

        GameObject prefab = popEffectPrefabsByColor[idx];
        if (prefab == null) return;

        RectTransform prefabRect = prefab.GetComponent<RectTransform>();

        if (prefabRect != null && effectCanvas != null)
        {
            // UI 파티클: Canvas 자식으로 생성 후, 월드 좌표를 캔버스 로컬 좌표로 변환해서 배치
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
            // 일반 월드 파티클
            Instantiate(prefab, worldPosition, Quaternion.identity);
        }
    }

    [Header("연쇄 반응 연출")]
    [Tooltip("밀려난 풍선이 자리를 잡은 뒤, 새로운 매칭을 판정하기까지 기다리는 시간(초). " +
             "풍선 슬라이드 이동 시간(기본 0.15초)보다 살짝 길게 잡아야 자연스럽습니다.")]
    public float pushSettleDelay = 0.2f;

    /// <summary>
    /// 터짐 -> 밀려나기 -> (잠시 대기) -> 밀려난 자리에서 새로운 매칭 판정 -> 또 터짐...
    /// 으로 이어지는 연쇄 반응을 코루틴으로 처리합니다. 각 단계 사이에 pushSettleDelay만큼
    /// 텀을 두어서, 여러 번의 터짐이 한 프레임에 겹쳐 보이지 않도록 합니다.
    /// </summary>
    private IEnumerator ProcessPopCascadeRoutine(List<Vector2Int> poppedCells)
    {
        List<Vector2Int> movedFarCells = PerformPush(poppedCells);

        if (movedFarCells.Count == 0) yield break;

        // 밀려난 풍선들의 슬라이드 애니메이션이 끝날 때까지 기다린 후 매칭 판정
        yield return new WaitForSeconds(pushSettleDelay);

        List<Vector2Int> newlyPoppedCells = new List<Vector2Int>();

        foreach (Vector2Int farCell in movedFarCells)
        {
            // 다른 그룹 매칭으로 이미 사라졌을 수 있으니 존재 여부 재확인
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
    /// 방금 터진 칸들 각각에 대해, 바로 옆에 있던 풍선을 터진 방향의 반대쪽(=자신을 지나 계속
    /// 이어지는 방향)으로 한 칸 밀어냅니다. 단, 그 자리에 이미 다른 풍선이 있거나 격자 범위를
    /// 벗어나면 이동하지 못하고 원래 자리에서 반동만 재생합니다.
    /// 반환값: 실제로 이동에 성공해서 자리를 옮긴 칸들의 목록 (매칭 판정은 호출자가 나중에 수행)
    /// </summary>
    private List<Vector2Int> PerformPush(List<Vector2Int> poppedCells)
    {
        List<Vector2Int> movedFarCells = new List<Vector2Int>();

        foreach (Vector2Int poppedCell in poppedCells)
        {
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
                    // 이동할 수 없는 경우: 제자리에서 살짝 반동만 재생
                    neighborBalloon.PlayRecoilBump(pushDir);
                    continue;
                }

                // 이동 실행: 격자 데이터 갱신 + 부드러운 슬라이드 애니메이션
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

    /// <summary>
    /// from(터진 칸)에서 through(그 옆 풍선)를 지나, 같은 방향으로 한 칸 더 이어지는 칸을 계산합니다.
    /// 육각 격자에서 방향을 정확히 다루기 위해 큐브 좌표계로 변환해서 계산합니다.
    /// </summary>
    private Vector2Int GetOppositeCell(Vector2Int from, Vector2Int through)
    {
        Vector3Int fromCube = OffsetToCube(from);
        Vector3Int throughCube = OffsetToCube(through);
        Vector3Int direction = throughCube - fromCube;
        Vector3Int farCube = throughCube + direction;
        return CubeToOffset(farCube);
    }

    // offset(row, col) 좌표 <-> 큐브(x, y, z) 좌표 변환 (odd-r 가로 배치 기준)
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
}