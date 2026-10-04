using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ChangeColorCtrl
{
    public float changeAlpha = 0;
    public Color changeColor = Color.white;
}

/// <summary>
/// 같은 오브젝트에 있는 Image 또는 TextMeshPro의 색/투명도를 애니메이션합니다.
/// - 오브젝트가 켜질 때마다(OnEnable) 원래 색에서 시작해 TimeLength 동안 목표 값으로 바뀝니다.
/// - 꺼질 때(OnDisable) 원래 색으로 되돌립니다.
/// - Alpha_Value: 투명도만 changeAlpha로 / Color_Value: 색 전체를 changeColor로
///
/// Image, RawImage, TextMeshPro 등 UI 그래픽이면 자동으로 인식합니다(따로 연결할 필요 없음).
/// </summary>
public class UIImgNTextMeshProCtrl : MonoBehaviour
{
    public float TimeLength = 1;

    public enum ValueType
    {
        Alpha_Value,
        Color_Value
    }
    public ValueType valueType;
    public ChangeColorCtrl changeData;

    [Tooltip("진행 곡선. 가로축 = 진행률(0~1), 세로축 = 0이면 원래 값, 1이면 목표 값. " +
             "기본값은 직선(일정한 속도)입니다. 0→1→0 모양으로 그리면 바뀌었다가 원래대로 돌아옵니다.")]
    public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("켜면 Time.timeScale과 무관하게 실제 시간으로 재생됩니다. " +
             "게임이 잠깐 멈춘 동안(스킬 발동 연출 등)에도 계속 움직여야 하면 켜두세요.")]
    public bool UseUnscaledTime = true;

    private Graphic graphic;        // Image / RawImage / TMP_Text 모두 Graphic을 상속합니다
    private Color baseColor = Color.white;
    private Coroutine routine;
    private bool initialized;

    void Awake()
    {
        Init();
    }

    void Init()
    {
        graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning($"[UIImgNTextMeshProCtrl] '{name}': Image 또는 TextMeshPro가 없습니다. " +
                             "색을 바꿀 UI 오브젝트 자신에게 붙여주세요.", this);
            return;
        }

        baseColor = graphic.color;   // 시작 시점의 색을 "원래 색"으로 기억
        initialized = true;
    }

    void OnEnable()
    {
        if (!initialized) return;

        graphic.color = baseColor;
        routine = StartCoroutine(Play());
    }

    void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (initialized && graphic != null)
            graphic.color = baseColor;
    }

    private IEnumerator Play()
    {
        float currentTime = 0f;

        while (currentTime < TimeLength)
        {
            if (graphic == null) yield break;

            currentTime += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Apply(TimeLength > 0f ? Mathf.Clamp01(currentTime / TimeLength) : 1f);

            yield return null;
        }

        if (graphic != null) Apply(1f); // 마지막 값을 정확히 맞춰줌
        routine = null;
    }

    /// <summary>진행률(0~1)에 해당하는 색을 적용합니다.</summary>
    private void Apply(float normalTime)
    {
        float t = Curve != null ? Curve.Evaluate(normalTime) : normalTime;

        if (valueType == ValueType.Color_Value)
        {
            graphic.color = Color.LerpUnclamped(baseColor, changeData.changeColor, t);
        }
        else
        {
            Color c = graphic.color;
            c.a = Mathf.LerpUnclamped(baseColor.a, changeData.changeAlpha, t);
            graphic.color = c;
        }
    }
}