using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임오버가 발생하면, 시작 화면에서 미리 저장해둔 닉네임으로 자동으로 점수를 제출하고
/// 순위표를 보여주는 UI 스크립트. (닉네임은 이미 게임 시작 시 LoadingScreenController에서
/// 입력받아 저장해뒀으므로, 게임오버 시점에 다시 물어보지 않습니다.)
///
/// 띄우는 타이밍
///   - 점수 제출과 순위 조회는 게임오버 즉시 시작합니다. (기다리는 동안 미리 받아둠)
///   - 순위표는 GameOverUI가 게임오버 화면을 띄운 뒤, Show Delay(초)만큼 더 지나서 뜹니다.
///     (GameOverUI를 연결하지 않으면 게임오버 순간부터 셉니다.)
///   - 서버 응답이 그보다 늦으면, 응답이 오는 즉시 뜹니다.
///   - 순위표 패널에 Canvas Group이 있으면 Fade Duration 동안 서서히 나타납니다.
///
/// 순위 줄 수
///   - LeaderboardManager의 Max Entries 값만큼 줄을 자동으로 만듭니다. (10 → 15 → 30 자유롭게)
///   - Row Prefab(줄 하나)을 Row Parent(스크롤 목록의 Content) 아래에 필요한 만큼 복제합니다.
///   - 내 순위가 목록 안에 있으면 그 줄로 자동 스크롤합니다.
///
/// 내 줄 (강조)
///   - Self Row Prefab을 넣으면 내 줄은 그 프리팹으로 보여줍니다. (목록 안이든 밖이든)
///     강조 색, 글꼴, 배경, 아이콘 등은 프리팹에서 직접 꾸미면 되고, 스크립트는 색을 건드리지 않습니다.
///   - 목록 밖(예: 20위)이면 Self Row Outside Parent 아래에 내 줄을 따로 보여줍니다.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LeaderboardManager leaderboardManager;

    [Tooltip("연결하면 게임오버 화면이 뜬 순간부터 Show Delay를 셉니다. (GameOverUI가 붙은 GameOver 오브젝트)")]
    public GameOverUI gameOverUI;

    [Header("순위표 UI")]
    public GameObject leaderboardPanel;

    [Header("순위 목록 (줄 자동 생성)")]
    [Tooltip("순위 한 줄 프리팹. 안에 TMP Text가 하나 있어야 합니다.")]
    public GameObject rowPrefab;

    [Tooltip("줄을 생성할 부모. 스크롤 목록(Scroll View)의 Viewport > Content를 넣으세요.")]
    public RectTransform rowParent;

    [Tooltip("스크롤 목록 (Scroll View의 Scroll Rect). 넣으면 내 순위 줄로 자동 스크롤합니다.")]
    public ScrollRect scrollRect;

    [Tooltip("체크하면 순위 기록이 Max Entries보다 적을 때 남은 줄을 \"-\"로 채웁니다.")]
    public bool showEmptySlots = true;

    [Header("내 줄 (강조 프리팹)")]
    [Tooltip("내 순위 줄 전용 프리팹. 안에 TMP Text가 하나 있어야 합니다. " +
             "목록 안에서는 내 자리의 일반 줄 대신, 목록 밖이면 아래 Self Row Outside Parent에 생성됩니다.")]
    public GameObject selfRowPrefab;

    [Tooltip("내가 목록 밖(예: 20위)일 때 내 줄을 생성할 부모. Scroll View 바로 아래에 둔 빈 오브젝트를 넣으세요.")]
    public RectTransform selfRowOutsideParent;

    [Tooltip("체크하면 내 순위가 목록 안에 있을 때 Self Row Outside Parent(아래 자리)를 숨깁니다. " +
             "LeaderboardPanel에 Vertical Layout Group을 쓰면 그만큼 스크롤 목록이 늘어나 빈 공간이 생기지 않습니다.")]
    public bool hideSlotWhenInList = true;

    public string emptySlotText = "-";

    [Header("띄우는 타이밍")]
    [Tooltip("게임오버 화면이 뜬 뒤 순위표가 뜨기까지 더 기다리는 시간(초). 0이면 게임오버 화면과 같이 뜹니다.")]
    public float showDelay = 1.5f;

    [Tooltip("순위표가 서서히 나타나는 시간(초). 순위표 패널에 Canvas Group이 있을 때만 적용됩니다. 0이면 한 번에 나타납니다.")]
    public float fadeDuration = 0.3f;

    [Header("이름 표시 제한")]
    public int maxDisplayNameLength = 8;

    [Header("재시작 버튼 연동")]
    [Tooltip("순위표가 뜬 뒤에 나타날 재시작 버튼")]
    public GameObject restartButtonObject;

    // 시작 화면(LoadingScreenController)에서 저장해두는 닉네임과 동일한 키를 사용합니다.
    private const string LastNicknamePrefsKey = "last_nickname";

    private bool gameOverPanelShown;
    private float gameOverPanelShownTime;
    private bool gameOverHandled;

    private readonly List<TMP_Text> rows = new List<TMP_Text>();
    private RectTransform selfRow; // 목록 안의 내 줄 (자동 스크롤용)

    private TMP_Text selfRowInList;     // 목록 안에 끼워 넣는 내 줄 (Self Row Prefab으로 생성)
    private TMP_Text selfRowOutside;    // 목록 밖에 따로 보여주는 내 줄 (Self Row Prefab으로 생성)


    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnGameOver += HandleGameOver;
        if (gameOverUI != null)
            gameOverUI.OnPanelShown += HandleGameOverPanelShown;

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (restartButtonObject != null) restartButtonObject.SetActive(false);
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;
        if (gameOverUI != null)
            gameOverUI.OnPanelShown -= HandleGameOverPanelShown;
    }

    private void HandleGameOver(string reason)
    {
        if (gameOverHandled) return; // 게임오버가 두 번 들어와도 점수는 한 번만 제출
        gameOverHandled = true;

        if (restartButtonObject != null)
            restartButtonObject.SetActive(false);

        // GameOverUI를 쓰지 않으면 게임오버 순간부터 Show Delay를 셉니다.
        if (gameOverUI == null) MarkPanelShown();

        // 이벤트 핸들러는 async로 만들 수 없으니, 별도의 async 메서드를 fire-and-forget으로 호출
        _ = SubmitScoreAndShowLeaderboardAsync();
    }

    private void HandleGameOverPanelShown() => MarkPanelShown();

    private void MarkPanelShown()
    {
        if (gameOverPanelShown) return;
        gameOverPanelShown = true;
        gameOverPanelShownTime = Time.realtimeSinceStartup; // 게임이 멈춰 있어도 흐르는 실제 시간
    }

    /// <summary>
    /// 저장된 닉네임으로 자동 제출하고, 순위표를 정해진 타이밍에 보여준 뒤 재시작 버튼을 활성화합니다.
    /// </summary>
    private async Task SubmitScoreAndShowLeaderboardAsync()
    {
        string playerName = PlayerPrefs.GetString(LastNicknamePrefsKey, "Player");
        int finalScore = gameManager != null ? gameManager.Score : 0;
        int finalLevel = (gameManager != null && gameManager.launcher != null) ? gameManager.launcher.currentLevel : 1;

        // 1) 기다리는 동안 미리 제출하고 순위를 받아서 텍스트를 채워둡니다. (패널은 아직 꺼진 상태)
        if (leaderboardManager != null)
            await leaderboardManager.SubmitScoreAsync(playerName, finalScore, finalLevel);

        if (this == null) return; // 기다리는 사이 씬이 바뀐 경우
        await FillLeaderboardAsync();

        // 2) 게임오버 화면이 뜬 뒤 Show Delay만큼 지날 때까지 기다림
        while (this != null && !gameOverPanelShown)
            await Task.Yield();
        while (this != null && Time.realtimeSinceStartup < gameOverPanelShownTime + showDelay)
            await Task.Yield();

        if (this == null) return;

        // 3) 순위표를 띄우고, 다 나타나면 재시작 버튼을 보여줌
        OpenPanel(true);
    }

    /// <summary>
    /// 서버에서 순위 데이터를 받아와 바로 패널을 엽니다. (다른 곳에서 순위표를 바로 열 때 사용)
    /// </summary>
    public async Task ShowLeaderboardAsync()
    {
        await FillLeaderboardAsync();
        if (this == null) return;
        OpenPanel(false);
    }

    /// <summary>
    /// 서버에서 순위 데이터를 전부 받아온 뒤 텍스트를 채웁니다. (패널은 열지 않음)
    /// </summary>
    private async Task FillLeaderboardAsync()
    {
        if (leaderboardManager == null) return;

        Task<List<ScoreEntry>> entriesTask = leaderboardManager.LoadEntriesAsync();
        Task<ScoreEntry> myEntryTask = leaderboardManager.GetMyEntryAsync();
        await Task.WhenAll(entriesTask, myEntryTask);

        if (this == null) return;

        List<ScoreEntry> entries = entriesTask.Result;
        ScoreEntry myEntry = myEntryTask.Result;
        string myPlayerId = leaderboardManager.CurrentPlayerId;

        List<TMP_Text> lines = PrepareRows(entries.Count);
        selfRow = null;
        int selfIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            TMP_Text line = lines[i];
            if (line == null) continue;

            if (i < entries.Count)
            {
                ScoreEntry e = entries[i];
                bool isSelf = !string.IsNullOrEmpty(myPlayerId) && e.playerId == myPlayerId;

                line.text = FormatEntry(e);

                if (isSelf && selfIndex < 0)
                {
                    selfIndex = i;
                    selfRow = GetRowRoot(line);
                }
            }
            else
            {
                line.text = $"{i + 1}. {emptySlotText}";
            }
        }

        bool foundSelfInTop = selfIndex >= 0;

        // ── 목록 안의 내 줄: 그 자리의 일반 줄을 숨기고 내 줄 프리팹을 끼워 넣음
        if (foundSelfInTop && selfRowPrefab != null && rowParent != null)
        {
            RectTransform normalRoot = GetRowRoot(lines[selfIndex]);

            if (selfRowInList == null)
                selfRowInList = CreateSelfRow(rowParent, "Row_Self");

            if (selfRowInList != null)
            {
                RectTransform selfRoot = GetRowRoot(selfRowInList);
                selfRowInList.text = lines[selfIndex].text;
                selfRoot.gameObject.SetActive(true);
                selfRoot.SetSiblingIndex(normalRoot.GetSiblingIndex()); // 같은 순서 자리로
                normalRoot.gameObject.SetActive(false);
                selfRow = selfRoot;
            }
        }
        else if (selfRowInList != null)
        {
            GetRowRoot(selfRowInList).gameObject.SetActive(false);
        }

        // ── 목록 밖의 내 줄
        bool showOutside = !foundSelfInTop && myEntry != null;

        if (selfRowPrefab != null && selfRowOutsideParent != null)
        {
            if (showOutside && selfRowOutside == null)
                selfRowOutside = CreateSelfRow(selfRowOutsideParent, "SelfRow_Outside");

            if (selfRowOutside != null)
            {
                if (showOutside) selfRowOutside.text = FormatEntry(myEntry);
                GetOutsideRoot().gameObject.SetActive(showOutside);
            }

            // 내 줄이 목록 안에 있으면 아래 자리(슬롯) 자체를 숨깁니다.
            // LeaderboardPanel에 Vertical Layout Group을 쓰면, 비워진 만큼 스크롤 목록이 자동으로 늘어납니다.
            if (hideSlotWhenInList)
                selfRowOutsideParent.gameObject.SetActive(showOutside);
        }
    }

    /// <summary>Self Row Prefab을 parent 아래에 하나 만들고 안의 TMP Text를 돌려줍니다.</summary>
    private TMP_Text CreateSelfRow(RectTransform parent, string objectName)
    {
        GameObject go = Instantiate(selfRowPrefab, parent, false);
        go.name = objectName;
        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            Debug.LogWarning("[LeaderboardUI] Self Row Prefab 안에 TMP Text가 없습니다.");
        return text;
    }

    /// <summary>목록 밖 내 줄의 최상위 오브젝트 (Self Row Outside Parent 바로 아래).</summary>
    private Transform GetOutsideRoot()
    {
        Transform t = selfRowOutside.transform;
        while (t.parent != null && t.parent != selfRowOutsideParent)
            t = t.parent;
        return t;
    }

    private string FormatEntry(ScoreEntry e)
    {
        return $"{e.rank}. {TruncateName(e.playerName)} - Lv.{e.level} [{e.score}]";
    }

    /// <summary>
    /// 이번에 보여줄 줄 목록을 준비합니다.
    /// Max Entries 만큼 줄을 만들거나(모자라면 추가) 숨깁니다(남으면).
    /// </summary>
    private List<TMP_Text> PrepareRows(int entryCount)
    {
        if (rowPrefab == null || rowParent == null)
        {
            Debug.LogWarning("[LeaderboardUI] Row Prefab 또는 Row Parent가 연결되지 않았습니다.");
            return new List<TMP_Text>();
        }

        int maxRows = Mathf.Max(1, leaderboardManager != null ? leaderboardManager.maxEntries : 10);
        int count = showEmptySlots ? maxRows : Mathf.Min(entryCount, maxRows);

        // 모자란 줄은 새로 만듦
        while (rows.Count < count)
        {
            GameObject go = Instantiate(rowPrefab, rowParent, false);
            go.name = $"Row_{rows.Count + 1}";
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                Debug.LogWarning("[LeaderboardUI] Row Prefab 안에 TMP Text가 없습니다.");
            rows.Add(text);
        }

        // 필요한 만큼만 켜고 나머지는 숨김
        List<TMP_Text> active = new List<TMP_Text>();
        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < count;
            if (rows[i] != null) GetRowRoot(rows[i]).gameObject.SetActive(on);
            if (on) active.Add(rows[i]);
        }
        return active;
    }

    /// <summary>텍스트가 속한 줄(Row Parent 바로 아래 오브젝트)을 찾습니다.</summary>
    private RectTransform GetRowRoot(TMP_Text text)
    {
        Transform t = text.transform;
        while (rowParent != null && t.parent != null && t.parent != rowParent)
            t = t.parent;
        return t as RectTransform;
    }

    /// <summary>내 줄이 목록 가운데쯤 보이도록 스크롤합니다. 내 줄이 없으면 맨 위(1등)로.</summary>
    private void ScrollToSelf()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        Canvas.ForceUpdateCanvases(); // 줄 배치(Layout)를 먼저 끝냄

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        float contentH = content.rect.height;
        float viewH = viewport.rect.height;
        if (selfRow == null || contentH <= viewH)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        // Content 맨 위에서 내 줄 가운데까지의 거리
        Vector3 local = selfRow.parent == content
            ? selfRow.localPosition + (Vector3)selfRow.rect.center
            : content.InverseTransformPoint(selfRow.TransformPoint(selfRow.rect.center));
        float fromTop = content.rect.yMax - local.y;

        float offset = fromTop - viewH * 0.5f;
        scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / (contentH - viewH));
    }

    private void OpenPanel(bool showRestartAfter)
    {
        if (leaderboardPanel == null)
        {
            if (showRestartAfter && restartButtonObject != null) restartButtonObject.SetActive(true);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(OpenPanelRoutine(showRestartAfter));
    }

    private IEnumerator OpenPanelRoutine(bool showRestartAfter)
    {
        CanvasGroup group = leaderboardPanel.GetComponent<CanvasGroup>();
        bool fade = group != null && fadeDuration > 0f;

        if (fade)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        leaderboardPanel.SetActive(true);

        // 줄 배치가 끝난 뒤 내 순위로 스크롤
        yield return null;
        ScrollToSelf();

        if (fade)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // 게임이 멈춰 있어도 진행
                group.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        if (showRestartAfter && restartButtonObject != null)
            restartButtonObject.SetActive(true);
    }

    public void CloseLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    private string TruncateName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length <= maxDisplayNameLength) return name;
        return name.Substring(0, maxDisplayNameLength) + "…";
    }
}