using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 시작 직후, 화면 크기/격자 계산이 완전히 안정될 때까지 화면 전체를 가리는 로딩 스크린.
/// 준비가 끝나면 로딩 텍스트/스피너는 숨기고, 같은 자리에 닉네임 입력창을 띄워서
/// 플레이어가 미리 이름을 정해두게 합니다. Submit을 누르면 닉네임을 기기에 저장하고
/// (서버에는 아무것도 보내지 않음) 로딩 캔버스를 완전히 닫아 게임을 시작합니다.
///
/// 여기서 저장하는 닉네임은 게임오버 화면(LeaderboardUI)의 이름 입력창과 같은 키를
/// 사용하므로, 게임오버 때도 여기서 입력한 이름이 자동으로 미리 채워집니다.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [Header("참조")]
    public HexGridManager gridManager;

    [Tooltip("닉네임 Submit 전까지 조작을 막아둘 발사대. LauncherController는 화면 터치를 " +
             "UI 클릭 시스템을 거치지 않고 직접 읽기 때문에, 로딩/닉네임 화면이 떠 있어도 " +
             "그 아래에서 조작이 먼저 반응해버릴 수 있습니다. 이 컴포넌트를 꺼둬서 원천 차단합니다.")]
    public LauncherController launcher;

    [Tooltip("닉네임 입력이 끝난 뒤 보여줄 시작 화면(하트 확인 + 시작 버튼). " +
             "비워두면 예전처럼 닉네임 Submit 직후 바로 게임이 시작됩니다.")]
    public StartScreenController startScreen;

    [Header("로딩 화면 UI")]
    [Tooltip("로딩 캔버스 전체(닉네임 입력 포함)를 담는 오브젝트. 최종적으로 이게 꺼지면 게임이 드러남")]
    public GameObject loadingPanel;

    [Tooltip("\"Loading...\" 텍스트 + 스피너를 묶은 부모. 준비가 끝나면 이것만 숨김")]
    public GameObject loadingContent;

    [Header("닉네임 입력 (로딩 완료 후 표시)")]
    [Tooltip("닉네임 입력창 + Submit 버튼을 묶은 부모. 준비가 끝나면 이게 대신 나타남")]
    public GameObject nameEntryPanel;
    public TMP_InputField nicknameInputField;
    public Button startSubmitButton;

    [Header("설정")]
    [Tooltip("최소한 이 시간(초) 동안은 로딩 화면을 유지합니다.")]
    public float minimumDisplayTime = 0.5f;

    [Tooltip("혹시 모를 상황(무한 대기) 대비 안전장치")]
    public float maxWaitTime = 5f;

    private const string LastNicknamePrefsKey = "last_nickname";

    void Start()
    {
        // 게임오버 후 재시작이면 로딩 화면과 닉네임 입력을 건너뛰고 바로 시작 화면(하트 확인)으로 갑니다.
        // 닉네임이 저장되어 있지 않은 예외적인 경우에는 일반 흐름을 그대로 탑니다.
        if (SessionState.IsRestart && startScreen != null && PlayerPrefs.HasKey(LastNicknamePrefsKey))
        {
            SessionState.IsRestart = false;

            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (launcher != null) launcher.enabled = false; // 시작 버튼을 누르기 전까지는 조작 차단

            startScreen.Show();
            return;
        }

        SessionState.IsRestart = false;

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingContent != null) loadingContent.SetActive(true);
        if (nameEntryPanel != null) nameEntryPanel.SetActive(false);

        // 닉네임 Submit 전까지는 발사대 조작 자체를 막아둠
        if (launcher != null) launcher.enabled = false;

        StartCoroutine(WaitUntilReady());
    }

    private IEnumerator WaitUntilReady()
    {
        float elapsed = 0f;

        while (elapsed < maxWaitTime)
        {
            bool gridReady = gridManager == null || gridManager.IsReady;
            bool minTimePassed = elapsed >= minimumDisplayTime;

            if (gridReady && minTimePassed)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ShowNicknamePrompt();
    }

    private void ShowNicknamePrompt()
    {
        if (loadingContent != null)
            loadingContent.SetActive(false);

        if (nicknameInputField != null)
            nicknameInputField.text = PlayerPrefs.GetString(LastNicknamePrefsKey, "");

        if (nameEntryPanel != null)
            nameEntryPanel.SetActive(true);
    }

    /// <summary>
    /// 시작 화면의 Submit 버튼 On Click()에 연결하세요.
    /// 닉네임을 저장만 하고(서버 전송 없음), 로딩 캔버스를 완전히 닫아 게임을 시작합니다.
    /// </summary>
    public void OnStartSubmit()
    {
        string name = (nicknameInputField != null && !string.IsNullOrWhiteSpace(nicknameInputField.text))
            ? nicknameInputField.text.Trim()
            : "Player";

        PlayerPrefs.SetString(LastNicknamePrefsKey, name);
        PlayerPrefs.Save();

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (startScreen != null)
        {
            // 하트를 확인하고 "시작"을 눌러야 게임이 시작됩니다 (발사대는 시작 화면에서 켭니다)
            startScreen.Show();
        }
        else if (launcher != null)
        {
            // 시작 화면이 연결되지 않았다면 예전처럼 바로 시작
            launcher.enabled = true;
        }
    }
}