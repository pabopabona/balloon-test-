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

    [Tooltip("등장 레벨부터 한 레벨 오를 때마다 가중치를 얼마나 늘릴지. 0이면 항상 같은 가중치. " +
             "예: 벽돌 0.045 → 30레벨 0.2에서 시작해 39레벨에 약 0.6")]
    public float weightPerLevel = 0f;

    [Tooltip("가중치가 늘어날 때의 최대값. 0이면 제한 없음.")]
    public float maxWeight = 0f;

    /// <summary>해당 레벨에서의 실제 가중치 (등장 전이면 0).</summary>
    public float GetWeight(int level)
    {
        if (level < unlockLevel) return 0f;

        float w = weight + (level - unlockLevel) * weightPerLevel;
        if (maxWeight > 0f) w = Mathf.Min(w, maxWeight);
        return Mathf.Max(0f, w);
    }
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

    [Header("벽돌 풍선 - 레벨별 개수")]
    [Tooltip("벽돌이 처음 나오는 레벨")]
    public int brickUnlockLevel = 25;

    [Tooltip("등장 레벨에서 한 레벨 동안 나오는 벽돌 개수 (최소~최대 사이에서 레벨마다 무작위)")]
    public int bricksAtUnlockMin = 2;
    public int bricksAtUnlockMax = 3;

    [Tooltip("레벨이 하나 오를 때마다 늘어나는 벽돌 개수. 0.5면 2레벨마다 1개씩 늘어남")]
    public float bricksIncreasePerLevel = 0.5f;

    [Tooltip("한 레벨에 나올 수 있는 벽돌의 최대 개수")]
    public int maxBricksPerLevel = 8;

    [Tooltip("벽돌과 벽돌 사이에 최소 몇 발은 일반 풍선이 나오게 할지 (벽돌이 연달아 몰려 나오는 것 방지)")]
    public int minShotsBetweenBricks = 4;

    [Tooltip("한 발에 평균 몇 개의 풍선이 터진다고 볼지. 남은 발사 수를 추정해서 벽돌을 레벨 전체에 고르게 퍼뜨리는 데 씁니다. " +
             "벽돌이 레벨 초반에 몰리면 값을 낮추고, 다 못 나오고 레벨이 끝나면 값을 높이세요.")]
    public float estimatedPopsPerShot = 1.5f;

    private int brickQuotaLevel = -1;   // 아래 할당량을 정한 레벨
    private int brickQuota;             // 이번 레벨에 나올 벽돌 개수
    private int bricksSpawnedThisLevel;
    private int shotsSinceLastBrick = 999;

    /// <summary>이번 레벨에 정해진 벽돌 개수 / 지금까지 나온 개수 (디버그·UI용)</summary>
    public int BrickQuotaThisLevel => brickQuota;
    public int BricksSpawnedThisLevel => bricksSpawnedThisLevel;

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

        AddLevelProgress(count);
    }

    /// <summary>
    /// 스킬(회색 스킬의 화면 전체 터뜨리기 등)로 터진 풍선을 점수에 반영합니다.
    /// countTowardLevelUp이 false면 점수만 오르고 레벨업 진행도에는 넣지 않습니다
    /// (수십 개가 한 번에 터져 레벨이 여러 단계 뛰는 것을 막기 위함).
    /// </summary>
    public void AddSkillPops(int count, bool countTowardLevelUp)
    {
        if (count <= 0 || IsGameOver) return;

        Score += count * pointsPerBalloon;
        OnScoreChanged?.Invoke(Score);

        if (countTowardLevelUp) AddLevelProgress(count);
    }

    private void AddLevelProgress(int count)
    {
        if (launcher == null) return;

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
        // 벽돌은 확률이 아니라 "레벨별 개수"로 따로 관리합니다
        if (ShouldSpawnBrick(level)) return BalloonColor.Brick;

        List<ColorUnlock> available = new List<ColorUnlock>();
        float totalWeight = 0f;

        List<float> weights = new List<float>();

        foreach (ColorUnlock entry in colorUnlocks)
        {
            if (entry.color == BalloonColor.Brick) continue; // 벽돌은 위의 개수 규칙으로만 등장

            float w = entry.GetWeight(level); // 레벨에 따라 늘어나는 가중치 반영
            if (w > 0f)
            {
                available.Add(entry);
                weights.Add(w);
                totalWeight += w;
            }
        }

        if (available.Count == 0 || totalWeight <= 0f)
        {
            return BalloonColorUtil.GetRandom(); // 안전장치
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < available.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return available[i].color;
            }
        }

        return available[available.Count - 1].color; // 부동소수점 오차 대비 안전장치
    }

    // ───────────── 벽돌 개수 규칙 ─────────────

    /// <summary>
    /// 다음에 쏠 풍선을 벽돌로 할지 정합니다. (풍선을 하나 뽑을 때마다 한 번 호출됨)
    /// - 레벨이 바뀌면 그 레벨에 나올 벽돌 개수를 새로 정합니다.
    /// - 정한 개수를 넘지 않고, 레벨 전체에 고르게 퍼지도록 확률을 조절합니다.
    /// </summary>
    private bool ShouldSpawnBrick(int level)
    {
        if (level < brickUnlockLevel) return false;

        if (level != brickQuotaLevel) RollBrickQuota(level);

        shotsSinceLastBrick++;

        int remaining = brickQuota - bricksSpawnedThisLevel;
        if (remaining <= 0) return false;
        if (shotsSinceLastBrick <= Mathf.Max(0, minShotsBetweenBricks)) return false;

        // 이번 레벨이 끝날 때까지 남은 발사 수를 대략 추정해서, 남은 벽돌을 그 안에 고르게 배치
        int remainingPops = Mathf.Max(1, GetThresholdForLevel(level) - poppedSinceLevelStart);
        float estimatedShotsLeft = Mathf.Max(1f, remainingPops / Mathf.Max(0.1f, estimatedPopsPerShot));
        float chance = Mathf.Clamp01(remaining / estimatedShotsLeft);

        if (Random.value >= chance) return false;

        bricksSpawnedThisLevel++;
        shotsSinceLastBrick = 0;
        return true;
    }

    private void RollBrickQuota(int level)
    {
        brickQuotaLevel = level;
        bricksSpawnedThisLevel = 0;

        int baseCount = Random.Range(bricksAtUnlockMin, Mathf.Max(bricksAtUnlockMin, bricksAtUnlockMax) + 1);
        int extra = Mathf.FloorToInt((level - brickUnlockLevel) * Mathf.Max(0f, bricksIncreasePerLevel));
        brickQuota = Mathf.Clamp(baseCount + extra, 0, Mathf.Max(0, maxBricksPerLevel));

        Debug.Log($"[Brick] 레벨 {level}: 이번 레벨 벽돌 {brickQuota}개");
    }
}