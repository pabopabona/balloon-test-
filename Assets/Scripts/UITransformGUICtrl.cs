using System;
using UnityEngine;

/// <summary>
/// UI(RectTransform)의 위치/크기/회전을 애니메이션 커브로 움직이는 컴포넌트.
/// - 오브젝트가 켜질 때마다(OnEnable) 처음부터 다시 재생됩니다.
/// - Once가 꺼져 있으면 TimeLength 주기로 계속 반복합니다.
/// - IsManual이 켜져 있으면 자동 재생하지 않고, PlayManual()을 호출했을 때 재생합니다.
///
/// 배너처럼 "켜질 때 한 번 재생"하려면, 움직일 오브젝트 자신에게 붙이고(obj = None) Once를 켜세요.
/// </summary>
public class UITransformGUICtrl : MonoBehaviour
{
    public GameObject obj;
    public GameObject TargetObj;
    public float TimeLength = 1.0f;
    public bool Once = false;

    [Tooltip("켜면 Time.timeScale과 무관하게 실제 시간으로 재생됩니다. " +
             "게임이 잠깐 멈춘 동안(스킬 발동 연출 등)에도 UI 애니메이션이 계속 움직여야 하면 켜두세요.")]
    public bool UseUnscaledTime = true;

    [Tooltip("켜면 재생 중에 대상 오브젝트(obj)를 강제로 활성화합니다. " +
             "다른 스크립트가 그 오브젝트를 켜고 끄는 경우(콤보/배너 등)에는 서로 충돌하므로 꺼두세요.")]
    public bool ActivateTargetWhilePlaying = false;

    public bool IsManual;

    public enum ValueType
    {
        Position_Value,
        Scale_Value,
        Rotation_Value,
    }
    public ValueType valueType;

    public enum PositionType
    {
        AddXY_Value,
        OnlyX_Value,
        OnlyY_Value,
        NoneXY_Value,
        Target_Value,
    }
    public PositionType positionType;

    public enum ScaleType
    {
        One_Value,
        SaperateXY_Value,
    }
    public ScaleType scaleType;

