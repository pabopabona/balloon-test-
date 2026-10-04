using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 레인보우 / 회색 풍선의 "스킬" 관리.
///
/// 레인보우 스킬 (타이머 완화)
///   - 레인보우 매칭(3개 이상 터뜨리기)을 rainbowMatchesRequired번(기본 5) 하면 즉시 발동
///   - 발사(홀드) 타이머의 기준 레벨을 절반으로 낮춤: 레벨 10에서 발동 → 레벨 5의 속도
///   - 이후 레벨이 오르면 거기서부터 다시 조금씩 빨라짐 (여러 번 발동하면 그때마다 다시 절반)
///
/// 회색 스킬 (화면 정리)
///   - 회색 매칭을 grayMatchesRequired번(기본 5) 하면 발동
///   - 맨 위 keepTopRows줄(기본 1줄)만 남기고 화면의 풍선을 전부 터뜨림
///   - 진행 중이던 연쇄 반응이 다 끝난 직후에 실행됩니다(격자가 꼬이지 않도록)
///
/// 발동 연출
///   - 스킬이 발동하면 콤보처럼 배너 이미지를 화면에 잠깐 띄우고, 게임을 잠깐 멈춥니다(pauseDuration)
///   - 멈춘 동안에는 풍선/타이머/발사대 조작이 모두 정지하고, 회색 스킬의 화면 정리는 멈춤이 끝난 뒤 시작됩니다
///
/// 게이지 UI
///   - 각 스킬의 첫 매칭 때 아이콘+바가 나타나고, 매칭할 때마다 바가 차오름
///   - 가득 차서 발동하면 잠깐 가득 찬 상태를 보여준 뒤 다시 비워지고 숨겨짐
///
/// 항상 켜져 있는 오브젝트에 붙이세요. (게이지 오브젝트 자체에 붙이면 꺼진 동안 동작하지 못합니다)
/// </summary>
public class SkillManager : MonoBehaviour
{
    [System.Serializable]
    public class SkillGauge
    {
        [Tooltip("아이콘 + 바를 묶은 부모 오브젝트. 스크립트가 켜고 끕니다.")]
        public GameObject root;

        [Tooltip("차오르는 바. Image Type을 Filled (Horizontal)로 설정하세요.")]
        public Image fillImage;

        [Tooltip("\"2/5\"처럼 횟수를 보여줄 텍스트 (선택)")]
        public TMP_Text countText;

        [System.NonSerialized] public float shownFill;   // 화면에 보이는 채움 정도(부드럽게 따라감)
        [System.NonSerialized] public float flashUntil;  // 이 시각까지는 "가득 참" 상태로 표시
    }

    [Header("참조")]
    public HexGridManager gridManager;
    public GameManager gameManager;
    public LauncherController launcher;

    [Header("레인보우 스킬 - 타이머 완화")]
    [Tooltip("레인보우 매칭을 몇 번 하면 발동할지")]
    public int rainbowMatchesRequired = 5;

    [Tooltip("타이머 기준 레벨을 얼마로 낮출지. 0.5면 절반(레벨 10 → 레벨 5의 속도)")]
    [Range(0.1f, 1f)] public float timerLevelRatio = 0.5f;

    public SkillGauge rainbowGauge;

    [Tooltip("발동 시 재생할 효과음 (선택)")]
    public AudioClip rainbowSkillClip;

    [Header("회색 스킬 - 화면 정리")]
    [Tooltip("회색 매칭을 몇 번 하면 발동할지")]
    public int grayMatchesRequired = 5;

    [Tooltip("맨 위에서 몇 줄을 남길지")]
    public int keepTopRows = 1;

    [Tooltip("윗줄부터 차례로 터지는 연출의 줄 간격(초). 0이면 한꺼번에 터집니다.")]
    public float popWaveRowDelay = 0.04f;

    [Tooltip("스킬로 터지는 풍선마다 나올 전용 이펙트 프리팹. " +
             "넣으면 일반 회색 매칭 이펙트 대신 이 프리팹이 쓰입니다. 비워두면 아래 설정을 따릅니다.")]
    public GameObject graySkillPopEffectPrefab;

    [Tooltip("(전용 이펙트를 비워뒀을 때만 적용) 체크하면 풍선 색과 상관없이 전부 회색 매칭 이펙트로 터집니다. " +
             "끄면 각 풍선의 색에 맞는 이펙트가 나옵니다.")]
    public bool useElectricEffectForAll = true;

    public SkillGauge grayGauge;

    [Tooltip("발동 시 재생할 효과음 (선택)")]
    public AudioClip graySkillClip;

    [Header("발동 연출 - 배너 + 잠깐 멈춤")]
    [Tooltip("레인보우 스킬 발동 시 화면에 띄울 배너 오브젝트 (평소엔 비활성화 상태로 두세요)")]
    public GameObject rainbowBanner;

    [Tooltip("회색 스킬 발동 시 화면에 띄울 배너 오브젝트 (평소엔 비활성화 상태로 두세요)")]
    public GameObject grayBanner;

