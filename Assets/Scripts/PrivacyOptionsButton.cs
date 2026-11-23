using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "개인정보 설정" 버튼. 유럽 등 동의가 필요한 지역의 유저에게만 보이고,
/// 누르면 광고 동의 선택을 다시 할 수 있는 Google 창이 열립니다. (GDPR 필수 항목)
///
/// 한국, 미국 유저에게는 버튼이 자동으로 숨겨집니다.
/// 시작 화면 구석(예: 버전 표시 근처)의 작은 버튼에 붙이세요.
/// 버튼 글자는 LocalizedText로 "개인정보 설정" / "Privacy Settings"를 넣으면 됩니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class PrivacyOptionsButton : MonoBehaviour
{
    [Tooltip("숨길 범위. 비워두면 이 버튼(과 자식 글자)의 그래픽을 숨깁니다. 버튼 옆 아이콘 등까지 함께 숨기려면 그 묶음 부모를 넣으세요.")]
    public GameObject visualRoot;

    private Button button;
    private bool lastVisible = true;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    void OnEnable()
    {
        Refresh(true);
    }

    void Update()
    {
        // 동의 정보는 앱 시작 후 잠시 뒤에 확인되므로 계속 따라갑니다. (값이 바뀔 때만 실제로 갱신)
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        bool visible = ConsentManager.IsPrivacyOptionsRequired;
        if (!force && visible == lastVisible) return;
        lastVisible = visible;

        // 오브젝트를 꺼버리면 Update가 멈춰서 다시 켤 수 없으므로, 그래픽과 클릭만 끄고 켭니다.
        GameObject target = visualRoot != null ? visualRoot : gameObject;
        foreach (Graphic g in target.GetComponentsInChildren<Graphic>(true)) g.enabled = visible;
        button.interactable = visible;
    }

    private void OnClicked()
    {
        button.interactable = false;
        ConsentManager.ShowPrivacyOptions(() =>
        {
            if (this != null && button != null) button.interactable = true;
        });
    }
}
