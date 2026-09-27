using System.Collections;
using UnityEngine;

/// <summary>
/// 풍선 하나(발사되어 날아가는 풍선 / 그리드에 붙은 풍선 공통)를 다루는 스크립트.
/// 발사된 풍선은 위로 이동하다가, 화면 천장 또는 이미 붙어있는 다른 풍선에 닿으면
/// HexGridManager를 통해 격자 칸에 스냅되어 붙습니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Balloon : MonoBehaviour
{
    public enum BalloonState
    {
        Moving,
        Attached,
        Popped
    }

    [Header("상태")]
    public BalloonColor color;
    public BalloonState state = BalloonState.Moving;
    public Vector2Int gridCell;

    [Header("이동 설정 (Moving 상태일 때만 사용)")]
    public Vector2 velocity;

    [Header("실제 이미지 리소스 (선택 사항)")]
    public Sprite[] bodySpritesByColor;
    public Sprite[] stringSpritesByColor;
    public SpriteRenderer stringRenderer;

    [Header("충돌 판정 설정")]
    [Tooltip("이 반경 안에 다른 풍선이 있으면 충돌한 것으로 간주 (보통 풍선 지름과 비슷하게)")]
    public float collisionRadius = 1.1f;

    [Tooltip("HexGridManager가 씬에 없을 때 대신 사용할 예비 천장 y좌표.")]
    public float ceilingY = 8.4f;

    private SpriteRenderer spriteRenderer;

    // 이번 이동 중 실제로 충돌한 "특정 풍선". 격자 칸을 정확히 알고 있는 이 풍선을 기준으로
    // 이웃 칸을 계산하면, 위치 좌표를 반올림해서 칸을 추정하는 방식보다 훨씬 정확합니다.
    private Balloon collidedWithBalloon;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(BalloonColor balloonColor, Vector2 initialVelocity)
    {
        color = balloonColor;
        velocity = initialVelocity;
        state = BalloonState.Moving;
        ApplyColorVisual();
    }

    private void ApplyColorVisual()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        int idx = (int)color;
        Sprite bodySprite = (bodySpritesByColor != null && idx >= 0 && idx < bodySpritesByColor.Length)
            ? bodySpritesByColor[idx]
            : null;

        if (bodySprite != null)
        {
            spriteRenderer.sprite = bodySprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.color = BalloonColorUtil.ToColor(color);
        }

        if (stringRenderer != null)
        {
            Sprite stringSprite = (stringSpritesByColor != null && idx >= 0 && idx < stringSpritesByColor.Length)
                ? stringSpritesByColor[idx]
                : null;

            if (stringSprite != null)
            {
                stringRenderer.sprite = stringSprite;
                stringRenderer.color = Color.white;
                stringRenderer.enabled = true;
            }
            else
            {
                stringRenderer.enabled = false;
            }
        }
    }

    [Header("저사양 기기 대응 - 이동 세분화")]
    [Tooltip("프레임이 버벅여서 한 프레임에 이 거리(월드 유닛) 이상 이동해야 하면, " +
             "여러 단계로 쪼개서 이동시키며 매 단계마다 충돌 검사를 합니다. " +
             "이렇게 안 하면 저사양 기기에서 프레임 저하가 생겼을 때, 풍선이 한 번에 너무 멀리 " +
             "이동해버려서 옆에 있는 풍선을 그대로 통과(터널링)해버릴 수 있습니다.")]
    public float maxStepDistance = 0.1f;

    [Tooltip("한 프레임에 쪼갤 수 있는 최대 단계 수 (극단적인 렉 상황에서 성능이 더 나빠지는 것을 막는 안전장치)")]
    public int maxStepsPerFrame = 60;

    void Update()
    {
        if (state != BalloonState.Moving) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (HexGridManager.Instance != null && !HexGridManager.Instance.IsReady) return;

        float distanceThisFrame = velocity.magnitude * Time.deltaTime;
        int steps = Mathf.Clamp(Mathf.CeilToInt(distanceThisFrame / maxStepDistance), 1, maxStepsPerFrame);
        Vector2 stepMovement = velocity * (Time.deltaTime / steps);

        for (int i = 0; i < steps; i++)
        {
            transform.position += (Vector3)stepMovement;

            if (CheckCollision())
            {
                AttachToGrid();
                return; // 이미 붙었으니 이번 프레임의 남은 단계는 진행할 필요 없음
            }
        }
    }

    /// <summary>
    /// 천장에 닿았는지, 혹은 이미 붙어있는 다른 풍선과 가까워졌는지 검사합니다.
    /// 다른 풍선과 충돌했다면 그 풍선을 collidedWithBalloon에 기억해둡니다(정확한 배치를 위해).
    /// </summary>
    private bool CheckCollision()
    {
        collidedWithBalloon = null;

        // 격자 셀 크기 계산이 아직 안정화되기 전이면(앱 시작 직후 몇 프레임), 잘못된 계산값으로
        // 충돌/배치가 이루어지지 않도록 잠시 대기합니다. 실제로는 첫 발사까지 시간이 걸리기 때문에
        // 거의 항상 이미 준비된 상태겠지만, 혹시 모를 극단적인 상황을 대비한 안전장치입니다.
        if (HexGridManager.Instance != null && !HexGridManager.Instance.IsReady)
        {
            return false;
        }

        float effectiveCeiling = HexGridManager.Instance != null
            ? HexGridManager.Instance.topRowY
            : ceilingY;

        // 다른 풍선과의 충돌을 먼저 검사 (충돌한 특정 풍선을 알아야 정확한 배치가 가능하므로)
        Balloon[] allBalloons = FindObjectsByType<Balloon>(FindObjectsSortMode.None);
        Balloon closest = null;
        float closestDist = float.MaxValue;

        foreach (Balloon other in allBalloons)
        {
            if (other == this) continue;
            if (other.state != BalloonState.Attached) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist <= collisionRadius && dist < closestDist)
            {
                closestDist = dist;
                closest = other;
            }
        }

        if (closest != null)
        {
            collidedWithBalloon = closest;
            return true;
        }

        if (transform.position.y >= effectiveCeiling)
            return true;

        return false;
    }

    private void AttachToGrid()
    {
        if (HexGridManager.Instance == null)
        {
            Debug.LogWarning("Balloon: HexGridManager.Instance가 없습니다. 씬에 GridManager 오브젝트를 추가하세요.");
            Stop();
            return;
        }

        Vector2Int targetCell;

        if (collidedWithBalloon != null)
        {
            // 충돌한 풍선의 "정확한" 격자 좌표를 알고 있으므로, 그 이웃 칸 중 비어있고
            // 지금 내 위치와 가장 가까운 칸을 선택합니다. 위치를 반올림해서 칸을 추정하는 것보다
            // 훨씬 정확해서, 인접 판정이 어긋나는 문제를 방지합니다.
            targetCell = HexGridManager.Instance.FindNearestEmptyNeighborOf(collidedWithBalloon.gridCell, transform.position);
        }
        else
        {
            // 천장에만 닿은 경우: 기존처럼 위치 기반으로 가장 가까운 빈 칸을 찾음
            targetCell = HexGridManager.Instance.FindNearestEmptyCell(transform.position);
        }

        HexGridManager.Instance.PlaceBalloon(this, targetCell);
    }

    public void Stop()
    {
        velocity = Vector2.zero;
        state = BalloonState.Attached;
    }

    private Coroutine slideCoroutine;
    private Coroutine bumpCoroutine;

    public void StartSlide(Vector3 targetWorldPosition, float duration = 0.15f)
    {
        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        slideCoroutine = StartCoroutine(SlideRoutine(targetWorldPosition, duration));
    }

    private IEnumerator SlideRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        slideCoroutine = null;
    }

    public void PlayRecoilBump(Vector3 direction, float bumpDistance = 0.12f, float duration = 0.12f)
    {
        if (bumpCoroutine != null)
            StopCoroutine(bumpCoroutine);

        Vector3 restPos = HexGridManager.Instance != null
            ? HexGridManager.Instance.GridToWorld(gridCell.x, gridCell.y)
            : transform.position;

        bumpCoroutine = StartCoroutine(RecoilRoutine(restPos, direction.normalized * bumpDistance, duration));
    }

    private IEnumerator RecoilRoutine(Vector3 restPos, Vector3 offset, float duration)
    {
        Vector3 bumpedPos = restPos + offset;
        float half = duration / 2f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(restPos, bumpedPos, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(bumpedPos, restPos, t / half);
            yield return null;
        }

        transform.position = restPos;
        bumpCoroutine = null;
    }

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            ApplyColorVisual();
    }
}