    [Tooltip("배너가 화면에 떠 있는 시간(초). 멈춤 시간보다 길어도 됩니다.")]
    public float bannerDuration = 1f;

    [Tooltip("스킬 발동 시 게임을 멈추는 시간(초). 0이면 멈추지 않습니다.")]
    public float pauseDuration = 0.5f;

    [Header("게이지 표시")]
    [Tooltip("횟수 표시 형식. {0}=현재, {1}=필요 횟수")]
    public string countFormat = "{0}/{1}";

    [Tooltip("바가 차오르는 속도 (1초에 전체의 몇 배만큼)")]
    public float fillSpeed = 4f;

    [Tooltip("발동했을 때 가득 찬 상태를 보여주는 시간(초)")]
    public float fullHoldTime = 0.6f;

    [Tooltip("체크하면 횟수가 0일 때 게이지를 숨깁니다(첫 매칭 때 나타남). 끄면 항상 보입니다.")]
    public bool hideGaugeWhenEmpty = true;

    /// <summary>레인보우 스킬이 발동했을 때 호출됩니다.</summary>
    public System.Action OnRainbowSkillActivated;

    /// <summary>회색 스킬이 발동했을 때 호출됩니다. 인자: 터진 풍선 수</summary>
    public System.Action<int> OnGraySkillActivated;

    private int rainbowCount;
    private int grayCount;
    private bool graySkillPending; // 회색 스킬이 발동 대기 중(연쇄가 끝나면 실행)

    private int pauseDepth;          // 멈춤이 겹쳤을 때를 대비한 카운터
    private float savedTimeScale = 1f;
    private Coroutine rainbowBannerRoutine;
    private Coroutine grayBannerRoutine;

    void Awake()
    {
        if (rainbowBanner != null) rainbowBanner.SetActive(false);
        if (grayBanner != null) grayBanner.SetActive(false);
    }

    void OnEnable()
    {
        if (gridManager != null)
        {
            gridManager.OnMatchPopped += HandleMatchPopped;
            gridManager.OnPlacementSettled += HandlePlacementSettled;
        }

        RefreshGauge(rainbowGauge, rainbowCount, rainbowMatchesRequired, true);
        RefreshGauge(grayGauge, grayCount, grayMatchesRequired, true);
    }

    void OnDisable()
    {
        if (gridManager != null)
        {
            gridManager.OnMatchPopped -= HandleMatchPopped;
            gridManager.OnPlacementSettled -= HandlePlacementSettled;
        }

        // 멈춘 상태에서 씬이 바뀌거나 꺼지면 게임이 계속 멈춰 있게 되므로 반드시 되돌립니다.
        // (Time.timeScale은 씬을 다시 불러와도 유지되는 값입니다)
        if (pauseDepth > 0)
        {
            pauseDepth = 0;
            Time.timeScale = savedTimeScale;
            if (launcher != null) launcher.SetInputPaused(false);
        }
    }

    void Update()
    {
        RefreshGauge(rainbowGauge, rainbowCount, rainbowMatchesRequired, false);
        RefreshGauge(grayGauge, grayCount, grayMatchesRequired, false);
    }

    // ───────────── 매칭 집계 ─────────────

    private void HandleMatchPopped(BalloonColor color, int count)
    {
        if (gameManager != null && gameManager.IsGameOver) return;

        if (color == BalloonColor.Rainbow)
        {
            rainbowCount++;
            if (rainbowCount >= Mathf.Max(1, rainbowMatchesRequired))
            {
                rainbowCount = 0;
                ActivateRainbowSkill();
            }
        }
        else if (color == BalloonColor.Gray)
        {
            grayCount++;
            if (grayCount >= Mathf.Max(1, grayMatchesRequired))
            {
                grayCount = 0;
                graySkillPending = true; // 실제 실행은 연쇄가 다 끝난 뒤(HandlePlacementSettled)
                if (grayGauge != null) grayGauge.flashUntil = float.PositiveInfinity; // 발동할 때까지 가득 찬 상태 유지
            }
        }
    }

    // ───────────── 레인보우 스킬 ─────────────

    private void ActivateRainbowSkill()
    {
        if (rainbowGauge != null)
            rainbowGauge.flashUntil = Time.unscaledTime + pauseDuration + fullHoldTime;

        if (launcher != null)
        {
            launcher.ApplyHoldTimerRelief(timerLevelRatio);
            Debug.Log($"[Skill] 레인보우 스킬 발동: 타이머 기준 레벨 {launcher.EffectiveHoldLevel} (현재 레벨 {launcher.currentLevel})");
        }

        PlaySkillClip(rainbowSkillClip);
        ShowBanner(rainbowBanner, ref rainbowBannerRoutine);
        OnRainbowSkillActivated?.Invoke();

        // 잠깐 멈춤: 진행 중이던 연쇄/슬라이드/이펙트가 그 자리에서 멈췄다가 이어집니다.
        if (pauseDuration > 0f) StartCoroutine(PauseRoutine(pauseDuration));
    }

