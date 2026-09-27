using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특정 색깔이 몇 레벨부터 등장할지, 그리고 얼마나 자주 등장할지를 나타내는 데이터.
/// Inspector에서 자유롭게 조정 가능합니다.
/// </summary>
[System.Serializable]
public class ColorUnlock
{
    public BalloonColor color;

    [Tooltip("이 레벨 이상이 되어야 이 색깔 풍선이 등장 풀에 포함됩니다.")]
    public int unlockLevel = 1;

    [Tooltip("등장 확률 가중치. 값이 클수록 더 자주 나옵니다. 기본 색상은 1, " +
             "희귀하게 만들고 싶은 색은 낮게(예: 0.3) 설정하세요. 0이면 절대 안 나옵니다.")]
    public float weight = 1f;
}

/// <summary>
/// 게임 전반의 진행 상태(레벨, 점수, 게임오버, 색상 등장 레벨/확률)를 관리하는 매니저.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("참조")]
    public LauncherController launcher;
    public HexGridManager gridManager;

    [Header("레벨업 기준")]
    public int baseBalloonsToLevelUp = 8;
    public int incrementPerLevel = 2;

    public System.Action<int> OnLevelUp;
    public System.Action<string> OnGameOver;
    public bool IsGameOver { get; private set; } = false;

    [Header("점수 설정")]
    public int pointsPerBalloon = 10;
    public int bonusPerExtraBalloon = 5;
    public int Score { get; private set; } = 0;
    public System.Action<int> OnScoreChanged;

    [Header("색깔별 등장 레벨 / 확률")]
    [Tooltip("각 색깔이 몇 레벨부터 등장할지, 얼마나 자주 나올지 설정합니다. " +
             "여기서 리스트만 조정하면 코드를 건드리지 않고도 밸런스를 바로 테스트할 수 있습니다.")]
    public List<ColorUnlock> colorUnlocks = new List<ColorUnlock>
    {
        new ColorUnlock { color = BalloonColor.Red,     unlockLevel = 1,  weight = 1f },
        new ColorUnlock { color = BalloonColor.Blue,    unlockLevel = 1,  weight = 1f },
        new ColorUnlock { color = BalloonColor.Green,   unlockLevel = 1,  weight = 1f },
        new ColorUnlock { color = BalloonColor.Yellow,  unlockLevel = 1,  weight = 1f },
        new ColorUnlock { color = BalloonColor.Purple,  unlockLevel = 1,  weight = 1f },
        new ColorUnlock { color = BalloonColor.Rainbow, unlockLevel = 15, weight = 0.3f },
        new ColorUnlock { color = BalloonColor.Gray,    unlockLevel = 19, weight = 0.3f },
    };

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

    public void TriggerGameOver(string reason)
    {
        if (IsGameOver) return;

        IsGameOver = true;

        if (launcher != null)
            launcher.enabled = false;

        OnGameOver?.Invoke(reason);
    }

    private void HandleBalloonsPopped(int count)
    {
        if (launcher == null) return;

        int extra = Mathf.Max(0, count - gridManager.minMatchCount);
        int gained = (count * pointsPerBalloon) + (extra * bonusPerExtraBalloon);
        Score += gained;
        OnScoreChanged?.Invoke(Score);

        poppedSinceLevelStart += count;
        int threshold = GetThresholdForLevel(launcher.currentLevel);

        while (poppedSinceLevelStart >= threshold)
        {
            poppedSinceLevelStart -= threshold;
            launcher.currentLevel++;
            OnLevelUp?.Invoke(launcher.currentLevel);
            threshold = GetThresholdForLevel(launcher.currentLevel);
        }
    }

    public int GetThresholdForLevel(int level)
    {
        return baseBalloonsToLevelUp + (level - 1) * incrementPerLevel;
    }

    /// <summary>
    /// 현재 레벨 기준으로 등장 가능한 색깔들 중, 가중치(weight)에 비례한 확률로 하나를 뽑습니다.
    /// 예: Red(가중치1), Rainbow(가중치0.3)면 Rainbow는 Red보다 대략 1/3 확률로 등장합니다.
    /// </summary>
    public BalloonColor GetRandomColorForLevel(int level)
    {
        List<ColorUnlock> available = new List<ColorUnlock>();
        float totalWeight = 0f;

        foreach (ColorUnlock entry in colorUnlocks)
        {
            if (level >= entry.unlockLevel && entry.weight > 0f)
            {
                available.Add(entry);
                totalWeight += entry.weight;
            }
        }

        if (available.Count == 0 || totalWeight <= 0f)
        {
            return BalloonColorUtil.GetRandom(); // 안전장치
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (ColorUnlock entry in available)
        {
            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                return entry.color;
            }
        }

        return available[available.Count - 1].color; // 부동소수점 오차 대비 안전장치
    }
}