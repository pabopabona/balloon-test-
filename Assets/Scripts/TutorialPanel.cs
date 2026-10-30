using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 화면 오른쪽 위 동그란 버튼(?)을 누르면 열리는 게임 설명(튜토리얼) 창.
/// 한 장씩 [제목 + 이미지 + 설명]을 보여주고, 이전/다음 버튼으로 넘깁니다.
///
/// - 창이 열려 있는 동안에는 게임이 멈춥니다 (풍선, 타이머, 발사대 조작, 플레이 시간 모두 정지).
/// - 닫으면 열기 전 상태로 돌아갑니다. (시작 화면에서 열었으면 시작 화면 그대로)
/// - 페이지 내용(제목/이미지/설명)은 인스펙터의 Pages 목록에서 바로 고칠 수 있습니다.
///
/// 항상 켜져 있는 오브젝트(예: Canvas)에 붙이세요. panelRoot는 이 스크립트가 켜고 끕니다.
/// </summary>
public class TutorialPanel : MonoBehaviour
{
    [System.Serializable]
    public class TutorialPage
    {
        public string title;
        public Sprite image;
        [TextArea(2, 5)] public string description;
    }

    [Header("열기 버튼 (화면 오른쪽 위 동그란 버튼)")]
    public Button openButton;

    [Header("튜토리얼 창")]
    [Tooltip("튜토리얼 창 전체 (평소엔 비활성화). 화면 전체를 덮는 반투명 배경을 맨 아래에 두면 뒤가 눌리지 않습니다.")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public Image pageImage;
    public TMP_Text descriptionText;

    [Tooltip("\"3 / 8\"처럼 몇 번째 장인지 보여줄 텍스트 (선택)")]
    public TMP_Text pageNumberText;

    public Button prevButton;
    public Button nextButton;
    public Button closeButton;

    [Header("게임 멈춤")]
    [Tooltip("창이 열려 있는 동안 조작을 막을 발사대")]
    public LauncherController launcher;

    [Tooltip("체크하면 창이 열려 있는 동안 게임 시간을 멈춥니다.")]
    public bool pauseGameWhileOpen = true;

    [Header("처음 실행 시 자동으로 보여주기 (선택)")]
    [Tooltip("체크하면 앱을 처음 설치하고 실행했을 때 한 번 자동으로 열립니다.")]
    public bool showOnFirstLaunch = false;

    [Header("페이지 내용")]
    public List<TutorialPage> pages = new List<TutorialPage>
    {
        new TutorialPage { title = "풍선 쏘기",
            description = "풍선마차를 좌우로 움직이며 풍선을 쏩니다.\n풍선을 쏘면 다음에 나올 풍선을 보여줍니다." },
        new TutorialPage { title = "풍선 터뜨리기",
            description = "같은 색, 같은 모양의 풍선 3개를 붙여서 터뜨리세요." },
        new TutorialPage { title = "레인보우 풍선",
            description = "특정 레벨부터 레인보우 풍선이 나옵니다.\n3개를 모아 터뜨리면 바로 옆에 붙어 있는 풍선까지 함께 터지고, 리셋타이머 스킬 게이지가 찹니다." },
        new TutorialPage { title = "리셋타이머 스킬",
            description = "리셋타이머 게이지가 가득 차면 자동으로 발동해 발사 타이머가 느려집니다.\n발동할 때마다 다음에 채워야 하는 횟수가 하나씩 늘어납니다." },
        new TutorialPage { title = "번개 풍선",
            description = "특정 레벨부터 번개 풍선이 나옵니다.\n3개를 모아 터뜨리면 번개 풍선이 있던 줄이 모두 터지고, 풍선 All Clear 스킬 게이지가 찹니다." },
        new TutorialPage { title = "풍선 All Clear 스킬",
            description = "All Clear 게이지가 가득 차면 자동으로 발동해 맨 윗줄을 뺀 모든 풍선이 터집니다.\n발동할 때마다 다음에 채워야 하는 횟수가 하나씩 늘어납니다." },
        new TutorialPage { title = "벽돌 풍선",
            description = "특정 레벨부터 벽돌 풍선이 나오며, 붙는 즉시 벽돌로 바뀝니다.\n벽돌은 3개가 붙어도 사라지지 않습니다." },
        new TutorialPage { title = "벽돌 깨기",
            description = "벽돌은 옆에서 레인보우 풍선이 터지거나, 번개 풍선이 터지면 사라집니다." },
    };

    private const string SeenKey = "tutorial_seen";

    private int currentIndex;
    private bool isOpen;
    private float savedTimeScale = 1f;
    private bool launcherWasEnabled;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (openButton != null) openButton.onClick.AddListener(Open);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    void Start()
    {
        if (showOnFirstLaunch && PlayerPrefs.GetInt(SeenKey, 0) == 0)
            StartCoroutine(OpenAfterSceneSetup());
    }

    /// <summary>
    /// 처음 실행 시 자동으로 열 때는 한 프레임 기다렸다가 엽니다.
    /// 바로 열면 LoadingScreenController가 발사대를 끄기 "전"의 상태(켜짐)를 기억해버려서,
    /// 튜토리얼을 닫는 순간 발사대가 켜지며 시작 버튼 없이 게임이 시작되는 문제가 생깁니다.
    /// </summary>
    private IEnumerator OpenAfterSceneSetup()
    {
        yield return null; // 모든 스크립트의 Start(발사대 끄기 포함)가 끝난 뒤
        Open();
    }

    void Update()
    {
        // 열려 있는 동안 다른 연출(스킬 멈춤 등)이 끝나면서 게임 시간을 되돌려도 계속 멈춰 있게 합니다.
        if (isOpen && pauseGameWhileOpen && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }

    void OnDisable()
    {
        // 창이 열린 채로 씬이 바뀌면 게임이 멈춘 상태로 남지 않도록 되돌립니다.
        if (isOpen) Close();
    }

    // ───────────── 열기 / 닫기 ─────────────

    public void Open()
    {
        if (pages == null || pages.Count == 0) return;

        if (!isOpen)
        {
            isOpen = true;

            if (pauseGameWhileOpen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            // 발사대는 화면 터치를 직접 읽기 때문에, 창을 넘기는 터치가 발사로 이어지지 않도록 잠시 끕니다.
            if (launcher != null)
            {
                launcherWasEnabled = launcher.enabled;
                launcher.enabled = false;
            }
        }

        currentIndex = 0;
        if (panelRoot != null) panelRoot.SetActive(true);
        ShowPage(currentIndex);

        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);

        if (pauseGameWhileOpen)
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;

        // 열기 전 상태로 되돌림 (시작 화면에서 열었으면 꺼진 상태 그대로, 게임 중이었으면 다시 켬)
        if (launcher != null)
            launcher.enabled = launcherWasEnabled;
    }

    // ───────────── 페이지 넘기기 ─────────────

    public void NextPage()
    {
        if (currentIndex < pages.Count - 1) ShowPage(++currentIndex);
    }

    public void PrevPage()
    {
        if (currentIndex > 0) ShowPage(--currentIndex);
    }

    private void ShowPage(int index)
    {
        TutorialPage page = pages[index];

        if (titleText != null) titleText.text = page.title;
        if (descriptionText != null) descriptionText.text = page.description;

        if (pageImage != null)
        {
            pageImage.sprite = page.image;
            pageImage.enabled = page.image != null; // 이미지가 없으면 빈 흰 사각형 대신 숨김
            pageImage.preserveAspect = true;
        }

        if (pageNumberText != null)
            pageNumberText.text = $"{index + 1} / {pages.Count}";

        // 첫 장에서는 이전 버튼, 마지막 장에서는 다음 버튼을 비활성화
        if (prevButton != null) prevButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < pages.Count - 1;
    }

    [ContextMenu("TEST: 처음 실행 상태로 되돌리기 (자동 열림 다시 보기)")]
    private void TestResetSeen()
    {
        PlayerPrefs.DeleteKey(SeenKey);
    }
}