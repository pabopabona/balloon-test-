using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

/// <summary>
/// 게임오버가 발생하면, 시작 화면에서 미리 저장해둔 닉네임으로 자동으로 점수를 제출하고
/// 순위표를 보여주는 UI 스크립트. (닉네임은 이미 게임 시작 시 LoadingScreenController에서
/// 입력받아 저장해뒀으므로, 게임오버 시점에 다시 물어보지 않습니다.)
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LeaderboardManager leaderboardManager;

    [Header("순위표 UI")]
    public GameObject leaderboardPanel;

    [Tooltip("상위 순위를 표시할 텍스트 줄들 (1등부터 순서대로)")]
    public TMP_Text[] rankTexts;

    [Tooltip("내가 상위 목록 밖(예: 20위)일 때, 목록 바로 아래에 따로 보여줄 줄")]
    public TMP_Text selfRankText;

    public string emptySlotText = "-";

    [Header("강조 색상 (그라디언트 미사용 시 폴백)")]
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    [Header("강조 스타일 - 그라디언트 (SelfRankText와 동일한 느낌)")]
    public bool useGradientForHighlight = true;
    public Color highlightGradientTop = Color.white;
    public Color highlightGradientBottom = new Color(0.99f, 0.525f, 0f); // #FD8600

    [Header("이름 표시 제한")]
    public int maxDisplayNameLength = 8;

    [Header("재시작 버튼 연동")]
    public GameObject restartButtonObject;

    // 시작 화면(LoadingScreenController)에서 저장해두는 닉네임과 동일한 키를 사용합니다.
    private const string LastNicknamePrefsKey = "last_nickname";

    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnGameOver += HandleGameOver;

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (restartButtonObject != null) restartButtonObject.SetActive(false);
        if (selfRankText != null) selfRankText.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver(string reason)
    {
        if (restartButtonObject != null)
            restartButtonObject.SetActive(false);

        // 이벤트 핸들러는 async로 만들 수 없으니, 별도의 async 메서드를 fire-and-forget으로 호출
        _ = SubmitScoreAndShowLeaderboardAsync();
    }

    /// <summary>
    /// 저장된 닉네임으로 자동 제출하고, 순위표까지 보여준 뒤 재시작 버튼을 활성화합니다.
    /// </summary>
    private async Task SubmitScoreAndShowLeaderboardAsync()
    {
        string playerName = PlayerPrefs.GetString(LastNicknamePrefsKey, "Player");
        int finalScore = gameManager != null ? gameManager.Score : 0;
        int finalLevel = (gameManager != null && gameManager.launcher != null) ? gameManager.launcher.currentLevel : 1;

        if (leaderboardManager != null)
        {
            await leaderboardManager.SubmitScoreAsync(playerName, finalScore, finalLevel);
        }

        await ShowLeaderboardAsync();

        if (restartButtonObject != null)
            restartButtonObject.SetActive(true);
    }

    /// <summary>
    /// 서버에서 순위 데이터를 전부 받아온 뒤에야 패널을 열고 텍스트를 채웁니다.
    /// </summary>
    public async Task ShowLeaderboardAsync()
    {
        if (leaderboardManager == null || rankTexts == null) return;

        Task<List<ScoreEntry>> entriesTask = leaderboardManager.LoadEntriesAsync();
        Task<ScoreEntry> myEntryTask = leaderboardManager.GetMyEntryAsync();
        await Task.WhenAll(entriesTask, myEntryTask);

        List<ScoreEntry> entries = entriesTask.Result;
        ScoreEntry myEntry = myEntryTask.Result;
        string myPlayerId = leaderboardManager.CurrentPlayerId;

        bool foundSelfInTop = false;

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] == null) continue;

            if (i < entries.Count)
            {
                ScoreEntry e = entries[i];
                bool isSelf = !string.IsNullOrEmpty(myPlayerId) && e.playerId == myPlayerId;

                rankTexts[i].text = $"{e.rank}. {TruncateName(e.playerName)} - Lv.{e.level} [{e.score}]";
                ApplyHighlightStyle(rankTexts[i], isSelf);

                if (isSelf) foundSelfInTop = true;
            }
            else
            {
                rankTexts[i].text = $"{i + 1}. {emptySlotText}";
                ApplyHighlightStyle(rankTexts[i], false);
            }
        }

        if (selfRankText != null)
        {
            if (!foundSelfInTop && myEntry != null)
            {
                selfRankText.text = $"{myEntry.rank}. {TruncateName(myEntry.playerName)} - Lv.{myEntry.level} [{myEntry.score}]";
                ApplyHighlightStyle(selfRankText, true);
                selfRankText.gameObject.SetActive(true);
            }
            else
            {
                selfRankText.gameObject.SetActive(false);
            }
        }

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);
    }

    public void CloseLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    private void ApplyHighlightStyle(TMP_Text text, bool isHighlighted)
    {
        if (text == null) return;

        if (isHighlighted && useGradientForHighlight)
        {
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(
                highlightGradientTop, highlightGradientTop,
                highlightGradientBottom, highlightGradientBottom);
        }
        else if (isHighlighted)
        {
            text.enableVertexGradient = false;
            text.color = highlightColor;
        }
        else
        {
            text.enableVertexGradient = false;
            text.color = normalColor;
        }
    }

    private string TruncateName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length <= maxDisplayNameLength) return name;
        return name.Substring(0, maxDisplayNameLength) + "…";
    }
}