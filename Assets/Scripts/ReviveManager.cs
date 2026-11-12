using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 광고 보고 이어하기.
///
/// 흐름
///   1. 풍선이 위험선에 닿으면 GameManager가 바로 게임오버하지 않고 이 스크립트에 물어봅니다.
///   2. 조건이 맞으면(레벨, 횟수, 광고 준비) 이어하기 창을 띄우고 카운트다운을 시작합니다.
///   3. [광고 보고 이어하기] → 광고를 끝까지 보면:
///        - 전기풍선 All Clear 스킬처럼 맨 윗줄만 남기고 모두 터뜨림 (배너, 이펙트, 효과음 재사용)
///        - 레벨, 점수, 진행도는 그대로. 터진 풍선은 레벨업 진행에 들어가지 않음
///        - 전기 스킬의 다음 필요 횟수도 늘어나지 않음
///        - 발사 타이머를 처음부터 다시 시작
///   4. [포기] / 시간 초과 / 광고를 끝까지 안 봄 → 진짜 게임오버
///      (게임오버 화면, 등급 문구, 최고 기록, 리더보드 제출은 이때 처리됩니다)
///
/// 항상 켜져 있는 오브젝트(예: GameManager)에 붙이세요. Panel Root는 이 스크립트가 켜고 끕니다.
/// </summary>
public class ReviveManager : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LauncherController launcher;
    public HexGridManager gridManager;

    [Tooltip("하트 광고와 같은 보상형 광고 서비스")]
    public RewardedADService adService;

    [Tooltip("전기 스킬 설정(남길 줄 수, 이펙트, 배너)을 그대로 가져올 SkillManager")]
    public SkillManager skillManager;

    [Header("조건")]
    [Tooltip("한 판에 이어하기를 몇 번까지 할 수 있는지")]
    public int maxRevivesPerGame = 1;

    [Tooltip("이 레벨 이상에서만 이어하기를 물어봅니다. (짧은 판은 그냥 다시 시작하는 게 편하므로)")]
    public int minLevelToOffer = 8;

    [Tooltip("체크하면 광고가 아직 준비되지 않았을 때는 묻지 않고 바로 게임오버합니다.")]
    public bool requireAdReady = true;

    [Header("이어하기 창")]
    [Tooltip("이어하기 창 전체 (평소엔 비활성화). 화면 전체를 덮는 반투명 배경을 맨 아래에 두면 뒤가 눌리지 않습니다.")]
    public GameObject panelRoot;

    public Button watchAdButton;

    [Tooltip("이어하지 않고 바로 게임오버로 가는 버튼 (선택)")]
    public Button giveUpButton;

    [Tooltip("남은 시간 숫자. 예: \"5\"")]
    public TMP_Text countdownText;

    [Tooltip("남은 시간 바 (Image Type: Filled). 1에서 0으로 줄어듭니다. (선택)")]
    public Image countdownFill;

    [Tooltip("몇 초 안에 선택하지 않으면 게임오버할지")]
    public float countdownSeconds = 5f;

    [Tooltip("남은 시간 표시 형식. {0} = 남은 초")]
    public string countdownFormat = "{0}";

    [Header("이어갈 때 화면 정리 (비워두면 전기 스킬 설정을 그대로 사용)")]
    [Tooltip("맨 위에서 몇 줄을 남길지. -1이면 SkillManager의 Keep Top Rows와 같은 값")]
    public int keepTopRows = -1;

    [Tooltip("터질 때 이펙트. 비워두면 SkillManager의 Gray Skill Pop Effect Prefab")]
    public GameObject popEffectPrefab;

    [Tooltip("화면 정리 때 띄울 배너. 비워두면 SkillManager의 Gray Banner(All Clear 배너)")]
    public GameObject reviveBanner;

    [Tooltip("이어갈 때 재생할 효과음. 비워두면 SkillManager의 Gray Skill Clip")]
    public AudioClip reviveClip;

    [Tooltip("배너가 뜬 뒤 화면 정리를 시작하기까지 기다리는 시간(초)")]
    public float clearDelay = 0.5f;

    [Tooltip("화면 정리가 끝난 뒤 조작을 다시 허용하기까지 기다리는 시간(초)")]
    public float resumeDelay = 0.4f;

    [Tooltip("배너가 떠 있는 시간(초)")]
    public float bannerDuration = 1f;

    [Tooltip("체크하면 이어하기로 터진 풍선도 점수에 넣습니다. (기본: 넣지 않음 → 리더보드 공정성)")]
    public bool giveScoreForClearedBalloons = false;

    private bool offerOpen;
    private bool adShowing;
    private float remaining;

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAdPressed);
        if (giveUpButton != null) giveUpButton.onClick.AddListener(OnGiveUpPressed);
    }

    // ───────────── GameManager가 호출 ─────────────

    /// <summary>
    /// 이어하기를 물어볼 수 있으면 창을 띄우고 true를 돌려줍니다.
    /// false면 GameManager가 바로 게임오버를 진행합니다.
    /// </summary>
    public bool TryOffer(GameManager gm)
    {
        if (gm == null) return false;
        if (gm.ReviveCount >= maxRevivesPerGame) return false;

        int level = launcher != null ? launcher.currentLevel : 1;
        if (level < minLevelToOffer) return false;

        if (adService == null) return false;
        if (requireAdReady && !adService.IsReady) return false;

        offerOpen = true;
        adShowing = false;
        remaining = Mathf.Max(0.1f, countdownSeconds);

        if (panelRoot != null) panelRoot.SetActive(true);
        if (watchAdButton != null) watchAdButton.interactable = true;
        if (giveUpButton != null) giveUpButton.interactable = true;
        RefreshCountdown();

        Debug.Log($"[Revive] 이어하기 제안 (Lv.{level})");
        return true;
    }

    void Update()
    {
        if (!offerOpen || adShowing) return; // 광고를 보는 동안에는 시간이 줄지 않음

        // 게임오버 상태라 게임 시간이 멈춰 있을 수 있으므로 실제 시간으로 셉니다.
        remaining -= Time.unscaledDeltaTime;
        RefreshCountdown();

        if (remaining <= 0f) Decline("시간 초과");
    }

    private void RefreshCountdown()
    {
        float r = Mathf.Max(0f, remaining);

        if (countdownText != null)
            countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(r));

        if (countdownFill != null)
            countdownFill.fillAmount = countdownSeconds > 0f ? r / countdownSeconds : 0f;
    }

    // ───────────── 버튼 ─────────────

    private void OnWatchAdPressed()
    {
        if (!offerOpen || adShowing) return;

        adShowing = true;
        if (watchAdButton != null) watchAdButton.interactable = false;
        if (giveUpButton != null) giveUpButton.interactable = false;

        adService.Show(
            onRewarded: () =>
            {
                adShowing = false;
                Accept();
            },
            onClosedWithoutReward: () =>
            {
                adShowing = false;
                Decline("광고를 끝까지 보지 않음");
            });
    }

    private void OnGiveUpPressed()
    {
        if (!offerOpen || adShowing) return;
        Decline("포기");
    }

    // ───────────── 결과 처리 ─────────────

    private void Decline(string why)
    {
        if (!offerOpen) return;
        offerOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        Debug.Log($"[Revive] 이어하지 않음: {why}");

        if (gameManager != null) gameManager.ConfirmGameOver();
    }

    private void Accept()
    {
        if (!offerOpen) return;
        offerOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        Debug.Log("[Revive] 광고 시청 완료 → 이어하기");

        if (gameManager != null) gameManager.Revive();
        StartCoroutine(ReviveRoutine());
    }

    private IEnumerator ReviveRoutine()
    {
        // 전기 스킬 설정을 기본값으로 사용
        int keep = keepTopRows >= 0 ? keepTopRows : (skillManager != null ? skillManager.keepTopRows : 1);
        GameObject effect = popEffectPrefab != null ? popEffectPrefab : (skillManager != null ? skillManager.graySkillPopEffectPrefab : null);
        GameObject banner = reviveBanner != null ? reviveBanner : (skillManager != null ? skillManager.grayBanner : null);
        AudioClip clip = reviveClip != null ? reviveClip : (skillManager != null ? skillManager.graySkillClip : null);
        float rowDelay = skillManager != null ? skillManager.popWaveRowDelay : 0.04f;
        bool singleEffect = skillManager == null || skillManager.useElectricEffectForAll;

        // 1) 배너 + 효과음
        if (banner != null) StartCoroutine(ShowBannerFor(banner, bannerDuration));
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlayClip(clip);

        if (clearDelay > 0f) yield return new WaitForSecondsRealtime(clearDelay);

        // 2) 맨 윗줄만 남기고 모두 터뜨림 (전기 스킬과 같은 연출)
        int popped = 0;
        if (gridManager != null)
            popped = gridManager.PopAllBelowTopRows(Mathf.Max(0, keep), rowDelay, singleEffect, BalloonColor.Gray, effect);

        // 레벨업 진행에는 넣지 않음. 점수는 설정에 따라.
        if (giveScoreForClearedBalloons && gameManager != null)
            gameManager.AddSkillPops(popped, false);

        Debug.Log($"[Revive] 화면 정리: {popped}개 터짐");

        // 3) 터지는 연출이 끝날 때까지 기다렸다가 조작 허용
        int rows = gridManager != null ? Mathf.Max(1, gridManager.maxRows) : 10;
        float waveTime = rowDelay * rows;
        yield return new WaitForSecondsRealtime(waveTime + Mathf.Max(0f, resumeDelay));

        if (gameManager != null && gameManager.IsGameOver) yield break;

        if (launcher != null)
        {
            launcher.enabled = true;
            launcher.ApplyHoldTimerRelief(1f); // 타이머 속도는 그대로, 진행 중이던 타이머만 0으로
        }
    }

    private IEnumerator ShowBannerFor(GameObject banner, float seconds)
    {
        banner.SetActive(true);
        yield return new WaitForSecondsRealtime(seconds);
        if (banner != null) banner.SetActive(false);
    }

    // ───────────── 테스트용 (Play 중 컴포넌트 ⋮ 메뉴) ─────────────

    [ContextMenu("TEST: 지금 게임오버 일으키기")]
    private void TestGameOver()
    {
        if (gameManager != null) gameManager.TriggerGameOver("test");
    }
}