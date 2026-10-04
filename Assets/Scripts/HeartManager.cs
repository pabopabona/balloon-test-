using System;
using UnityEngine;

/// <summary>
/// 하트(게임 시작 재화)를 관리합니다.
/// - 시작 하트 3개, 최대 9개(어떤 경로로도 초과 불가)
/// - 시간 충전: regenIntervalMinutes(기본 30분)마다 1개씩, 단 regenMaxHearts(기본 5개)까지만 참
/// - 광고 보상(AddHearts): maxHearts(9개)까지 채울 수 있음
/// - 하트가 5개 이상이면 타이머가 멈추고, 5개 미만으로 내려가는 순간부터 다시 30분을 셉니다
/// - 앱을 꺼둔 동안 흐른 시간도 다음 실행 때 계산해서 채워줍니다(오프라인 충전, 역시 5개까지만)
/// - 값은 PlayerPrefs에 저장되어 앱을 껐다 켜도 유지됩니다
///
/// 주의: 충전 시간 계산에 기기 시계(DateTime.UtcNow)를 씁니다. 테스트 단계에서는 충분하지만,
/// 기기 시계를 조작하면 하트를 공짜로 늘릴 수 있으므로, 정식 출시 전에는 서버 시간 기준으로
/// 바꾸는 것을 권장합니다.
/// </summary>
public class HeartManager : MonoBehaviour
{
    public static HeartManager Instance { get; private set; }

    [Header("규칙")]
    [Tooltip("가질 수 있는 하트의 최대 개수. 광고 보상으로는 여기까지 채울 수 있습니다.")]
    public int maxHearts = 9;

    [Tooltip("시간이 지나면서 자동으로 충전되는 한도. 이 개수에 도달하면 타이머가 멈춥니다.")]
    public int regenMaxHearts = 5;

    public int initialHearts = 3;

    [Tooltip("하트 1개가 충전되는 데 걸리는 시간(분). 테스트할 때 0.2처럼 줄이면 빨리 확인할 수 있어요.")]
    public float regenIntervalMinutes = 30f;

    /// <summary>현재 보유 하트 수.</summary>
    public int Hearts { get; private set; }

    /// <summary>하트 수가 바뀔 때마다 호출됩니다. 인자: (현재 하트, 최대 하트)</summary>
    public event Action<int, int> OnHeartsChanged;

    private const string HeartsKey = "hearts_v1";
    private const string AnchorKey = "hearts_regen_anchor_v1";

    // "현재 충전 주기가 시작된 시각"(UTC ticks). 하트가 시간 충전 한도 미만인 동안만 의미가 있습니다.
    private long anchorTicks;

    // 시간 충전 한도. 실수로 regenMaxHearts를 maxHearts보다 크게 넣어도 최대치를 넘지 않게 합니다.
    private int RegenCap => Mathf.Clamp(regenMaxHearts, 0, maxHearts);

