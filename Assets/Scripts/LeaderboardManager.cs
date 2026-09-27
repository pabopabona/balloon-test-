using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;

/// <summary>
/// 순위표 한 줄(랭크 + 이름 + 점수)을 나타내는 데이터.
/// playerId는 "이게 내 항목인지" 판별하는 용도로만 쓰입니다.
/// </summary>
[System.Serializable]
public class ScoreEntry
{
    public string playerId;
    public string playerName;
    public int score;
    public int rank; // 1부터 시작
    public int level;
}

/// <summary>
/// UGS에 점수와 함께 저장할 메타데이터(플레이어가 입력한 이름).
/// </summary>
[System.Serializable]
public class ScoreMetadata
{
    public string playerName;
    public int level;
}

/// <summary>
/// 점수 순위표를 UGS(Unity Gaming Services) Leaderboards로 저장/조회하는 매니저.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Tooltip("Unity Dashboard에서 만든 리더보드의 ID")]
    public string leaderboardId = "balloon_top_scores";

    [Tooltip("한 번에 조회할 상위 순위 개수")]
    public int maxEntries = 10;

    public bool IsReady { get; private set; } = false;

    /// <summary>
    /// 현재 로그인된(익명) 플레이어의 고유 ID. 순위표 항목 중 "내 것"을 찾을 때 사용합니다.
    /// </summary>
    public string CurrentPlayerId => IsReady ? AuthenticationService.Instance.PlayerId : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    async void Start()
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            IsReady = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LeaderboardManager] UGS 초기화 실패: {e}");
            IsReady = false;
        }
    }

    public async Task SubmitScoreAsync(string playerName, int score, int level)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[LeaderboardManager] 아직 준비되지 않아 점수 제출을 건너뜁니다.");
            return;
        }

        try
        {
            var metadata = new ScoreMetadata
            {
                playerName = string.IsNullOrEmpty(playerName) ? "Player" : playerName,
                level = level
            };

            var options = new AddPlayerScoreOptions { Metadata = metadata };
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score, options);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LeaderboardManager] 점수 제출 실패: {e}");
        }
    }

    /// <summary>
    /// 상위 순위표를 서버에서 조회합니다. 각 항목에는 playerId와 실제 rank가 포함됩니다.
    /// </summary>
    public async Task<List<ScoreEntry>> LoadEntriesAsync()
    {
        List<ScoreEntry> result = new List<ScoreEntry>();

        if (!IsReady)
        {
            Debug.LogWarning("[LeaderboardManager] 아직 준비되지 않아 조회를 건너뜁니다.");
            return result;
        }

        try
        {
            var options = new GetScoresOptions { IncludeMetadata = true, Limit = maxEntries };
            LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, options);

            foreach (LeaderboardEntry entry in page.Results)
            {
                result.Add(ToScoreEntry(entry));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LeaderboardManager] 순위표 조회 실패: {e}");
        }

        return result;
    }

    /// <summary>
    /// 현재 플레이어 본인의 순위/점수만 별도로 조회합니다.
    /// 아직 한 번도 점수를 제출하지 않았거나 조회 실패 시 null을 반환합니다.
    /// </summary>
    public async Task<ScoreEntry> GetMyEntryAsync()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[LeaderboardManager] 아직 준비되지 않아 조회를 건너뜁니다.");
            return null;
        }

        try
        {
            var options = new GetPlayerScoreOptions { IncludeMetadata = true };
            LeaderboardEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId, options);

            if (entry == null) return null;

            return ToScoreEntry(entry);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LeaderboardManager] 내 순위 조회 실패(아직 기록이 없을 수 있음): {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// LeaderboardEntry(서버 응답)를 우리 UI가 다루기 쉬운 ScoreEntry로 변환합니다.
    /// Metadata는 Dictionary가 아니라 JSON 문자열로 오기 때문에 직접 역직렬화합니다.
    /// </summary>
    private ScoreEntry ToScoreEntry(LeaderboardEntry entry)
    {
        string name = "Player";
        int level = 0;

        if (!string.IsNullOrEmpty(entry.Metadata))
        {
            try
            {
                ScoreMetadata meta = JsonUtility.FromJson<ScoreMetadata>(entry.Metadata);
                if (meta != null)
                {
                    if (!string.IsNullOrEmpty(meta.playerName))
                    {
                        name = meta.playerName;
                    }
                    level = meta.level;
                }
            }
            catch (System.Exception parseEx)
            {
                Debug.LogWarning($"[LeaderboardManager] 메타데이터 파싱 실패: {parseEx.Message}");
            }
        }

        return new ScoreEntry
        {
            playerId = entry.PlayerId,
            playerName = name,
            score = (int)entry.Score,
            // UGS의 Rank는 0부터 시작(0-indexed)하므로, 화면에는 1등부터 보이도록 +1 해줍니다.
            rank = entry.Rank + 1,
            level = level
        };
    }
}