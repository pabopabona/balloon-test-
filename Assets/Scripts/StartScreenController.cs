using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 닉네임 입력이 끝난 뒤 보여주는 시작 화면.
/// - 현재 하트 수와 다음 충전까지 남은 시간을 보여줍니다.
/// - 시작 버튼: 하트 1개를 소모하고 게임을 시작합니다. 하트가 0개면 비활성화됩니다.
/// - 광고 버튼: (가짜) 보상형 광고를 보면 하트 1개를 받습니다. 하트가 가득 차 있으면 비활성화됩니다.
///
/// 이 스크립트는 항상 켜져 있는 오브젝트에 붙이세요. startPanel을 껐다 켜는 건 이 스크립트가
/// 코드로 하기 때문에, startPanel 자신에게 붙이면 꺼진 동안 동작하지 못합니다.
/// </summary>
public class StartScreenController : MonoBehaviour
{
    [Header("참조")]
    public HeartManager heartManager;
    public RewardedADService adService;

    [Tooltip("게임 시작 시 조작을 허용할 발사대. 시작 버튼을 누르기 전까지는 꺼져 있어야 합니다.")]
    public LauncherController launcher;

    [Header("UI")]
    [Tooltip("시작 화면 전체를 담는 패널 (평소엔 비활성화 상태로 두세요)")]
    public GameObject startPanel;

    [Tooltip("게임 중에만 보여줄 HUD 하트(메인 Canvas에 만든 것)의 최상위 오브젝트. " +
             "시작 화면이 떠 있는 동안에는 시작 화면의 하트와 겹쳐 보이지 않도록 숨기고, " +
             "시작 버튼을 누르면 다시 보여줍니다. HUD 하트를 안 쓰면 비워두세요.")]
    public GameObject hudHeartRoot;
    public TMP_Text heartCountText;
    public TMP_Text timerText;
    public Button startButton;
    public Button watchAdButton;

    [Header("표시 형식")]
    [Tooltip("하트 개수 표시. {0}=현재, {1}=최대")]
    public string heartCountFormat = "{0} / {1}";

    [Tooltip("남은 시간 표시. {0}=분, {1}=초")]
    public string timerFormat = "{0:00}:{1:00}";

    private bool adInProgress;
    private bool isShown;

    void Awake()
    {
        if (startPanel != null) startPanel.SetActive(false);

        // 게임이 시작되기 전(로딩/닉네임/시작 화면)에는 HUD 하트를 숨겨둡니다.
        if (hudHeartRoot != null) hudHeartRoot.SetActive(false);
    }

    void OnDisable()
    {
        if (heartManager != null)
            heartManager.OnHeartsChanged -= HandleHeartsChanged;
    }

    /// <summary>시작 화면을 엽니다. (닉네임 입력이 끝난 뒤 LoadingScreenController가 호출)</summary>
    public void Show()
    {
        isShown = true;
        if (startPanel != null) startPanel.SetActive(true);

        if (heartManager != null)
        {
            heartManager.OnHeartsChanged -= HandleHeartsChanged;
            heartManager.OnHeartsChanged += HandleHeartsChanged;
        }

        Refresh();
    }

    void Update()
    {
        if (!isShown) return;

        // 남은 시간 표시는 매 프레임 갱신 (하트가 가득 차면 숨김)
        RefreshTimer();

        // [AdMob 대응] 실제 광고는 네트워크로 받아오는 데 몇 초가 걸립니다.
        // 시작 화면이 열린 시점엔 아직 준비가 안 됐다가 나중에 준비되는 경우가 많아서,
        // 광고 버튼 상태를 매 프레임 다시 확인합니다. (가짜 광고는 항상 준비 상태라 이 문제가 없었음)
        RefreshAdButton();
    }

    private void HandleHeartsChanged(int hearts, int max)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (heartManager == null) return;

        int hearts = heartManager.Hearts;
        int max = heartManager.maxHearts;

        if (heartCountText != null)
            heartCountText.text = string.Format(heartCountFormat, hearts, max);

        if (startButton != null)
            startButton.interactable = hearts >= 1 && !adInProgress;

        RefreshAdButton();
        RefreshTimer();
    }

    private void RefreshAdButton()
    {
        if (watchAdButton == null || heartManager == null) return;

        bool adReady = adService != null && adService.IsReady;
        watchAdButton.interactable = !heartManager.IsFull && adReady && !adInProgress;
    }

    private void RefreshTimer()
    {
        if (timerText == null || heartManager == null) return;

        if (heartManager.IsFull)
        {
            timerText.gameObject.SetActive(false);
            return;
        }

        timerText.gameObject.SetActive(true);

        int totalSeconds = Mathf.CeilToInt(heartManager.SecondsUntilNextHeart);
        timerText.text = string.Format(timerFormat, totalSeconds / 60, totalSeconds % 60);
    }

    /// <summary>시작 버튼의 On Click()에 연결하세요.</summary>
    public void OnStartPressed()
    {
        if (heartManager == null || !heartManager.TrySpendHeart())
        {
            Refresh(); // 하트가 없으면 아무 일도 없이 화면만 갱신
            return;
        }

        isShown = false;
        heartManager.OnHeartsChanged -= HandleHeartsChanged;

        if (startPanel != null) startPanel.SetActive(false);

        // 게임이 시작됐으니 HUD 하트를 보여줌
        if (hudHeartRoot != null) hudHeartRoot.SetActive(true);

        // 이제서야 발사대 조작을 허용 = 게임 시작
        if (launcher != null) launcher.enabled = true;
    }

    /// <summary>광고 버튼의 On Click()에 연결하세요.</summary>
    public void OnWatchAdPressed()
    {
        if (adService == null || heartManager == null || heartManager.IsFull) return;

        adInProgress = true;
        Refresh();

        adService.Show(
            onRewarded: () =>
            {
                heartManager.AddHearts(1);
                adInProgress = false;
                Refresh();
            },
            onClosedWithoutReward: () =>
            {
                adInProgress = false;
                Refresh();
            });
    }
}