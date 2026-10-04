using UnityEngine;

/// <summary>
/// 닉네임 저장/불러오기/자동 생성을 한 곳에서 처리하는 도우미.
/// 기존과 같은 PlayerPrefs 키("last_nickname")를 쓰므로, LeaderboardUI는 수정 없이 그대로 동작합니다.
/// (씬에 붙이는 컴포넌트가 아니라, 다른 스크립트에서 NicknameStore.Get() 처럼 바로 호출해서 씁니다)
/// </summary>
public static class NicknameStore
{
    public const string PrefsKey = "last_nickname";

    /// <summary>닉네임 최대 글자 수. 순위표가 8글자까지만 보여주므로 맞춰둡니다.</summary>
    public const int MaxLength = 8;

    /// <summary>자동 생성 닉네임의 앞부분. 예: Pang4821</summary>
    public const string AutoPrefix = "Pang";

    public static bool HasNickname => PlayerPrefs.HasKey(PrefsKey)
                                      && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PrefsKey));

    public static string Get() => PlayerPrefs.GetString(PrefsKey, "Player");

    /// <summary>
    /// 저장된 닉네임이 없으면(첫 실행) 자동으로 만들어 저장합니다. 있으면 그대로 둡니다.
    /// </summary>
    public static string EnsureExists()
    {
        if (!HasNickname)
        {
            string generated = AutoPrefix + Random.Range(1000, 10000); // Pang1000 ~ Pang9999
            Save(generated);
            Debug.Log($"[Nickname] 자동 생성: {generated}");
        }
        return Get();
    }

    /// <summary>
    /// 입력값을 검사합니다. 문제가 없으면 null, 있으면 안내 문구를 돌려줍니다.
    /// </summary>
    public static string Validate(string input)
    {
        string name = Clean(input);
        if (string.IsNullOrEmpty(name)) return "닉네임을 입력해 주세요.";
        if (name.Length > MaxLength) return $"닉네임은 {MaxLength}글자까지 가능해요.";
        return null;
    }

    /// <summary>앞뒤 공백과 줄바꿈을 정리합니다.</summary>
    public static string Clean(string input)
    {
        if (input == null) return "";
        return input.Replace("\n", "").Replace("\r", "").Trim();
    }

    public static void Save(string name)
    {
        PlayerPrefs.SetString(PrefsKey, Clean(name));
        PlayerPrefs.Save();
    }
}