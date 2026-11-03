using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 게임오버 때 도달한 레벨에 따라 칭찬/격려 문구를 보여주고, 다음 목표를 알려줘서
/// "한 판만 더" 해보고 싶게 만드는 스크립트.
///
///   Lv. 12  좀 더 노력해봐
///   최고 등급이에요! ...          ← 최고 등급일 때만 표시
///   최고 기록 Lv. 18              ← 내 최고 기록 (새 기록이면 "새 기록!")
///
/// 등급(레벨 구간, 문구, 색)은 인스펙터의 Tiers 목록에서 자유롭게 바꿀 수 있습니다.
/// 항상 켜져 있는 오브젝트(예: GameOverUI가 붙은 오브젝트)에 붙이세요.
/// </summary>
public class GameOverRating : MonoBehaviour
{
    [System.Serializable]
    public class RatingTier
    {
        [Tooltip("이 레벨 이하이면 이 등급 (마지막 등급은 아무리 높아도 해당)")]
        public int maxLevel;

        public string message;

        [Tooltip("문구 색")]
        public Color color = Color.white;
    }

    [Header("참조")]
    public GameManager gameManager;
    public LauncherController launcher;

    [Header("UI (필요한 것만 연결)")]
    [Tooltip("배경 이미지를 포함한 묶음 오브젝트 (선택). 넣으면 텍스트와 함께 켜지고 꺼집니다. " +
             "배경 이미지 위에 아래 텍스트들을 자식으로 올려두세요.")]
    public GameObject ratingRoot;

    [Tooltip("도달 레벨. 예: \"Lv. 17\"")]
    public TMP_Text levelText;

    [Tooltip("칭찬/격려 문구. 예: \"좀 하는데\"")]
    public TMP_Text ratingText;

    [Tooltip("최고 등급일 때만 보여줄 추가 문구. 예: \"최고 등급이에요! 기록을 더 늘려보세요\"")]
    public TMP_Text nextGoalText;

    [Tooltip("최고 기록. 예: \"최고 기록 Lv. 22\" / \"새 기록!\"")]
    public TMP_Text bestText;

    [Header("표시 형식")]
    [Tooltip("{0} = 도달 레벨")]
    public string levelFormat = "Lv. {0}";

    [Tooltip("최고 등급일 때 Next Goal Text에 보여줄 문구 (비워두면 숨김). 그 외 등급에서는 Next Goal Text가 숨겨집니다.")]
    public string topTierGoalMessage = "최고 등급이에요! 기록을 더 늘려보세요";

    [Tooltip("{0} = 최고 기록 레벨")]
    public string bestFormat = "최고 기록 Lv. {0}";

    public string newBestMessage = "새 기록!";

    [Header("최고 등급일 때 표시")]
    [Tooltip("체크하면 최고 등급일 때 Rating Text를 숨기고 Next Goal Text만 보여줍니다.")]
    public bool hideRatingOnTopTier = true;

    [Tooltip("체크하면 최고 등급일 때도 Best Text(최고 기록)를 함께 보여줍니다.")]
    public bool showBestOnTopTier = true;

    [Header("등급 (레벨이 낮은 순서대로)")]
    public List<RatingTier> tiers = new List<RatingTier>
    {
        new RatingTier { maxLevel = 15,  message = "좀 더 노력해봐",          color = new Color(0.85f, 0.85f, 0.85f) },
        new RatingTier { maxLevel = 20,  message = "좀 하는데",              color = new Color(0.55f, 0.85f, 1.00f) },
        new RatingTier { maxLevel = 25,  message = "훌륭해요!!!",            color = new Color(1.00f, 0.85f, 0.25f) },
        new RatingTier { maxLevel = 999, message = "믿을 수 없는 수준이네요!!", color = new Color(1.00f, 0.45f, 0.75f) },
    };

    private const string BestLevelKey = "best_level";

    /// <summary>지금까지의 최고 도달 레벨 (기기에 저장).</summary>
    public int BestLevel => PlayerPrefs.GetInt(BestLevelKey, 0);

    void OnEnable()
    {
        if (gameManager != null) gameManager.OnGameOver += HandleGameOver;
        SetVisible(false);
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver(string reason)
    {
        int level = launcher != null ? launcher.currentLevel : 1;
        Show(level);
    }

    /// <summary>도달 레벨에 맞는 문구를 표시합니다. (테스트나 클리어 화면에서도 직접 호출 가능)</summary>
    public void Show(int level)
    {
        if (tiers == null || tiers.Count == 0) return;

        int tierIndex = GetTierIndex(level);
        RatingTier tier = tiers[tierIndex];

        // 최고 기록 갱신 여부 (저장하기 전에 비교)
        int previousBest = BestLevel;
        bool isNewBest = level > previousBest;
        if (isNewBest)
        {
            PlayerPrefs.SetInt(BestLevelKey, level);
            PlayerPrefs.Save();
        }

        if (levelText != null)
            levelText.text = string.Format(levelFormat, level);

        if (ratingText != null)
        {
            ratingText.text = tier.message;
            ratingText.color = tier.color;
        }

        bool isTopTier = tierIndex >= tiers.Count - 1;
        if (nextGoalText != null)
            nextGoalText.text = topTierGoalMessage;

        if (bestText != null)
        {
            // 첫 판(이전 기록 없음)에는 "새 기록!" 대신 최고 기록만 보여줌
            bestText.text = (isNewBest && previousBest > 0)
                ? newBestMessage
                : string.Format(bestFormat, Mathf.Max(level, previousBest));
        }

        SetVisible(true);

        // 상황별 표시
        //   일반 등급:  Rating Text + Best Text
        //   최고 등급:  Next Goal Text (+ Best Text, 설정에 따라)
        // 숨긴 텍스트는 Vertical Layout Group이 자리를 비워주므로, 보이는 것만 가운데로 모입니다.
        if (ratingText != null)
            ratingText.gameObject.SetActive(!(isTopTier && hideRatingOnTopTier));

        if (nextGoalText != null)
            nextGoalText.gameObject.SetActive(isTopTier && !string.IsNullOrEmpty(topTierGoalMessage));

        if (bestText != null)
            bestText.gameObject.SetActive(!isTopTier || showBestOnTopTier);
    }

    private int GetTierIndex(int level)
    {
        for (int i = 0; i < tiers.Count; i++)
        {
            if (level <= tiers[i].maxLevel) return i;
        }
        return tiers.Count - 1;
    }

    private void SetVisible(bool visible)
    {
        if (ratingRoot != null) ratingRoot.SetActive(visible);

        if (levelText != null) levelText.gameObject.SetActive(visible);
        if (ratingText != null) ratingText.gameObject.SetActive(visible);
        if (nextGoalText != null) nextGoalText.gameObject.SetActive(visible);
        if (bestText != null) bestText.gameObject.SetActive(visible);
    }

    // ───────────── 테스트용 (Play 중 컴포넌트 ⋮ 메뉴) ─────────────

    [ContextMenu("TEST: Lv.12로 표시")] private void Test12() => Show(12);
    [ContextMenu("TEST: Lv.18로 표시")] private void Test18() => Show(18);
    [ContextMenu("TEST: Lv.23로 표시")] private void Test23() => Show(23);
    [ContextMenu("TEST: Lv.31로 표시")] private void Test31() => Show(31);
    [ContextMenu("TEST: 최고 기록 지우기")] private void TestResetBest() => PlayerPrefs.DeleteKey(BestLevelKey);
}