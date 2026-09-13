using System.Collections;
using UnityEngine;

/// <summary>
/// 풍선 하나(발사되어 날아가는 풍선 / 그리드에 붙은 풍선 공통)를 다루는 스크립트.
/// 발사된 풍선은 위로 이동하다가, 화면 천장 또는 이미 붙어있는 다른 풍선에 닿으면
/// HexGridManager를 통해 가장 가까운 빈 격자 칸에 스냅되어 붙습니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Balloon : MonoBehaviour
{
    public enum BalloonState
    {
        Moving,     // 발사되어 날아가는 중
        Attached,   // 그리드에 붙어서 정지한 상태
        Popped      // 터짐 처리됨 (곧 제거될 예정)
    }

    [Header("상태")]
    public BalloonColor color;
    public BalloonState state = BalloonState.Moving;

    [Tooltip("그리드에 붙은 이후 자신의 격자 좌표 (row, col)")]
    public Vector2Int gridCell;

    [Header("이동 설정 (Moving 상태일 때만 사용)")]
    public Vector2 velocity;

    [Header("실제 이미지 리소스 (선택 사항)")]
    [Tooltip("색깔별 몸통 스프라이트. 배열 순서는 BalloonColor enum 순서(Red, Blue, Green, Yellow, Purple)와 정확히 일치해야 합니다. " +
             "비워두면 기존처럼 단색 틴트로 표시됩니다.")]
    public Sprite[] bodySpritesByColor;

    [Tooltip("색깔별 끈 스프라이트. 순서는 bodySpritesByColor와 동일합니다. 비워두면 끈 없이 표시됩니다.")]
    public Sprite[] stringSpritesByColor;

    [Tooltip("끈을 표시할 자식 오브젝트의 SpriteRenderer. 비워두면 끈 표시를 생략합니다.")]
    public SpriteRenderer stringRenderer;

    [Header("충돌 판정 설정")]
    [Tooltip("이 반경 안에 다른 풍선이 있으면 충돌한 것으로 간주 (보통 풍선 지름과 비슷하게)")]
    public float collisionRadius = 1.1f;

    [Tooltip("HexGridManager가 씬에 없을 때 대신 사용할 예비 천장 y좌표. " +
             "HexGridManager가 있으면 그쪽의 topRowY를 우선 사용해서 격자 스냅 위치와 완전히 일치시킵니다.")]
    public float ceilingY = 8.4f;

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 스폰 시점에 색깔과 속도를 세팅합니다.
    /// </summary>
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
            // 실제 이미지가 있으면 그걸 사용하고, 틴트는 흰색(원본 색 그대로)으로 초기화
            spriteRenderer.sprite = bodySprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            // 이미지가 없는 색은 기존처럼 단색 원(기본 스프라이트) + 틴트로 대체
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

    void Update()
    {
        if (state != BalloonState.Moving) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        transform.position += (Vector3)(velocity * Time.deltaTime);

        if (CheckCollision())
        {
            AttachToGrid();
        }
    }

    /// <summary>
    /// 천장에 닿았는지, 혹은 이미 붙어있는 다른 풍선과 가까워졌는지 검사합니다.
    /// 천장 기준선은 HexGridManager의 topRowY를 우선 사용해서, 실제 스냅되는 격자 위치와
    /// 충돌이 감지되는 위치가 정확히 일치하도록 합니다. (그래야 "닿았다가 뚝 떨어지는" 현상이 없음)
    /// </summary>
    private bool CheckCollision()
    {
        float effectiveCeiling = HexGridManager.Instance != null
            ? HexGridManager.Instance.topRowY
            : ceilingY;

        if (transform.position.y >= effectiveCeiling)
            return true;

        // 씬에 존재하는 Attached 상태 풍선들과의 거리 체크
        // (풍선 개수가 매우 많아지면 성능 최적화가 필요하지만, 지금 단계에서는 단순 방식으로 충분)
        Balloon[] allBalloons = FindObjectsByType<Balloon>(FindObjectsSortMode.None);
        foreach (Balloon other in allBalloons)
        {
            if (other == this) continue;
            if (other.state != BalloonState.Attached) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist <= collisionRadius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 그리드 매니저에게 위임하여 가장 가까운 빈 칸에 스냅 배치시킵니다.
    /// </summary>
    private void AttachToGrid()
    {
        if (HexGridManager.Instance == null)
        {
            Debug.LogWarning("Balloon: HexGridManager.Instance가 없습니다. 씬에 GridManager 오브젝트를 추가하세요.");
            Stop();
            return;
        }

        Vector2Int targetCell = HexGridManager.Instance.FindNearestEmptyCell(transform.position);
        HexGridManager.Instance.PlaceBalloon(this, targetCell);
    }

    /// <summary>
    /// 이 풍선이 그리드에 붙는(Attached) 순간 호출되는 이벤트.
    /// BalloonSpawner가 구독해서 "다음 발사 가능" 신호로 사용합니다.
    /// </summary>
    public System.Action<Balloon> OnAttached;

    /// <summary>
    /// 이동을 멈추고 그리드에 붙은 것으로 간주합니다. (실제 위치/등록은 HexGridManager.PlaceBalloon에서 처리)
    /// </summary>
    public void Stop()
    {
        velocity = Vector2.zero;
        state = BalloonState.Attached;
        OnAttached?.Invoke(this);
    }

    private Coroutine slideCoroutine;
    private Coroutine bumpCoroutine;

    /// <summary>
    /// 이 풍선을 지정된 월드 좌표로 부드럽게 슬라이드 이동시킵니다.
    /// (터진 풍선의 반동으로 실제로 한 칸 밀려날 때 사용)
    /// </summary>
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

    /// <summary>
    /// 밀려나려고 했지만 막혀서 이동하지 못했을 때, 제자리에서 살짝 밀렸다가
    /// 돌아오는 반동 애니메이션만 재생합니다. 그리드 위치는 바뀌지 않습니다.
    /// </summary>
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