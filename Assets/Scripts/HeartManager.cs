using System;
using UnityEngine;

/// <summary>
/// 하트(게임 시작 재화)를 관리합니다.
/// - 시작 하트 3개, 최대 9개(어떤 경로로도 초과 불가)
/// - 하트가 최대치 미만이면 regenIntervalMinutes(기본 30분)마다 1개씩 자동 충전
/// - 앱을 꺼둔 동안 흐른 시간도 다음 실행 때 계산해서 채워줍니다(오프라인 충전)
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
    public int maxHearts = 9;
    public int initialHearts = 3;

    [Tooltip("하트 1개가 충전되는 데 걸리는 시간(분). 테스트할 때 0.2처럼 줄이면 빨리 확인할 수 있어요.")]
    public float regenIntervalMinutes = 30f;

    /// <summary>현재 보유 하트 수.</summary>
    public int Hearts { get; private set; }

    /// <summary>하트 수가 바뀔 때마다 호출됩니다. 인자: (현재 하트, 최대 하트)</summary>
    public event Action<int, int> OnHeartsChanged;

    private const string HeartsKey = "hearts_v1";
    private const string AnchorKey = "hearts_regen_anchor_v1";

    // "현재 충전 주기가 시작된 시각"(UTC ticks). 하트가 최대치 미만인 동안만 의미가 있습니다.
    private long anchorTicks;

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

        bool wasFull = Hearts >= maxHearts;
        Hearts--;

        // 가득 찬 상태에서 처음 줄어드는 순간부터 충전 타이머가 시작됩니다.
        if (wasFull) anchorTicks = DateTime.UtcNow.Ticks;

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

        // 최대치에 도달하면 충전 타이머는 더 이상 필요 없으므로 기준 시각을 현재로 맞춰둡니다.
        if (Hearts >= maxHearts) anchorTicks = DateTime.UtcNow.Ticks;

        int added = Hearts - before;
        if (added > 0)
        {
            Save();
            Notify();
        }
        return added;
    }

    public bool IsFull => Hearts >= maxHearts;

    /// <summary>다음 하트가 충전될 때까지 남은 초. 이미 가득 차 있으면 0.</summary>
    public float SecondsUntilNextHeart
    {
        get
        {
            if (Hearts >= maxHearts) return 0f;

            TimeSpan elapsed = DateTime.UtcNow - new DateTime(anchorTicks, DateTimeKind.Utc);
            double remaining = RegenInterval.TotalSeconds - elapsed.TotalSeconds;
            return (float)Math.Max(0.0, remaining);
        }
    }

    // ---------------- 내부 로직 ----------------

    private void ApplyRegen()
    {
        if (Hearts >= maxHearts) return;

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

        Hearts = (int)Math.Min(maxHearts, Hearts + gained);

        if (Hearts >= maxHearts)
        {
            anchorTicks = now.Ticks; // 가득 참: 타이머 정지
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

    [ContextMenu("TEST: 하트 초기화(처음 상태)")]
    private void TestReset()
    {
        PlayerPrefs.DeleteKey(HeartsKey);
        PlayerPrefs.DeleteKey(AnchorKey);
        Load();
        Notify();
    }
}