    // ───────────── 회색 스킬 ─────────────

    private void HandlePlacementSettled()
    {
        if (!graySkillPending) return;
        graySkillPending = false;

        if (gameManager != null && gameManager.IsGameOver) return;

        if (grayGauge != null)
            grayGauge.flashUntil = Time.unscaledTime + pauseDuration + fullHoldTime;

        PlaySkillClip(graySkillClip);
        ShowBanner(grayBanner, ref grayBannerRoutine);

        if (pauseDuration > 0f)
            StartCoroutine(GraySkillAfterPause());
        else
            PopForGraySkill();
    }

    /// <summary>배너를 띄우고 잠깐 멈췄다가, 멈춤이 풀리는 순간 화면 정리를 시작합니다.</summary>
    private IEnumerator GraySkillAfterPause()
    {
        yield return PauseRoutine(pauseDuration);

        if (gameManager != null && gameManager.IsGameOver) yield break;
        PopForGraySkill();
    }

    private void PopForGraySkill()
    {
        if (gridManager == null) return;

        int popped = gridManager.PopAllBelowTopRows(
            Mathf.Max(0, keepTopRows), popWaveRowDelay, useElectricEffectForAll, BalloonColor.Gray,
            graySkillPopEffectPrefab);
        Debug.Log($"[Skill] 회색 스킬 발동: {popped}개 터짐");

        // 스킬로 터진 풍선은 점수만 주고, 레벨업 진행도에는 넣지 않습니다
        // (수십 개가 한 번에 터져 레벨이 여러 단계 뛰는 것을 방지)
        if (gameManager != null)
            gameManager.AddSkillPops(popped, false);

        OnGraySkillActivated?.Invoke(popped);
    }

    // ───────────── 배너 + 잠깐 멈춤 ─────────────

    private void ShowBanner(GameObject banner, ref Coroutine routine)
    {
        if (banner == null) return;

        if (routine != null) StopCoroutine(routine);
        banner.SetActive(true);
        routine = StartCoroutine(HideBannerAfter(banner, bannerDuration));
    }

    private IEnumerator HideBannerAfter(GameObject banner, float seconds)
    {
        // 게임이 멈춰 있어도 흐르는 "실제 시간" 기준으로 기다립니다.
        yield return new WaitForSecondsRealtime(seconds);
        if (banner != null) banner.SetActive(false);
    }

    private IEnumerator PauseRoutine(float seconds)
    {
        BeginPause();
        yield return new WaitForSecondsRealtime(seconds);
        EndPause();
    }

    private void BeginPause()
    {
        if (pauseDepth++ > 0) return; // 이미 멈춰 있으면 그대로

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // timeScale만 0으로 하면 터치 입력은 계속 들어와서 멈춘 도중에 발사가 될 수 있으므로,
        // 발사대 입력도 함께 막습니다.
        if (launcher != null) launcher.SetInputPaused(true);
    }

    private void EndPause()
    {
        if (pauseDepth <= 0) return;
        if (--pauseDepth > 0) return;

        Time.timeScale = savedTimeScale;
        if (launcher != null) launcher.SetInputPaused(false);
    }

    // ───────────── 게이지 표시 ─────────────

    private void RefreshGauge(SkillGauge gauge, int count, int required, bool instant)
    {
        if (gauge == null) return;

        required = Mathf.Max(1, required);
        // 게임이 잠깐 멈춘 동안에도 게이지는 움직여야 하므로 "실제 시간"(unscaled)을 씁니다.
        bool flashing = Time.unscaledTime < gauge.flashUntil;   // 발동 직후: 가득 찬 상태로 잠깐 보여줌
        float target = flashing ? 1f : (float)count / required;
        bool visible = flashing || count > 0 || !hideGaugeWhenEmpty;

        if (gauge.root != null && gauge.root.activeSelf != visible)
            gauge.root.SetActive(visible);

        if (instant || target < gauge.shownFill)
            gauge.shownFill = target;                            // 비워질 때는 바로
        else
            gauge.shownFill = Mathf.MoveTowards(gauge.shownFill, target, fillSpeed * Time.unscaledDeltaTime); // 찰 때는 부드럽게

        if (gauge.fillImage != null)
            gauge.fillImage.fillAmount = gauge.shownFill;

        if (gauge.countText != null)
            gauge.countText.text = string.Format(countFormat, flashing ? required : count, required);
    }

    private void PlaySkillClip(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayClip(clip);
    }

    // ───────────── 테스트용 (Play 중 컴포넌트 ⋮ 메뉴에서 실행) ─────────────

    [ContextMenu("TEST: 레인보우 매칭 +1")]
    private void TestRainbowMatch()
    {
        HandleMatchPopped(BalloonColor.Rainbow, 3);
    }

    [ContextMenu("TEST: 회색 매칭 +1")]
    private void TestGrayMatch()
    {
        HandleMatchPopped(BalloonColor.Gray, 3);
        HandlePlacementSettled(); // 테스트에서는 연쇄를 기다리지 않고 바로 실행
    }
}