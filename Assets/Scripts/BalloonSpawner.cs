using UnityEngine;

/// <summary>
/// LauncherController의 발사 이벤트를 받아서 실제 풍선(Balloon) 오브젝트를 생성하고 쏘는 역할.
/// 발사대(Launcher) 오브젝트에 같이 붙여서 사용합니다.
///
/// 중요: 다음 발사는 "풍선이 그리드에 붙는 순간"이 아니라, "이번 발사로 시작된 매칭/연쇄
/// 반응이 완전히 다 끝나는 순간"(HexGridManager.OnPlacementSettled)에만 허용합니다.
/// 연쇄가 끝나기 전에 다음 풍선이 끼어들면, 격자 데이터가 동시에 바뀌면서 풍선이 겹치거나
/// 밀려났다가 되돌아오거나 매칭이 어긋나는 등의 문제가 생기기 때문입니다.
/// </summary>
[RequireComponent(typeof(LauncherController))]
public class BalloonSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("발사될 풍선 프리팹 (Balloon 스크립트가 붙어있어야 함)")]
    public Balloon balloonPrefab;

    [Header("현재 발사 대기 중인 풍선 색")]
    public BalloonColor currentColor;

    [Header("미리보기 이미지 (선택 사항)")]
    public SpriteRenderer currentColorPreview;
    public Sprite[] previewSpritesByColor;

    private LauncherController launcher;

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

        if (HexGridManager.Instance != null)
            HexGridManager.Instance.OnPlacementSettled -= HandlePlacementSettled;
    }

    private void HandleShoot(Vector3 spawnPosition, Vector2 velocity)
    {
        if (balloonPrefab == null)
        {
            Debug.LogWarning("BalloonSpawner: balloonPrefab이 연결되어 있지 않습니다.");
            return;
        }

        Balloon newBalloon = Instantiate(balloonPrefab, spawnPosition, Quaternion.identity);
        newBalloon.Initialize(currentColor, velocity);

        // 이번 풍선의 배치+매칭+연쇄가 완전히 끝날 때까지 다음 발사를 막음
        launcher.SetShootingBlocked(true);

        if (HexGridManager.Instance != null)
        {
            HexGridManager.Instance.OnPlacementSettled += HandlePlacementSettled;
        }
        else
        {
            // 안전장치: 그리드 매니저가 없는 예외적인 상황이면 바로 풀어줌
            launcher.SetShootingBlocked(false);
        }

        PickNewColor();
    }

    /// <summary>
    /// 이번 발사로 시작된 매칭/연쇄가 완전히 끝났을 때 호출되어 다음 발사를 허용합니다.
    /// </summary>
    private void HandlePlacementSettled()
    {
        if (HexGridManager.Instance != null)
            HexGridManager.Instance.OnPlacementSettled -= HandlePlacementSettled;

        launcher.SetShootingBlocked(false);
    }

    private void PickNewColor()
    {
        int level = launcher != null ? launcher.currentLevel : 1;

        currentColor = GameManager.Instance != null
            ? GameManager.Instance.GetRandomColorForLevel(level)
            : BalloonColorUtil.GetRandom();

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