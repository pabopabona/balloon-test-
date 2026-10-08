using UnityEngine;

/// <summary>
/// 닉네임 저장/불러오기/자동 생성을 한 곳에서 처리하는 도우미.
/// 기존과 같은 PlayerPrefs 키("last_nickname")를 쓰므로, LeaderboardUI는 수정 없이 그대로 동작합니다.
/// (씬에 붙이는 컴포넌트가 아니라, 다른 스크립트에서 NicknameStore.Get() 처럼 바로 호출해서 씁니다)
/// </summary>
public static class NicknameStore
{
    public const string PrefsKey = "last_nickname";

    /// <summary>화면에 보여주는 글자 수. 이보다 길면 뒤를 "..."으로 줄여서 표시합니다.</summary>
    public const int DisplayLength = 8;

    /// <summary>입력할 수 있는 최대 글자 수 (저장되는 실제 이름의 한도).</summary>
    public const int MaxLength = 16;

    /// <summary>자동 생성 닉네임의 앞부분. 예: Pang4821</summary>
    public const string AutoPrefix = "Pang";

    public static bool HasNickname => PlayerPrefs.HasKey(PrefsKey)
                                      && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PrefsKey));

    /// <summary>저장된 닉네임 전체(줄이지 않은 원래 이름).</summary>
    public static string Get() => PlayerPrefs.GetString(PrefsKey, "Player");

    /// <summary>화면 표시용 닉네임. 8글자를 넘으면 "여덟글자까지..." 형태로 줄입니다.</summary>
    public static string GetDisplay() => ToDisplay(Get());

    /// <summary>이름이 DisplayLength보다 길면 뒤를 "..."으로 줄입니다. 저장된 값은 바뀌지 않습니다.</summary>
    public static string ToDisplay(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length <= DisplayLength) return name;
        return name.Substring(0, DisplayLength) + "...";
    }

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

    /// <summary>앞뒤 공백과 줄바꿈을 정리하고, 최대 글자 수를 넘으면 잘라냅니다.</summary>
    public static string Clean(string input)
    {
        if (input == null) return "";

        string name = input.Replace("\n", "").Replace("\r", "").Trim();
        if (name.Length > MaxLength) name = name.Substring(0, MaxLength).Trim();
        return name;
    }

    /// <summary>
    /// 닉네임을 저장합니다. 비어 있으면 저장하지 않고 false를 돌려줍니다(원래 이름 유지).
    /// </summary>
    public static bool Save(string name)
    {
        string cleaned = Clean(name);
        if (string.IsNullOrEmpty(cleaned)) return false;

        PlayerPrefs.SetString(PrefsKey, cleaned);
        PlayerPrefs.Save();
        return true;
    }
}