using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이름 + 점수 한 건을 나타내는 데이터.
/// </summary>
[System.Serializable]
public class ScoreEntry
{
    public string playerName;
    public int score;
}

/// <summary>
/// JsonUtility는 최상위가 배열/리스트인 JSON을 바로 다루지 못해서, 감싸는 용도의 래퍼입니다.
/// </summary>
[System.Serializable]
public class ScoreEntryListWrapper
{
    public List<ScoreEntry> entries = new List<ScoreEntry>();
}

/// <summary>
/// 점수 순위표를 저장/조회하는 매니저.
/// 지금은 PlayerPrefs를 이용한 "기기 안에만 저장되는 로컬 순위표"로 구현되어 있습니다.
/// 나중에 온라인 순위표로 확장할 때는, SubmitScore/LoadEntries 두 메서드의 내부 구현만
/// 서버 API 호출(코루틴/async)로 교체하면 되도록 설계했습니다. 이 클래스를 사용하는
/// LeaderboardUI 쪽 코드는 그대로 두어도 됩니다.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private const string PrefsKey = "local_leaderboard_v1";

    [Tooltip("순위표에 보관할 최대 인원 수")]
    public int maxEntries = 10;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 저장된 전체 순위표를 점수 내림차순으로 불러옵니다.
    /// </summary>
    public List<ScoreEntry> LoadEntries()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json))
            return new List<ScoreEntry>();

        ScoreEntryListWrapper wrapper = JsonUtility.FromJson<ScoreEntryListWrapper>(json);
        return (wrapper != null && wrapper.entries != null) ? wrapper.entries : new List<ScoreEntry>();
    }

    private void SaveEntries(List<ScoreEntry> entries)
    {
        ScoreEntryListWrapper wrapper = new ScoreEntryListWrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 새 점수를 등록합니다. 등록 후 점수 내림차순으로 정렬되고, 상위 maxEntries개만 유지됩니다.
    /// </summary>
    public void SubmitScore(string playerName, int score)
    {
        List<ScoreEntry> entries = LoadEntries();

        entries.Add(new ScoreEntry
        {
            playerName = string.IsNullOrEmpty(playerName) ? "Player" : playerName,
            score = score
        });

        entries.Sort((a, b) => b.score.CompareTo(a.score));

        if (entries.Count > maxEntries)
            entries.RemoveRange(maxEntries, entries.Count - maxEntries);

        SaveEntries(entries);
    }

    /// <summary>
    /// 이 점수가 현재 순위표에 들어갈 수 있는지(순위표가 다 안 찼거나, 꼴찌보다 높은지) 확인합니다.
    /// UI에서 "순위표에 오를 수 있어요!" 같은 연출을 넣고 싶을 때 사용하면 됩니다.
    /// </summary>
    public bool IsHighScore(int score)
    {
        List<ScoreEntry> entries = LoadEntries();
        if (entries.Count < maxEntries) return true;
        return score > entries[entries.Count - 1].score;
    }
}
