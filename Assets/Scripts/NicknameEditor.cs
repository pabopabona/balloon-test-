using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 시작 화면 안에서 바로 닉네임을 바꾸는 기능.
/// 화면을 덮는 패널 없이, 같은 자리에서 표시 ↔ 입력이 전환됩니다.
///
///   [평소]   NicknameText  +  NicknameChangeButton
///   [편집중] NameInputField + AcceptButton
///
/// 편집 중에도 시작 버튼은 그대로 눌러서 게임을 시작할 수 있습니다.
/// 입력하다가 Accept 없이 시작을 눌러도, 올바른 이름이면 자동으로 저장됩니다.
///
/// 항상 켜져 있는 오브젝트(예: HeartSystem)에 붙이세요.
/// </summary>
public class NicknameEditor : MonoBehaviour
{
    [Header("평소 표시")]
    [Tooltip("현재 닉네임을 보여줄 텍스트 (NicknameText)")]
    public TMP_Text currentNameText;

    [Tooltip("표시 형식. {0} 자리에 닉네임이 들어갑니다.")]
    public string currentNameFormat = "{0}";

    [Tooltip("누르면 입력 모드로 바뀌는 버튼 (NicknameChangeButton)")]
    public Button changeButton;

    [Tooltip("평소에만 보이고, 입력 모드에서는 숨길 오브젝트들 (예: referenceText \"당신의 이름을 바꿔보세요\")")]
    public GameObject[] hideWhileEditing;

    [Header("편집 모드")]
    public TMP_InputField nicknameInputField;

    [Tooltip("확인 버튼 (AcceptButton)")]
    public Button acceptButton;

    [Tooltip("입력이 잘못됐을 때 안내 문구를 띄울 텍스트 (선택)")]
    public TMP_Text errorText;

    private bool isEditing;

    void Awake()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.characterLimit = NicknameStore.MaxLength;
            nicknameInputField.lineType = TMP_InputField.LineType.SingleLine;

            // 키보드의 완료(Enter)를 누르면 확인 버튼과 똑같이 저장
            nicknameInputField.onSubmit.AddListener(_ => OnAcceptPressed());

            // 확인을 안 누르고 다른 곳(예: 시작 버튼)을 눌러 입력이 끝나도, 올바른 이름이면 저장
            nicknameInputField.onEndEdit.AddListener(SaveIfValid);
        }

        if (changeButton != null) changeButton.onClick.AddListener(Open);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptPressed);

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
        if (nicknameInputField != null)
            nicknameInputField.text = NicknameStore.Get();

        SetError("");
        SetEditing(true);

        if (nicknameInputField != null)
        {
            nicknameInputField.Select();
            nicknameInputField.ActivateInputField(); // 모바일에서 바로 키보드가 올라오도록
        }
    }

    /// <summary>입력 없이 평소 표시로 돌아갑니다.</summary>
    public void Close()
    {
        SetEditing(false);
    }

    private void OnAcceptPressed()
    {
        if (!isEditing) return;

        string input = nicknameInputField != null ? nicknameInputField.text : "";

        string error = NicknameStore.Validate(input);
        if (error != null)
        {
            SetError(error);
            return;
        }

        NicknameStore.Save(input);
        RefreshCurrentName();
        SetEditing(false);
    }

    private void SaveIfValid(string input)
    {
        if (!isEditing) return;
        if (NicknameStore.Validate(input) != null) return; // 잘못된 값은 조용히 무시 (기존 이름 유지)

        NicknameStore.Save(input);
        RefreshCurrentName();
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
        if (acceptButton != null) acceptButton.gameObject.SetActive(editing);

        if (!editing) SetError("");
    }

    /// <summary>현재 닉네임 표시를 다시 그립니다.</summary>
    public void RefreshCurrentName()
    {
        if (currentNameText != null)
            currentNameText.text = string.Format(currentNameFormat, NicknameStore.Get());
    }

    private void SetError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}