using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HexGridManager의 OnComboStep 이벤트를 구독해서, 연쇄 반응(콤보)이 2단계 이상 이어질 때
/// COMBO 이미지를 잠깐 띄우고 자동으로 사라지게 하는 UI 스크립트.
/// </summary>
public class ComboUI : MonoBehaviour
{
    [Header("참조")]
    public HexGridManager gridManager;

    [Header("UI")]
    [Tooltip("콤보 시 표시할 이미지 (COMBO 그래픽 스프라이트가 연결된 Image 컴포넌트)")]
    public Image comboImage;

    [Tooltip("표시/숨김을 제어할 대상. 비워두면 comboImage 오브젝트 자체를 켜고 끕니다.")]
    public GameObject comboRoot;

    [Header("콤보 숫자 표시 (선택 사항)")]
    [Tooltip("몇 콤보인지 숫자로 보여줄 텍스트. 비워두면 숫자 표시 없이 이미지만 뜹니다.")]
    public TMP_Text comboCountText;

    [Tooltip("숫자 표시 형식. {0} 자리에 연쇄 단계 숫자가 들어갑니다.")]
    public string countFormat = "x{0}";

    [Header("설정")]
    [Tooltip("한 번 뜬 콤보 이미지가 화면에 유지되는 시간(초)")]
    public float displayDuration = 0.8f;

    private Coroutine hideCoroutine;

    void OnEnable()
    {
        if (gridManager != null)
            gridManager.OnComboStep += HandleComboStep;

        SetVisible(false);
    }

    void OnDisable()
    {
        if (gridManager != null)
            gridManager.OnComboStep -= HandleComboStep;
    }

    private void HandleComboStep(int comboDepth)
    {
        // 1단계(최초 매칭)는 콤보로 치지 않고, 2단계 이상(연쇄로 이어진 경우)만 표시
        if (comboDepth < 2) return;

        if (comboCountText != null)
            comboCountText.text = string.Format(countFormat, comboDepth);

        SetVisible(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        SetVisible(false);
        hideCoroutine = null;
    }

    private void SetVisible(bool visible)
    {
        if (comboRoot != null)
            comboRoot.SetActive(visible);
        else if (comboImage != null)
            comboImage.gameObject.SetActive(visible);

        if (comboCountText != null)
            comboCountText.gameObject.SetActive(visible);
    }
}