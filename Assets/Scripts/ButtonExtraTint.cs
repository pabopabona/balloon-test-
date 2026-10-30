using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼이 눌리거나 비활성화될 때, 버튼 배경(Target Graphic)뿐 아니라
/// 버튼 위에 올린 아이콘 이미지·텍스트 등도 같은 색으로 함께 바뀌게 해줍니다.
///
/// Unity 기본 Button은 Target Graphic 한 개만 색이 바뀌기 때문에, 이 스크립트를
/// 버튼 오브젝트에 같이 붙이고 Extra Graphics에 아이콘을 넣으면 됩니다.
/// 색은 기본적으로 Button의 Colors 설정(Normal / Pressed / Disabled 등)을 그대로 따라갑니다.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class ButtonExtraTint : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("버튼과 함께 색이 바뀔 이미지/텍스트들 (예: 버튼 위 아이콘)")]
    public Graphic[] extraGraphics;

    [Tooltip("체크하면 Button 컴포넌트의 Colors 설정을 그대로 씁니다. 끄면 아래 Custom Colors를 씁니다.")]
    public bool useButtonColors = true;

    public ColorBlock customColors = ColorBlock.defaultColorBlock;

    private Selectable selectable;
    private bool isPointerInside;
    private bool isPointerDown;

    private enum State { Normal, Highlighted, Pressed, Disabled }
    private State currentState = (State)(-1);

    void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    void OnEnable()
    {
        isPointerInside = false;
        isPointerDown = false;
        currentState = (State)(-1);
        Refresh(true);
    }

    void Update()
    {
        // interactable이 코드로 바뀌는 경우(광고 준비 전 비활성화 등)도 따라가도록 매 프레임 확인
        Refresh(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        isPointerDown = true;
        Refresh(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        isPointerDown = false;
        Refresh(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        Refresh(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        Refresh(false);
    }

    private void Refresh(bool instant)
    {
        State state;
        if (selectable != null && !selectable.IsInteractable()) state = State.Disabled;
        else if (isPointerDown && isPointerInside) state = State.Pressed;
        else if (isPointerInside) state = State.Highlighted;
        else state = State.Normal;

        if (state == currentState) return;
        currentState = state;

        ColorBlock colors = (useButtonColors && selectable != null) ? selectable.colors : customColors;

        Color tint;
        switch (state)
        {
            case State.Pressed: tint = colors.pressedColor; break;
            case State.Highlighted: tint = colors.highlightedColor; break;
            case State.Disabled: tint = colors.disabledColor; break;
            default: tint = colors.normalColor; break;
        }
        tint *= colors.colorMultiplier;

        float duration = instant ? 0f : colors.fadeDuration;

        if (extraGraphics == null) return;
        foreach (Graphic g in extraGraphics)
        {
            // Button과 같은 방식(CrossFadeColor)이라, 이미지의 원래 색에 곱해져서 원래 색감은 유지됩니다.
            if (g != null) g.CrossFadeColor(tint, duration, true, true);
        }
    }
}