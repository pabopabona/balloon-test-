using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 하트 개수를 하트 그림(아이콘) 또는 숫자 텍스트로 보여줍니다. 시작 화면과 게임 중 HUD 어디에든
/// 붙여서 쓸 수 있고, 인스턴스마다 독립적으로 동작합니다.
///
/// [아이콘 방식] heartSlots에 하트 Image들을 왼쪽부터 등록하면, 현재 하트 수만큼은 꽉 찬 하트로,
///   나머지는 빈 하트로 표시하고 최대 하트 수를 넘는 칸은 숨깁니다.
/// [숫자 방식] countText만 연결하면 "3 / 9" 같은 글자로 보여줍니다. (heartSlots는 비워두세요)
/// 두 방식을 같이 써도 됩니다.
///
/// 하트가 바뀌면(충전/소모/광고) 켜져 있는 동안 바로 반영됩니다.
/// </summary>
public class HeartIconDisplay : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("HeartSystem 오브젝트를 연결하세요. 비워두면 씬의 HeartManager를 자동으로 찾습니다.")]
    public HeartManager heartManager;

    [Header("아이콘 방식 (선택)")]
    [Tooltip("하트 Image들을 왼쪽부터 순서대로 등록. 숫자만 보여줄 거면 비워두세요.")]
    public Image[] heartSlots;

    [Tooltip("하트를 가지고 있는 칸에 쓸 그림 (꽉 찬 하트)")]
    public Sprite fullHeartSprite;

    [Tooltip("하트가 없는 칸에 쓸 그림 (빈 하트). 비워두면 꽉 찬 하트를 흐리게 만들어서 씁니다.")]
    public Sprite emptyHeartSprite;

    [Tooltip("빈 하트 그림이 없을 때, 꽉 찬 하트에 입힐 색(투명도를 낮춰서 흐리게)")]
    public Color emptyTint = new Color(1f, 1f, 1f, 0.3f);

    [Header("숫자 방식 (선택)")]
    [Tooltip("하트 수를 글자로도 보여줄 텍스트. 안 쓰면 비워두세요.")]
    public TMP_Text countText;

    [Tooltip("{0}=현재 하트, {1}=최대 하트")]
    public string countFormat = "{0} / {1}";

    private HeartManager subscribed;

    void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    // 씬이 시작될 때부터 켜져 있는 HUD는 OnEnable 시점에 HeartManager가 아직 준비 안 됐을 수 있어서,
    // 모든 Awake가 끝난 뒤(Start)에 한 번 더 연결하고 다시 그립니다.
    void Start()
    {
        TrySubscribe();
        Refresh();
    }

    void OnDisable()
    {
        if (subscribed != null)
        {
            subscribed.OnHeartsChanged -= HandleHeartsChanged;
            subscribed = null;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed != null) return;

        HeartManager target = heartManager != null ? heartManager : HeartManager.Instance;
        if (target == null) return;

        subscribed = target;
        subscribed.OnHeartsChanged += HandleHeartsChanged;
    }

    private void HandleHeartsChanged(int hearts, int max)
    {
        Refresh();
    }

    /// <summary>현재 하트 수에 맞게 화면을 다시 그립니다.</summary>
    public void Refresh()
    {
        if (subscribed == null) return;

        int hearts = subscribed.Hearts;
        int max = subscribed.maxHearts;

        if (countText != null)
            countText.text = string.Format(countFormat, hearts, max);

        if (heartSlots == null) return;

        for (int i = 0; i < heartSlots.Length; i++)
        {
            Image slot = heartSlots[i];
            if (slot == null) continue;

            // 최대 하트 수를 넘는 칸은 숨김
            bool withinMax = i < max;
            slot.gameObject.SetActive(withinMax);
            if (!withinMax) continue;

            bool filled = i < hearts;

            if (filled)
            {
                slot.sprite = fullHeartSprite;
                slot.color = Color.white;
            }
            else if (emptyHeartSprite != null)
            {
                slot.sprite = emptyHeartSprite;
                slot.color = Color.white;
            }
            else
            {
                slot.sprite = fullHeartSprite;
                slot.color = emptyTint;
            }
        }
    }
}