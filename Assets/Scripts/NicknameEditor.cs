using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 시작 화면 안에서 바로 닉네임을 바꾸는 기능.
/// 화면을 덮는 패널 없이, 같은 자리에서 표시 ↔ 입력이 전환됩니다.
///
///   [평소]   닉네임이 적힌 버튼 (NicknameChangeButton)
///   [편집중] 입력창 (NameInputField)
///
/// 확인 버튼은 없습니다. 입력이 끝나면 자동으로 적용됩니다:
///   - 키보드의 완료(Enter)를 누르거나
///   - 입력창 밖의 아무 곳이나 누르거나 (시작 버튼 포함)
///   - 휴대폰에서 키보드를 닫으면
///
/// 규칙
///   - 비어 있으면 저장하지 않고 원래 이름으로 돌아갑니다.
///   - 8글자를 넘어도 그대로 저장되고, 화면에는 앞 8글자 + "..."으로 줄여서 보여줍니다.
///     (입력 자체는 NicknameStore.MaxLength 글자까지만 가능)
///
/// 항상 켜져 있는 오브젝트(예: HeartSystem)에 붙이세요.
/// </summary>
public class NicknameEditor : MonoBehaviour
{
    [Header("평소 표시")]
    [Tooltip("현재 닉네임을 보여줄 텍스트 (닉네임 버튼 안의 Text)")]
    public TMP_Text currentNameText;

    [Tooltip("표시 형식. {0} 자리에 닉네임이 들어갑니다.")]
    public string currentNameFormat = "{0}";

    [Tooltip("누르면 입력 모드로 바뀌는 버튼 (NicknameChangeButton)")]
    public Button changeButton;

    [Tooltip("평소에만 보이고, 입력 모드에서는 숨길 오브젝트들 (예: referenceText \"당신의 이름을 바꿔보세요\")")]
    public GameObject[] hideWhileEditing;

    [Header("편집 모드")]
    [Tooltip("닉네임 입력창. 입력이 끝나면 자동으로 적용됩니다.")]
    public TMP_InputField nicknameInputField;

    private bool isEditing;
    private bool closeRequested;

    void Awake()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.characterLimit = NicknameStore.MaxLength;
            nicknameInputField.lineType = TMP_InputField.LineType.SingleLine;

            // 입력이 끝나면(완료 키, 입력창 밖 터치, 키보드 닫기) 자동으로 적용하고 평소 표시로 돌아갑니다.
            nicknameInputField.onEndEdit.AddListener(HandleEndEdit);
        }

        if (changeButton != null) changeButton.onClick.AddListener(Open);

        SetEditing(false);
    }

    void OnEnable()
    {
        // 첫 실행에서 LoadingScreenController보다 먼저 실행될 수도 있으므로 여기서도 보장
        NicknameStore.EnsureExists();
        RefreshCurrentName();
    }

    /// <summary>입력 모드로 전환합니다. (changeButton에 자동 연결됨)</summary>
    public void Open()
    {
        // 입력창에는 줄이지 않은 전체 이름을 보여줍니다.
        if (nicknameInputField != null)
            nicknameInputField.text = NicknameStore.Get();

        closeRequested = false;
        SetEditing(true);

        if (nicknameInputField != null)
        {
            nicknameInputField.Select();
            nicknameInputField.ActivateInputField(); // 모바일에서 바로 키보드가 올라오도록
        }
    }

    /// <summary>
    /// 입력이 끝났을 때 호출됩니다. 비어 있지 않으면 저장하고, 평소 표시로 돌아갑니다.
    /// </summary>
    private void HandleEndEdit(string input)
    {
        if (!isEditing) return;

        // 비어 있으면 Save가 아무것도 하지 않아서 원래 이름이 그대로 남습니다.
        NicknameStore.Save(input);
        RefreshCurrentName();

        // 입력창이 자기 이벤트를 처리하는 도중에 꺼버리면 오류가 날 수 있어서,
        // 실제로 닫는 것은 이번 프레임의 마지막(LateUpdate)으로 미룹니다.
        closeRequested = true;
    }

    void LateUpdate()
    {
        if (!closeRequested) return;
        closeRequested = false;

        if (isEditing) SetEditing(false);
    }

    private void SetEditing(bool editing)
    {
        isEditing = editing;

        if (currentNameText != null) currentNameText.gameObject.SetActive(!editing);
        if (changeButton != null) changeButton.gameObject.SetActive(!editing);

        if (hideWhileEditing != null)
            foreach (GameObject go in hideWhileEditing)
                if (go != null) go.SetActive(!editing);

        if (nicknameInputField != null) nicknameInputField.gameObject.SetActive(editing);
    }

    /// <summary>현재 닉네임 표시를 다시 그립니다. 8글자를 넘으면 "..."으로 줄여서 보여줍니다.</summary>
    public void RefreshCurrentName()
    {
        if (currentNameText != null)
            currentNameText.text = string.Format(currentNameFormat, NicknameStore.GetDisplay());
    }
}