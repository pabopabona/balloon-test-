using UnityEngine;
using TMPro;

/// <summary>
/// 게임 화면 아래쪽에 "이번 판 플레이 시간"을 분:초(예: 12:34)로 보여줍니다.
///
/// - 시작 버튼을 눌러 게임이 시작되는 순간부터 셉니다. (발사대가 켜지는 순간 = 게임 시작)
/// - 게임오버가 되면 멈춥니다.
/// - 스킬 연출이나 디버그 일시정지로 게임이 멈춘 동안에는 시간이 흐르지 않습니다.
/// - Restart로 씬을 다시 불러오면 0부터 다시 셉니다.
///
/// 나중에 "40레벨 클리어 시간 보너스" 등에 쓸 수 있도록 ElapsedSeconds를 공개해 두었습니다.
/// 항상 켜져 있는 오브젝트(예: 타이머 텍스트를 담은 Canvas)에 붙이세요.
/// </summary>
public class PlayTimer : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;

    [Tooltip("게임 시작 여부를 판단할 발사대. 시작 버튼을 누르면 켜지고, 게임오버 때 꺼집니다.")]
    public LauncherController launcher;

    [Header("UI")]
    public TMP_Text timerText;

    [Tooltip("표시 형식. {0} = 분, {1} = 초")]
    public string format = "{0:00}:{1:00}";

    [Tooltip("체크하면 게임이 시작되기 전(시작 화면)에는 타이머를 숨깁니다.")]
    public bool hideBeforeStart = true;

    /// <summary>이번 판에서 실제로 플레이한 시간(초). 멈춘 시간은 포함하지 않습니다.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>지금 시간이 흐르고 있는지.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>한 번이라도 게임이 시작됐는지.</summary>
    public bool HasStarted { get; private set; }

    private int lastShownSecond = -1;

    void Start()
    {
        ElapsedSeconds = 0f;
        RefreshText(true);
        if (timerText != null && hideBeforeStart) timerText.gameObject.SetActive(false);
    }

    void Update()
    {
        bool gameOver = gameManager != null && gameManager.IsGameOver;
        bool playing = launcher != null && launcher.enabled && !gameOver;

        if (playing && !HasStarted)
        {
            HasStarted = true;
            if (timerText != null) timerText.gameObject.SetActive(true); // 게임 시작과 함께 바로 표시
        }

        IsRunning = playing;

        // Time.deltaTime은 게임이 멈춘 동안(timeScale = 0) 0이라, 스킬 연출 중에는 자동으로 멈춥니다.
        if (IsRunning) ElapsedSeconds += Time.deltaTime;

        RefreshText(false);
    }

    private void RefreshText(bool force)
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.FloorToInt(ElapsedSeconds);
        if (!force && totalSeconds == lastShownSecond) return; // 초가 바뀔 때만 글자를 갱신
        lastShownSecond = totalSeconds;

        timerText.text = string.Format(format, totalSeconds / 60, totalSeconds % 60);
    }
}
