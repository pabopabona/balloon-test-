using UnityEngine;

/// <summary>
/// 카메라의 Viewport Rect를 조절해서, 화면 비율이 목표 비율(기본 9:16)과 다르더라도
/// 항상 그 비율만큼만 렌더링되도록 고정합니다. 남는 영역은 이 카메라가 그리지 않아서
/// 검은 여백(레터박스/필러박스)으로 남게 됩니다.
///
/// 중요: HexGridManager 등 다른 스크립트가 카메라 비율을 읽어 격자 크기를 계산하기 때문에,
/// 이 스크립트는 그보다 반드시 먼저 실행되어야 합니다. [DefaultExecutionOrder(-1000)]로
/// 항상 가장 먼저 실행되도록 강제하고, 보정도 Awake 단계에서 즉시 적용합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Camera))]
public class AspectRatioFitter : MonoBehaviour
{
    public float targetWidth = 9f;
    public float targetHeight = 16f;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyAspectRatio();
    }

    void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyAspectRatio();
        }
    }

    private void ApplyAspectRatio()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float targetAspect = targetWidth / targetHeight;
        float windowAspect = (float)Screen.width / Screen.height;

        Rect rect = new Rect(0f, 0f, 1f, 1f);

        if (windowAspect > targetAspect)
        {
            float widthFraction = targetAspect / windowAspect;
            rect.width = widthFraction;
            rect.height = 1f;
            rect.x = (1f - widthFraction) / 2f;
            rect.y = 0f;
        }
        else if (windowAspect < targetAspect)
        {
            float heightFraction = windowAspect / targetAspect;
            rect.width = 1f;
            rect.height = heightFraction;
            rect.x = 0f;
            rect.y = (1f - heightFraction) / 2f;
        }

        cam.rect = rect;
    }
}