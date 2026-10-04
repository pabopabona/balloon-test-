using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 시작 직후, 화면 크기/격자 계산이 완전히 안정될 때까지 화면 전체를 가리는 로딩 스크린.
///
/// [변경] 예전에는 로딩이 끝나면 닉네임 입력창을 띄웠지만, 이제는
///   - 첫 실행이면 닉네임을 자동으로 만들어 저장하고 (예: Pang4821)
///   - 바로 시작 화면(하트 확인)으로 넘어갑니다.
/// 닉네임은 시작 화면의 "닉네임 변경"(NicknameEditor)에서 언제든 바꿀 수 있습니다.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [Header("참조")]
    public HexGridManager gridManager;

    [Tooltip("시작 버튼을 누르기 전까지 조작을 막아둘 발사대. LauncherController는 화면 터치를 " +
             "UI 클릭 시스템을 거치지 않고 직접 읽기 때문에, 로딩/시작 화면이 떠 있어도 " +
             "그 아래에서 조작이 먼저 반응해버릴 수 있습니다. 이 컴포넌트를 꺼둬서 원천 차단합니다.")]
    public LauncherController launcher;

    [Tooltip("로딩이 끝난 뒤 보여줄 시작 화면(하트 확인 + 시작 버튼). " +
             "비워두면 로딩 직후 바로 게임이 시작됩니다.")]
    public StartScreenController startScreen;

    [Header("로딩 화면 UI")]
    [Tooltip("로딩 캔버스 전체를 담는 오브젝트. 로딩이 끝나면 꺼집니다.")]
    public GameObject loadingPanel;

    [Tooltip("\"Loading...\" 텍스트 + 스피너를 묶은 부모")]
    public GameObject loadingContent;

    [Header("설정")]
    [Tooltip("최소한 이 시간(초) 동안은 로딩 화면을 유지합니다.")]
    public float minimumDisplayTime = 0.5f;

    [Tooltip("혹시 모를 상황(무한 대기) 대비 안전장치")]
    public float maxWaitTime = 5f;

    void Start()
    {
        // 닉네임이 없으면(첫 실행) 여기서 자동 생성. 이후 리더보드 제출 때 이 이름이 쓰입니다.
        NicknameStore.EnsureExists();

        // 시작 버튼을 누르기 전까지는 발사대 조작 차단
        if (launcher != null) launcher.enabled = false;

        // 게임오버 후 재시작이면 로딩 화면을 건너뛰고 바로 시작 화면(하트 확인)으로 갑니다.
        if (SessionState.IsRestart && startScreen != null)
        {
            SessionState.IsRestart = false;

            if (loadingPanel != null) loadingPanel.SetActive(false);
            startScreen.Show();
            return;
        }

        SessionState.IsRestart = false;

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingContent != null) loadingContent.SetActive(true);

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

        FinishLoading();
    }

    /// <summary>로딩 화면을 닫고 시작 화면으로 넘어갑니다.</summary>
    private void FinishLoading()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (startScreen != null)
        {
            // 하트를 확인하고 "시작"을 눌러야 게임이 시작됩니다 (발사대는 시작 화면에서 켭니다)
            startScreen.Show();
        }
        else if (launcher != null)
        {
            // 시작 화면이 연결되지 않았다면 바로 시작
            launcher.enabled = true;
        }
    }
}