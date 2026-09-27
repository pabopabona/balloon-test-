using UnityEngine;

/// <summary>
/// 발사대 아래에 표시되는 홀드(조준) 제한시간 바.
/// LauncherController의 OnHoldTimeChanged 이벤트를 구독해서,
/// 남은 시간 비율만큼 fillRenderer의 Size(가로 폭)를 줄여나갑니다.
///
/// 9-Slice(Sliced) 스프라이트를 사용하는 것을 전제로 합니다 - 알약(캡슐) 모양처럼
/// 양 끝이 둥근 이미지를 쓸 때, 기존처럼 Transform.localScale로 늘였다 줄이면
/// 둥근 끝부분이 찌그러지기 때문입니다. SpriteRenderer의 Draw Mode를 Sliced로 설정하고
/// Sprite Editor에서 9-Slice 테두리(Border)를 지정해두면, Size만 바꿔도 양 끝 모양이
/// 항상 원래대로 유지된 채 가운데 부분만 늘었다 줄었다 합니다.
/// </summary>
public class HoldTimerBar : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("이벤트를 받아올 발사대 컨트롤러")]
    public LauncherController launcher;

    [Tooltip("실제로 크기가 줄어드는 바(9-Slice 스프라이트, Draw Mode: Sliced)")]
    public SpriteRenderer fillRenderer;

    [Header("표시 대상 (조준 중이 아닐 때 숨기기 위함)")]
    public SpriteRenderer backgroundRenderer;

    [Header("색상 (선택) - 시간이 얼마 안 남았을 때 경고색으로 변경")]
    public Color normalColor = new Color(0.3f, 0.85f, 0.4f);
    public Color warningColor = new Color(0.9f, 0.25f, 0.25f);
    [Range(0f, 1f)] public float warningThreshold = 0.3f;

    private float fullWidth;

    void Awake()
    {
        if (fillRenderer != null)
            fullWidth = fillRenderer.size.x; // 시작 시점의 크기를 "100%"로 기억해둠
    }

    void OnEnable()
    {
        if (launcher != null)
        {
            launcher.OnHoldTimeChanged += HandleHoldTimeChanged;
        }
        SetVisible(true);
    }

    void OnDisable()
    {
        if (launcher != null)
        {
            launcher.OnHoldTimeChanged -= HandleHoldTimeChanged;
        }
    }

    private void HandleHoldTimeChanged(float elapsed, float max)
    {
        float remainingRatio = Mathf.Clamp01(1f - (elapsed / max));

        if (fillRenderer != null)
        {
            Vector2 size = fillRenderer.size;
            size.x = fullWidth * remainingRatio;
            fillRenderer.size = size;

            fillRenderer.color = remainingRatio <= warningThreshold ? warningColor : normalColor;
        }
    }

    private void SetVisible(bool visible)
    {
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;
    }
}