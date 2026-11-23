using UnityEngine;

/// <summary>게임에서 지원하는 언어.</summary>
public enum GameLanguage
{
    Korean = 0,
    English = 1,
}

/// <summary>
/// 한국어 / 영어 전환을 담당하는 아주 가벼운 다국어 관리자.
///
/// - 처음 실행 시 폰 언어가 한국어면 한국어, 그 외 모든 언어는 영어로 시작합니다.
/// - SetLanguage로 바꾸면 기기에 저장되고, 화면의 모든 LocalizedText / LocalizedImage가 바로 바뀝니다.
/// - 스크립트에서는 Loc.Pick(한국어, 영어)로 현재 언어의 문장을 고릅니다.
///
/// 씬에 붙일 필요가 없는 static 클래스입니다. (Assets 아래 아무 폴더에 두면 됩니다)
/// </summary>
public static class Loc
{
    private const string SavedKey = "game_language";

    private static bool initialized;
    private static GameLanguage current;

    /// <summary>언어가 바뀌었을 때 호출됩니다. (화면 글자 새로고침용)</summary>
    public static event System.Action OnLanguageChanged;

    /// <summary>현재 언어.</summary>
    public static GameLanguage Current
    {
        get { Init(); return current; }
    }

    public static bool IsKorean => Current == GameLanguage.Korean;

    private static void Init()
    {
        if (initialized) return;
        initialized = true;

        int saved = PlayerPrefs.GetInt(SavedKey, -1);
        current = saved >= 0 ? (GameLanguage)saved : DetectSystemLanguage();
    }

    /// <summary>폰 언어 기준 기본 언어. 한국어가 아니면 모두 영어.</summary>
    public static GameLanguage DetectSystemLanguage()
    {
        return Application.systemLanguage == SystemLanguage.Korean
            ? GameLanguage.Korean
            : GameLanguage.English;
    }

    /// <summary>
    /// 언어를 바꿉니다. save가 true면 기기에 저장되어 다음 실행에도 유지됩니다.
    /// (나중에 설정 화면에 언어 선택 버튼을 넣을 때 사용)
    /// </summary>
    public static void SetLanguage(GameLanguage language, bool save = true)
    {
        Init();

        if (save)
        {
            PlayerPrefs.SetInt(SavedKey, (int)language);
            PlayerPrefs.Save();
        }

        if (current == language) return;
        current = language;
        OnLanguageChanged?.Invoke();
    }

    /// <summary>저장된 언어 선택을 지우고 폰 언어를 따르게 합니다.</summary>
    public static void ResetToSystemLanguage()
    {
        PlayerPrefs.DeleteKey(SavedKey);
        GameLanguage before = Current;
        current = DetectSystemLanguage();
        if (before != current) OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// 현재 언어에 맞는 문장을 고릅니다.
    /// 영어 칸이 비어 있으면 한국어를, 한국어 칸이 비어 있으면 영어를 대신 보여줍니다. (빈칸 방지)
    /// </summary>
    public static string Pick(string korean, string english)
    {
        if (Current == GameLanguage.English)
            return string.IsNullOrEmpty(english) ? korean : english;

        return string.IsNullOrEmpty(korean) ? english : korean;
    }
}