    // 커브의 가로축은 0~1(진행률), 세로축이 실제 값입니다.
    // 주의: 기본값은 전부 0이므로, Scale 타입에서 커브를 설정하지 않으면 크기가 0(안 보임)이 됩니다.
    public AnimationCurve FAnimation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
    public AnimationCurve F2Animation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
    public AnimationCurve RAnimation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
    public AnimationCurve XAnimation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
    public AnimationCurve YAnimation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));
    public AnimationCurve TargetAnimation = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 0.0f));

    [NonSerialized] public RectTransform RTransform;
    [NonSerialized] public RectTransform TargetRTransform;

    // 재생 중 계산된 현재 값 (다른 스크립트에서 읽을 수 있음, 저장되지는 않음)
    [NonSerialized] public float ValueCacheX = 1.0f;
    [NonSerialized] public float ValueCacheY = 1.0f;
    [NonSerialized] public float ValueCacheR = 1.0f;
    [NonSerialized] public float ValueCache = 1.0f;
    [NonSerialized] public float ValueCache2 = 1.0f;
    [NonSerialized] public float ValueTargetCache = 1.0f;

    private Vector3 vec;            // 시작 시점의 기준 값(위치 또는 크기)
    private Vector3 Tvec;           // Target 모드에서의 목표 위치
    private float currentTime = 0.0f;
    private bool manualAtStart;     // Inspector에서 설정한 IsManual 원래 값
    private bool initialized;

    void Awake()
    {
        Init();
    }

    public void Init()
    {
        RTransform = obj == null ? GetComponent<RectTransform>() : obj.GetComponent<RectTransform>();

        if (RTransform == null)
        {
            Debug.LogWarning($"[UITransformGUICtrl] '{name}': RectTransform을 찾을 수 없습니다. " +
                             "UI 오브젝트에 붙이거나 obj에 UI 오브젝트를 연결하세요.", this);
            return;
        }

        SetPropertyGUICtrl();
        manualAtStart = IsManual;
        initialized = true;

        if (IsManual) return;
        SetTransformFromAnimationcurve(0);
    }

    /// <summary>IsManual 상태에서 애니메이션을 처음부터 재생합니다. 여러 번 호출해도 됩니다.</summary>
    public void PlayManual()
    {
        if (!initialized) return;

        currentTime = 0;
        SetTransformFromAnimationcurve(0);
        IsManual = false;
    }

    public void SetPropertyGUICtrl()
    {
        if (RTransform == null) return;

        if (valueType == ValueType.Position_Value)
        {
            vec = RTransform.anchoredPosition3D;

            if (positionType == PositionType.Target_Value)
            {
                if (TargetObj == null)
                {
                    Debug.LogWarning($"[UITransformGUICtrl] '{name}': Position Type이 Target인데 TargetObj가 비어 있습니다.", this);
                    Tvec = vec; // 목표가 없으면 제자리에 있도록
                    return;
                }

                TargetRTransform = TargetObj.GetComponent<RectTransform>();
                Tvec = TargetRTransform != null ? TargetRTransform.anchoredPosition3D : vec;
            }
        }
        else if (valueType == ValueType.Scale_Value)
        {
            vec = RTransform.localScale;
        }
    }

    private void OnEnable()
    {
        currentTime = 0;
        if (initialized) IsManual = manualAtStart;
    }

    private void OnDisable()
    {
        currentTime = 0;
        if (initialized) IsManual = manualAtStart;
    }

    private void LateUpdate()
    {
        if (IsManual || !initialized) return;
        if (RTransform == null) return; // 대상이 파괴된 경우

        if (currentTime < TimeLength)
        {
            float normalTime = TimeLength > 0f ? currentTime / TimeLength : 1.0f;
            currentTime += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (currentTime >= TimeLength)
            {
                normalTime = 1.0f;
            }

            SetTransformFromAnimationcurve(normalTime);

            if (ActivateTargetWhilePlaying && !RTransform.gameObject.activeSelf)
            {
                RTransform.gameObject.SetActive(true);
            }
        }
        else if (!Once)
        {
            currentTime = 0; // 반복 재생
        }
    }

    private void SetTransformFromAnimationcurve(float normalTime)
    {
        if (RTransform == null) return;

        ValueCache = FAnimation.Evaluate(normalTime);
        ValueCache2 = F2Animation.Evaluate(normalTime);
        ValueCacheR = RAnimation.Evaluate(normalTime);
        ValueCacheX = XAnimation.Evaluate(normalTime);
        ValueCacheY = YAnimation.Evaluate(normalTime);
        ValueTargetCache = TargetAnimation.Evaluate(normalTime);

        if (valueType == ValueType.Position_Value)
        {
            if (positionType == PositionType.AddXY_Value)
            {
                RTransform.anchoredPosition3D = new Vector3(vec.x + ValueCacheX, vec.y + ValueCacheY, vec.z);
            }
            else if (positionType == PositionType.OnlyX_Value)
            {
                RTransform.anchoredPosition3D = new Vector3(ValueCacheX, vec.y, vec.z);
            }
            else if (positionType == PositionType.OnlyY_Value)
            {
                RTransform.anchoredPosition3D = new Vector3(vec.x, ValueCacheY, vec.z);
            }
            else if (positionType == PositionType.NoneXY_Value)
            {
                RTransform.anchoredPosition3D = new Vector3(ValueCacheX, ValueCacheY, vec.z);
            }
            else
            {
                RTransform.anchoredPosition3D = Vector3.LerpUnclamped(vec, Tvec, ValueTargetCache);
            }
        }
        else if (valueType == ValueType.Scale_Value)
        {
            if (scaleType == ScaleType.One_Value)
            {
                RTransform.localScale = vec * ValueCache;
            }
            else
            {
                RTransform.localScale = new Vector3(vec.x * ValueCache, vec.y * ValueCache2, vec.z);
            }
        }
        else
        {
            RTransform.localRotation = Quaternion.Euler(0, 0, ValueCacheR);
        }
    }

    /// <summary>자동 재생 중인 애니메이션을 처음으로 되감습니다.</summary>
    public void RestartUITransform()
    {
        currentTime = 0;
    }

    /// <summary>진행률(0~1)을 직접 지정해 그 순간의 모습으로 만듭니다.</summary>
    public void SetAnimationCurve(float value)
    {
        SetTransformFromAnimationcurve(value);
    }
}