using UnityEngine;
using TMPro;

/// <summary>
/// 게임오버 시 이름 입력창을 띄우고, 제출하면 점수를 등록한 뒤 순위표를 보여주는 UI 스크립트.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LeaderboardManager leaderboardManager;

    [Header("이름 입력 UI")]
    [Tooltip("게임오버 시 나타날 이름 입력 패널 (평소엔 비활성화 상태로 두세요)")]
    public GameObject nameInputPanel;
    public TMP_InputField nameInputField;

    [Header("순위표 UI")]
    [Tooltip("순위표를 보여줄 패널 (평소엔 비활성화 상태로 두세요)")]
    public GameObject leaderboardPanel;

    [Tooltip("순위를 표시할 텍스트 줄들을 순서대로 등록하세요 (1등부터). " +
             "예: 10위까지 보여주려면 텍스트 오브젝트 10개를 미리 만들어서 순서대로 연결")]
    public TMP_Text[] rankTexts;

    [Tooltip("순위표에 데이터가 없는 줄은 이 텍스트로 표시합니다 (완전히 숨기지 않고 빈 자리로 보여줄 때)")]
    public string emptySlotText = "-";

    [Header("재시작 버튼 연동")]
    [Tooltip("Submit 하기 전까지는 숨겨뒀다가, 순위표가 뜰 때 같이 나타나게 할 재시작 버튼 오브젝트")]
    public GameObject restartButtonObject;

    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnGameOver += HandleGameOver;

        if (nameInputPanel != null) nameInputPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (restartButtonObject != null) restartButtonObject.SetActive(false);
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver(string reason)
    {
        if (nameInputField != null)
            nameInputField.text = "";

        if (nameInputPanel != null)
            nameInputPanel.SetActive(true);

        if (restartButtonObject != null)
            restartButtonObject.SetActive(false);
    }

    /// <summary>
    /// 이름 입력 후 "제출" 버튼의 On Click()에 연결하세요.
    /// </summary>
    public void OnSubmitScore()
    {
        string playerName = (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            ? nameInputField.text.Trim()
            : "Player";

        int finalScore = gameManager != null ? gameManager.Score : 0;

        if (leaderboardManager != null)
            leaderboardManager.SubmitScore(playerName, finalScore);

        if (nameInputPanel != null)
            nameInputPanel.SetActive(false);

        ShowLeaderboard();

        if (restartButtonObject != null)
            restartButtonObject.SetActive(true);
    }

    /// <summary>
    /// 순위표 패널을 열고 현재 저장된 순위를 채워 넣습니다.
    /// "순위표 보기" 버튼 등에 직접 연결해도 됩니다.
    /// </summary>
    public void ShowLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);

        if (leaderboardManager == null || rankTexts == null) return;

        var entries = leaderboardManager.LoadEntries();

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] == null) continue;

            if (i < entries.Count)
            {
                rankTexts[i].text = $"{i + 1}. {entries[i].playerName} - {entries[i].score}";
            }
            else
            {
                rankTexts[i].text = $"{i + 1}. {emptySlotText}";
            }
        }
    }

    /// <summary>
    /// 순위표 패널을 닫는 용도 (닫기 버튼 등에 연결).
    /// </summary>
    public void CloseLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }
}