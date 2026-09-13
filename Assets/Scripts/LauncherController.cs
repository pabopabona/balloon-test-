using UnityEngine;

/// <summary>
/// 발사대(Launcher) 컨트롤러.
/// - 화면을 터치/드래그하면 그 x좌표를 따라 발사대가 좌우로 이동
/// - 좌우 이동 범위는 카메라 화면 폭 안으로 제한(Clamp)
/// - 터치를 뗄 때(또는 홀드 시간 제한 초과 시) 위쪽(90도 고정)으로 발사 이벤트 발생
/// - 조준(홀드) 가능 시간에 레벨별 제한을 두어, 너무 오래 들고 있으면 자동 발사됨
/// </summary>
public class LauncherController : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("발사대가 화면 좌우 끝에서 얼마나 여백을 둘지 (Unity 유닛 단위)")]
    public float horizontalPadding = 0.6f;

    [Tooltip("터치 위치로 얼마나 부드럽게 따라갈지 (1이면 즉시 따라감)")]
    [Range(0.05f, 1f)]
    public float followSmoothness = 0.1f;

    [Header("발사 설정")]
    [Tooltip("풍선이 실제로 생성/발사되는 기준점")]
    public Transform firePoint;

    [Tooltip("발사 속도 (풍선 이동 스크립트 쪽에서 사용할 값)")]
    public float shootSpeed = 12f;

    [Header("홀드 타임 리밋 설정")]
    [Tooltip("현재 레벨 (1부터 시작, 높을수록 허용 시간이 줄어듦)")]
    public int currentLevel = 1;

    [Tooltip("레벨 1일 때 허용되는 최대 홀드(조준) 시간(초)")]
    public float baseHoldTime = 5f;

    [Tooltip("레벨이 아무리 높아져도 보장되는 최소 홀드 시간(초)")]
    public float minHoldTime = 0.5f;

    [Tooltip("레벨에 따라 허용 시간이 줄어드는 속도. 값이 클수록 초반 레벨부터 빠르게 짧아짐")]
    public float decayRate = 0.15f;

    /// <summary>
    /// 홀드 시간이 갱신될 때마다 (경과시간, 최대허용시간)을 전달하는 이벤트.
    /// UI에 타이머 바/게이지 등을 붙일 때 구독해서 사용하면 됩니다.
    /// </summary>
    public System.Action<float, float> OnHoldTimeChanged;

    // 외부(GameManager, BalloonSpawner 등)에서 구독해서 실제 발사 로직을 처리
    public System.Action<Vector3, Vector2> OnShoot;

    /// <summary>
    /// 지금 발사가 가능한 상태인지 여부. false면 홀드 타이머가 흐르지 않고, 발사 시도도 무시됩니다.
    /// (이전에 발사한 풍선이 아직 그리드에 붙지 않았을 때 BalloonSpawner가 이 값을 false로 설정합니다)
    /// </summary>
    public bool CanShoot { get; private set; } = true;

    /// <summary>
    /// 발사 가능 여부를 외부(BalloonSpawner)에서 설정합니다.
    /// </summary>
    public void SetShootingBlocked(bool blocked)
    {
        CanShoot = !blocked;
    }

    private Camera mainCamera;
    private float minX;
    private float maxX;
    private bool isDragging = false;
    private float holdTimer = 0f;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Start()
    {
        CalculateHorizontalBounds();
    }

    /// <summary>
    /// 카메라의 Orthographic Size와 화면 비율을 이용해 발사대가 움직일 수 있는 좌우 한계를 계산합니다.
    /// </summary>
    private void CalculateHorizontalBounds()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        minX = -halfWidth + horizontalPadding;
        maxX = halfWidth - horizontalPadding;
    }

    /// <summary>
    /// 현재 레벨 기준으로 허용되는 최대 홀드(조준) 시간을 계산합니다.
    /// 레벨이 올라갈수록 baseHoldTime에서 minHoldTime 쪽으로 지수적으로 줄어듭니다.
    /// </summary>
    public float GetMaxHoldTime()
    {
        float t = Mathf.Exp(-decayRate * (currentLevel - 1));
        float value = minHoldTime + (baseHoldTime - minHoldTime) * t;
        return Mathf.Max(value, minHoldTime);
    }

    void Update()
    {
        HandleInput();
        HandleHoldTimer();
    }

    /// <summary>
    /// 터치 여부와 관계없이 항상 흐르는 타이머입니다. 단, CanShoot이 false인 동안(이전 풍선이 아직
    /// 그리드에 붙지 않은 상태)에는 타이머가 흐르지 않고 그대로 멈춰있습니다.
    /// </summary>
    private void HandleHoldTimer()
    {
        if (!CanShoot) return; // 발사 대기 상태: 타이머 정지

        holdTimer += Time.deltaTime;
        float maxHold = GetMaxHoldTime();

        OnHoldTimeChanged?.Invoke(holdTimer, maxHold);

        if (holdTimer >= maxHold)
        {
            // 시간 초과: 현재 발사대 위치(터치 중이 아니어도 마지막 위치) 기준으로 자동 발사
            TryShoot();
        }
    }

    private void HandleInput()
    {
        // 마우스(에디터 테스트용) / 터치(모바일) 공용 처리
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            StartDragging();
        }
        if (Input.GetMouseButton(0) && isDragging)
        {
            MoveToScreenPosition(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            TryShoot();
        }
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartDragging();
                    MoveToScreenPosition(touch.position);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isDragging)
                        MoveToScreenPosition(touch.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (isDragging)
                    {
                        isDragging = false;
                        TryShoot();
                    }
                    break;
            }
        }
#endif
    }

    private void StartDragging()
    {
        isDragging = true;
    }

    /// <summary>
    /// 화면 좌표(픽셀)를 월드 좌표로 변환해서 발사대의 x 위치를 갱신합니다.
    /// </summary>
    private void MoveToScreenPosition(Vector3 screenPosition)
    {
        if (mainCamera == null) return;

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));

        float targetX = Mathf.Clamp(worldPos.x, minX, maxX);

        Vector3 newPos = transform.position;
        newPos.x = followSmoothness >= 1f
            ? targetX
            : Mathf.Lerp(transform.position.x, targetX, followSmoothness);

        transform.position = newPos;
    }

    /// <summary>
    /// 발사 트리거. 실제 풍선 생성/이동 로직은 이 이벤트를 구독하는 쪽(BalloonSpawner 등)에서 처리합니다.
    /// CanShoot이 false인 동안에는(이전 풍선이 아직 처리 중) 아무 동작도 하지 않습니다.
    /// </summary>
    private void TryShoot()
    {
        if (!CanShoot) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector2 direction = Vector2.up; // 90도 고정 발사

        OnShoot?.Invoke(spawnPos, direction * shootSpeed);

        holdTimer = 0f;
    }

    // 씬 뷰에서 발사대 이동 가능 범위를 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            CalculateHorizontalBounds();
        }

        Gizmos.color = Color.yellow;
        Vector3 leftPoint = new Vector3(minX, transform.position.y, 0);
        Vector3 rightPoint = new Vector3(maxX, transform.position.y, 0);
        Gizmos.DrawLine(leftPoint + Vector3.up * 0.5f, leftPoint + Vector3.down * 0.5f);
        Gizmos.DrawLine(rightPoint + Vector3.up * 0.5f, rightPoint + Vector3.down * 0.5f);
        Gizmos.DrawLine(leftPoint, rightPoint);
    }
}