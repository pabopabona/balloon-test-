using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 글자가 그려진 이미지(배너, 로고 등)를 한국어 / 영어 이미지로 바꿔줍니다.
/// UI Image와 SpriteRenderer 둘 다 됩니다.
///
/// 사용법
///   1) 이미지 오브젝트에 이 컴포넌트를 붙입니다. → 지금 이미지가 Korean Sprite 칸에 자동으로 들어갑니다.
///   2) English Sprite 칸에 영어 이미지를 넣습니다. (비워두면 한국어 이미지 그대로)
/// </summary>
public class LocalizedImage : MonoBehaviour
{
    public Sprite koreanSprite;
    public Sprite englishSprite;

    [Tooltip("체크하면 이미지를 바꿀 때 원본 비율 크기(Set Native Size)로 맞춥니다. 두 이미지 크기가 다를 때만 사용")]
    public bool setNativeSize = false;

    private Image image;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        Cache();
        if (koreanSprite == null) koreanSprite = CurrentSprite();
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

    public void Apply()
    {
        Cache();

        Sprite target = (Loc.Current == GameLanguage.English && englishSprite != null) ? englishSprite : koreanSprite;
        if (target == null) return;

        if (image != null)
        {
            image.sprite = target;
            if (setNativeSize) image.SetNativeSize();
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.sprite = target;
        }
    }

    private void Cache()
    {
        if (image == null) image = GetComponent<Image>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private Sprite CurrentSprite()
    {
        if (image != null) return image.sprite;
        if (spriteRenderer != null) return spriteRenderer.sprite;
        return null;
    }

    void Reset()
    {
        Cache();
        koreanSprite = CurrentSprite();
    }
}
