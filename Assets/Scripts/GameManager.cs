using UnityEngine;

/// <summary>
/// 게임 전반의 진행 상태(레벨 등)를 관리하는 매니저.
/// HexGridManager의 풍선 터짐 이벤트를 구독해서 누적 개수를 세고,
/// 기준치를 넘으면 LauncherController의 currentLevel을 자동으로 올립니다.
/// 씬에 하나만 존재해야 하는 싱글턴입니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("참조")]
    public LauncherController launcher;
    public HexGridManager gridManager;

    [Header("레벨업 기준")]
    [Tooltip("레벨 1 -> 2로 올라가기 위해 필요한 풍선 터짐 개수")]
    public int baseBalloonsToLevelUp = 8;

    [Tooltip("레벨이 하나 오를 때마다 다음 레벨업에 필요한 개수가 얼마나 늘어나는지")]
    public int incrementPerLevel = 2;

    /// <summary>
    /// 레벨업이 발생할 때마다 호출됩니다. 인자는 새로 올라간 레벨 값.
    /// UI(레벨업 텍스트/이펙트 등)를 연결할 때 이 이벤트를 구독하세요.
    /// </summary>
    public System.Action<int> OnLevelUp;

    /// <summary>
    /// 게임오버가 발생했을 때 호출됩니다. 인자는 게임오버 사유 문자열입니다. (예: "danger_line")
    /// </summary>
    public System.Action<string> OnGameOver;

    /// <summary>
    /// 현재 게임오버 상태인지 여부. Balloon 등 다른 스크립트가 이 값을 참조해 동작을 멈출 수 있습니다.
    /// </summary>
    public bool IsGameOver { get; private set; } = false;

    [Header("점수 설정")]
    [Tooltip("풍선 1개가 터질 때 기본으로 얻는 점수")]
    public int pointsPerBalloon = 10;

    [Tooltip("한 번에 터진 개수가 최소 매칭 개수를 초과할 때마다 추가로 주는 보너스 점수 (콤보 보상)")]
    public int bonusPerExtraBalloon = 5;

    /// <summary>
    /// 현재 누적 점수.
    /// </summary>
    public int Score { get; private set; } = 0;

    /// <summary>
    /// 점수가 갱신될 때마다 호출됩니다. 인자는 갱신된 총점.
    /// </summary>
    public System.Action<int> OnScoreChanged;

    /// <summary>
    /// 누적 터뜨린 풍선 개수가 갱신될 때마다 호출됩니다.
    /// 인자: (현재 레벨 안에서 누적된 개수, 다음 레벨업까지 필요한 개수)
    /// 진행바 같은 UI를 붙일 때 사용하면 됩니다.
    /// </summary>
    public System.Action<int, int> OnProgressChanged;

    private int poppedSinceLevelStart = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        if (gridManager != null)
        {
            gridManager.OnBalloonsPopped += HandleBalloonsPopped;
            gridManager.OnDangerLineReached += HandleDangerLineReached;
        }
    }

    void OnDisable()
    {
        if (gridManager != null)
        {
            gridManager.OnBalloonsPopped -= HandleBalloonsPopped;
            gridManager.OnDangerLineReached -= HandleDangerLineReached;
        }
    }

    private void HandleDangerLineReached()
    {
        TriggerGameOver("danger_line");
    }

    /// <summary>
    /// 게임오버를 발생시킵니다. 이미 게임오버 상태라면 아무 동작도 하지 않습니다(중복 호출 방지).
    /// 발사대 입력을 정지시키고, 이벤트를 통해 UI 등에 알립니다.
    /// </summary>
    public void TriggerGameOver(string reason)
    {
        if (IsGameOver) return;

        IsGameOver = true;

        if (launcher != null)
            launcher.enabled = false; // Update 정지 -> 입력/타이머/자동발사 모두 중단

        OnGameOver?.Invoke(reason);
    }

    private void HandleBalloonsPopped(int count)
    {
        if (launcher == null) return;

        // 점수 계산: 기본 점수 + 콤보(최소 매칭 개수 초과분) 보너스
        int extra = Mathf.Max(0, count - gridManager.minMatchCount);
        int gained = (count * pointsPerBalloon) + (extra * bonusPerExtraBalloon);
        Score += gained;
        OnScoreChanged?.Invoke(Score);

        poppedSinceLevelStart += count;
        int threshold = GetThresholdForLevel(launcher.currentLevel);

        // 한 번에 여러 레벨을 넘을 수도 있으니 while로 처리 (콤보로 대량 매칭됐을 경우 대비)
        while (poppedSinceLevelStart >= threshold)
        {
            poppedSinceLevelStart -= threshold;
            launcher.currentLevel++;
            OnLevelUp?.Invoke(launcher.currentLevel);
            threshold = GetThresholdForLevel(launcher.currentLevel);
        }

        OnProgressChanged?.Invoke(poppedSinceLevelStart, threshold);
    }

    /// <summary>
    /// 특정 레벨에서 다음 레벨로 올라가기 위해 필요한 풍선 개수를 계산합니다.
    /// </summary>
    public int GetThresholdForLevel(int level)
    {
        return baseBalloonsToLevelUp + (level - 1) * incrementPerLevel;
    }
}