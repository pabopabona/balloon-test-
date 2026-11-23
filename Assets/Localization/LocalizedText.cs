using UnityEngine;
using TMPro;

/// <summary>
/// 씬에 직접 입력한 글자(버튼 글자, 안내 문구 등)를 한국어 / 영어로 바꿔줍니다.
///
/// 사용법
///   1) 글자가 들어 있는 TMP Text 오브젝트에 이 컴포넌트를 붙입니다.
///      → 붙이는 순간 지금 적혀 있는 글자가 Korean 칸에 자동으로 들어갑니다.
///   2) English 칸에 영어 문장을 적습니다.
///
/// 주의: 스크립트가 글자를 직접 바꾸는 텍스트(점수, 레벨, 타이머, 닉네임, 등급 문구, 순위 등)에는
///       붙이지 마세요. 서로 덮어써서 엉뚱한 글자가 나옵니다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(1, 4)] public string korean;
    [TextArea(1, 4)] public string english;

    private TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(korean) && text != null) korean = text.text;
    }

    void OnEnable()
    {
        Loc.OnLanguageChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= Apply;
    }

    /// <summary>현재 언어로 글자를 다시 씁니다.</summary>
    public void Apply()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text != null) text.text = Loc.Pick(korean, english);
    }

    // 인스펙터에서 컴포넌트를 처음 붙일 때, 지금 적힌 글자를 Korean 칸에 자동으로 채움
    void Reset()
    {
        TMP_Text t = GetComponent<TMP_Text>();
        if (t != null) korean = t.text;
    }
}