    private TimeSpan RegenInterval => TimeSpan.FromMinutes(Mathf.Max(0.01f, regenIntervalMinutes));

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Load();
        ApplyRegen();
    }

    void Update()
    {
        ApplyRegen();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    void OnApplicationQuit()
    {
        Save();
    }

    // ---------------- 공개 API ----------------

    /// <summary>하트 1개를 사용합니다. 하트가 없으면 false를 반환하고 아무것도 하지 않습니다.</summary>
    public bool TrySpendHeart()
    {
        ApplyRegen();

        if (Hearts <= 0) return false;

        bool timerWasStopped = Hearts >= RegenCap;
        Hearts--;

        // 타이머가 멈춰 있다가(5개 이상) 이번 사용으로 5개 미만이 되는 순간부터 충전 타이머가 시작됩니다.
        if (timerWasStopped && Hearts < RegenCap) anchorTicks = DateTime.UtcNow.Ticks;

        Save();
        Notify();
        return true;
    }

    /// <summary>하트를 추가합니다(최대치를 넘지 않음). 실제로 추가된 개수를 반환합니다.</summary>
    public int AddHearts(int amount)
    {
        ApplyRegen();

        int before = Hearts;
        Hearts = Mathf.Min(maxHearts, Hearts + Mathf.Max(0, amount));

        // 시간 충전 한도 이상이 되면 충전 타이머는 더 이상 필요 없으므로 기준 시각을 현재로 맞춰둡니다.
        if (Hearts >= RegenCap) anchorTicks = DateTime.UtcNow.Ticks;

        int added = Hearts - before;
        if (added > 0)
        {
            Save();
            Notify();
        }
        return added;
    }

    /// <summary>최대치(maxHearts)까지 가득 찼는지. 광고 버튼을 끌지 판단할 때 씁니다.</summary>
    public bool IsFull => Hearts >= maxHearts;

    /// <summary>시간 충전 한도(regenMaxHearts)에 도달했는지. true면 타이머가 멈춘 상태입니다.</summary>
    public bool IsRegenFull => Hearts >= RegenCap;

    /// <summary>다음 하트가 충전될 때까지 남은 초. 타이머가 멈춘 상태(5개 이상)면 0.</summary>
    public float SecondsUntilNextHeart
    {
        get
        {
            if (Hearts >= RegenCap) return 0f;

            TimeSpan elapsed = DateTime.UtcNow - new DateTime(anchorTicks, DateTimeKind.Utc);
            double remaining = RegenInterval.TotalSeconds - elapsed.TotalSeconds;
            return (float)Math.Max(0.0, remaining);
        }
    }

    // ---------------- 내부 로직 ----------------

    private void ApplyRegen()
    {
        int cap = RegenCap;
        if (Hearts >= cap) return; // 시간 충전 한도 이상이면 충전 없음 (광고로 받은 하트는 그대로 유지)

        DateTime now = DateTime.UtcNow;
        DateTime anchor = new DateTime(anchorTicks, DateTimeKind.Utc);

        // 기기 시계를 과거로 되돌린 경우: 기준 시각을 현재로 재설정하고 충전은 하지 않습니다.
        if (now < anchor)
        {
            anchorTicks = now.Ticks;
            Save();
            return;
        }

        long intervalTicks = RegenInterval.Ticks;
        long gained = (now - anchor).Ticks / intervalTicks;
        if (gained <= 0) return;

        Hearts = (int)Math.Min(cap, Hearts + gained);

        if (Hearts >= cap)
        {
            anchorTicks = now.Ticks; // 시간 충전 한도 도달: 타이머 정지
        }
        else
        {
            // 충전된 개수만큼만 기준 시각을 앞으로 당겨서, 남는 시간(나머지)은 다음 하트로 이월
            anchorTicks = anchor.Ticks + intervalTicks * gained;
        }

        Save();
        Notify();
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(HeartsKey))
        {
            // 첫 실행
            Hearts = Mathf.Clamp(initialHearts, 0, maxHearts);
            anchorTicks = DateTime.UtcNow.Ticks;
            Save();
            return;
        }

        Hearts = Mathf.Clamp(PlayerPrefs.GetInt(HeartsKey, initialHearts), 0, maxHearts);

        string savedAnchor = PlayerPrefs.GetString(AnchorKey, "");
        if (!long.TryParse(savedAnchor, out anchorTicks))
        {
            anchorTicks = DateTime.UtcNow.Ticks;
        }
    }

    private void Save()
    {
        PlayerPrefs.SetInt(HeartsKey, Hearts);
        PlayerPrefs.SetString(AnchorKey, anchorTicks.ToString());
        PlayerPrefs.Save();
    }

    private void Notify()
    {
        OnHeartsChanged?.Invoke(Hearts, maxHearts);
    }

    // ---------------- 테스트용 (Inspector 컴포넌트 우측 ⋮ 메뉴에서 실행) ----------------

    [ContextMenu("TEST: 하트 0개로 만들기")]
    private void TestSetZero()
    {
        Hearts = 0;
        anchorTicks = DateTime.UtcNow.Ticks;
        Save();
        Notify();
    }

    [ContextMenu("TEST: 하트 +1 (광고 보상처럼)")]
    private void TestAddOne()
    {
        AddHearts(1);
    }

    [ContextMenu("TEST: 충전 1회분 시간 경과")]
    private void TestSkipOneInterval()
    {
        anchorTicks -= RegenInterval.Ticks;
        ApplyRegen();
    }

    [ContextMenu("TEST: 하트 초기화(처음 상태)")]
    private void TestReset()
    {
        PlayerPrefs.DeleteKey(HeartsKey);
        PlayerPrefs.DeleteKey(AnchorKey);
        Load();
        Notify();
    }
}