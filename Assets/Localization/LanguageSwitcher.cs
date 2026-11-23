using UnityEngine;

/// <summary>
/// 언어 전환 도우미.
///
/// 1) 에디터 테스트: Editor Test Language를 English로 두고 Play하면, 폰 언어와 상관없이 영어로 확인할 수 있습니다.
///    (에디터에서만 적용되고 저장되지 않습니다. 빌드에는 영향 없음)
/// 2) 나중에 설정 화면에 언어 버튼을 넣을 때 버튼 OnClick에 SetKorean / SetEnglish를 연결하면 됩니다.
///
/// 항상 켜져 있는 오브젝트(예: GameManager)에 붙이세요.
/// </summary>
public class LanguageSwitcher : MonoBehaviour
{
    public enum EditorTestLanguage { FollowDevice, Korean, English }

    [Tooltip("에디터에서 Play할 때만 적용되는 테스트 언어. 빌드에서는 무시됩니다.")]
    public EditorTestLanguage editorTestLanguage = EditorTestLanguage.FollowDevice;

    void Awake()
    {
#if UNITY_EDITOR
        switch (editorTestLanguage)
        {
            case EditorTestLanguage.Korean:  Loc.SetLanguage(GameLanguage.Korean, false); break;
            case EditorTestLanguage.English: Loc.SetLanguage(GameLanguage.English, false); break;
        }
#endif
    }

    /// <summary>버튼용: 한국어로 바꾸고 저장</summary>
    public void SetKorean() => Loc.SetLanguage(GameLanguage.Korean);

    /// <summary>버튼용: 영어로 바꾸고 저장</summary>
    public void SetEnglish() => Loc.SetLanguage(GameLanguage.English);

    /// <summary>버튼용: 한국어 ↔ 영어 전환</summary>
    public void Toggle() => Loc.SetLanguage(Loc.IsKorean ? GameLanguage.English : GameLanguage.Korean);

    [ContextMenu("TEST: 지금 영어로")] private void TestEnglish() => Loc.SetLanguage(GameLanguage.English, false);
    [ContextMenu("TEST: 지금 한국어로")] private void TestKorean() => Loc.SetLanguage(GameLanguage.Korean, false);
    [ContextMenu("TEST: 저장된 언어 지우기 (폰 언어 따르기)")] private void TestReset() => Loc.ResetToSystemLanguage();
}
