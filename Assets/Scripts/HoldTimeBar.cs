using UnityEngine;

/// <summary>
/// 발사대 아래에 표시되는 홀드(조준) 제한시간 바.
/// LauncherController의 OnHoldTimeChanged 이벤트를 구독해서,
/// 남은 시간 비율만큼 막대(fillTransform)의 가로 크기를 줄여나갑니다.
/// 조준 중이 아닐 때는 자동으로 숨겨집니다.
/// </summary>
public class HoldTimerBar : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("이벤트를 받아올 발사대 컨트롤러")]
    public LauncherController launcher;

    [Tooltip("실제로 크기가 줄어드는 막대(Sprite) Transform")]
    public Transform fillTransform;

    [Header("표시 대상 (조준 중이 아닐 때 숨기기 위함)")]
    public SpriteRenderer backgroundRenderer;
    public SpriteRenderer fillRenderer;

    [Header("색상 (선택) - 시간이 얼마 안 남았을 때 경고색으로 변경")]
    public Color normalColor = new Color(0.3f, 0.85f, 0.4f);
    public Color warningColor = new Color(0.9f, 0.25f, 0.25f);
    [Range(0f, 1f)] public float warningThreshold = 0.3f; // 남은 비율이 이 값 이하면 경고색

    private float fullScaleX;

    void Awake()
    {
        if (fillTransform != null)
            fullScaleX = fillTransform.localScale.x;
    }

    void OnEnable()
    {
        if (launcher != null)
        {
            launcher.OnHoldTimeChanged += HandleHoldTimeChanged;
        }
        SetVisible(true); // 타이머가 항상 흐르므로 바도 항상 표시
    }

    void OnDisable()
    {
        if (launcher != null)
        {
            launcher.OnHoldTimeChanged -= HandleHoldTimeChanged;
        }
    }

    /// <summary>
    /// elapsed: 지금까지 조준한 시간, max: 이번 레벨에서 허용되는 최대 시간
    /// </summary>
    private void HandleHoldTimeChanged(float elapsed, float max)
    {
        float remainingRatio = Mathf.Clamp01(1f - (elapsed / max));

        if (fillTransform != null)
        {
            Vector3 scale = fillTransform.localScale;
            scale.x = fullScaleX * remainingRatio;
            fillTransform.localScale = scale;
        }

        if (fillRenderer != null)
        {
            fillRenderer.color = remainingRatio <= warningThreshold ? warningColor : normalColor;
        }
    }

    private void SetVisible(bool visible)
    {
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;
    }
}