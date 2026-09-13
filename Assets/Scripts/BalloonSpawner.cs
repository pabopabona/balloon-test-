using UnityEngine;

/// <summary>
/// LauncherController의 발사 이벤트를 받아서 실제 풍선(Balloon) 오브젝트를 생성하고 쏘는 역할.
/// 발사대(Launcher) 오브젝트에 같이 붙여서 사용합니다.
/// </summary>
[RequireComponent(typeof(LauncherController))]
public class BalloonSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("발사될 풍선 프리팹 (Balloon 스크립트가 붙어있어야 함)")]
    public Balloon balloonPrefab;

    [Header("현재 발사 대기 중인 풍선 색")]
    public BalloonColor currentColor;

    [Tooltip("현재 색을 화면에 미리 보여줄 SpriteRenderer (발사대 위 대기 풍선 등). 없어도 동작에는 문제없음")]
    public SpriteRenderer currentColorPreview;

    [Header("미리보기 이미지 (선택 사항)")]
    [Tooltip("색깔별 미리보기용 스프라이트. 순서는 BalloonColor enum 순서(Red, Blue, Green, Yellow, Purple)와 " +
             "정확히 일치해야 합니다. Balloon 프리팹의 Body Sprites By Color와 같은 이미지를 넣으면 됩니다. " +
             "비워두면 기존처럼 단색 틴트로 표시됩니다.")]
    public Sprite[] previewSpritesByColor;

    private LauncherController launcher;

    // 현재 날아가고 있는 풍선. 이 풍선이 그리드에 붙기(Attached) 전까지는 새로 발사되지 않도록 막는 용도.
    private Balloon activeBalloon;

    void Awake()
    {
        launcher = GetComponent<LauncherController>();
    }

    void OnEnable()
    {
        if (launcher != null)
            launcher.OnShoot += HandleShoot;

        PickNewColor();
    }

    void OnDisable()
    {
        if (launcher != null)
            launcher.OnShoot -= HandleShoot;
    }

    /// <summary>
    /// 발사 이벤트 수신 시 실제 Balloon 인스턴스를 생성하고 속도를 부여합니다.
    /// </summary>
    private void HandleShoot(Vector3 spawnPosition, Vector2 velocity)
    {
        if (balloonPrefab == null)
        {
            Debug.LogWarning("BalloonSpawner: balloonPrefab이 연결되어 있지 않습니다.");
            return;
        }

        // 이전에 발사한 풍선이 아직 날아가는 중이면(그리드에 붙지 않았으면) 새 발사를 무시
        // (LauncherController.CanShoot이 false일 때는 애초에 이 이벤트 자체가 호출되지 않지만,
        //  이중 안전장치로 한 번 더 확인합니다.)
        if (activeBalloon != null && activeBalloon.state == Balloon.BalloonState.Moving)
        {
            return;
        }

        Balloon newBalloon = Instantiate(balloonPrefab, spawnPosition, Quaternion.identity);
        newBalloon.Initialize(currentColor, velocity);
        activeBalloon = newBalloon;
        activeBalloon.OnAttached += HandleBalloonAttached;

        // 발사한 풍선이 그리드에 붙을 때까지 발사대의 타이머/발사를 정지
        launcher.SetShootingBlocked(true);

        // 다음 발사를 위해 새 색을 미리 뽑아둠
        PickNewColor();
    }

    /// <summary>
    /// 발사된 풍선이 그리드에 붙었을 때 호출됩니다. 다음 발사를 다시 허용합니다.
    /// </summary>
    private void HandleBalloonAttached(Balloon balloon)
    {
        balloon.OnAttached -= HandleBalloonAttached;

        if (activeBalloon == balloon)
        {
            activeBalloon = null;
            launcher.SetShootingBlocked(false);
        }
    }

    /// <summary>
    /// 다음 발사할 풍선 색을 랜덤으로 뽑고, 미리보기가 있으면 갱신합니다.
    /// </summary>
    private void PickNewColor()
    {
        currentColor = BalloonColorUtil.GetRandom();

        if (currentColorPreview == null) return;

        int idx = (int)currentColor;
        Sprite previewSprite = (previewSpritesByColor != null && idx >= 0 && idx < previewSpritesByColor.Length)
            ? previewSpritesByColor[idx]
            : null;

        if (previewSprite != null)
        {
            currentColorPreview.sprite = previewSprite;
            currentColorPreview.color = Color.white;
        }
        else
        {
            currentColorPreview.color = BalloonColorUtil.ToColor(currentColor);
        }
    }
}