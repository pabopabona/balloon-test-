using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨이 오를 때마다 배경 조각을 하나씩 켜주는 스크립트.
///
///   Lv. 5  → 조각 1     Lv. 10 → 조각 2     Lv. 15 → 조각 3
///   Lv. 20 → 조각 4     Lv. 25 → 조각 5
///
/// - 조각 수와 레벨은 인스펙터의 Pieces 목록에서 자유롭게 늘리고 바꿀 수 있습니다.
/// - 조각은 SpriteRenderer(월드 배경)든 Image(UI 배경)든 상관없습니다.
/// - Fade Duration 동안 서서히 나타납니다. (0이면 바로 켜짐)
/// - 게임 시작 시(재시작 포함) 아직 레벨이 안 된 조각은 모두 꺼진 상태로 시작합니다.
///
/// 항상 켜져 있는 오브젝트(예: GridBackground나 GameManager)에 붙이세요.
/// </summary>
public class BackgroundUnlocker : MonoBehaviour
{
    [System.Serializable]
    public class BackgroundPiece
    {
        [Tooltip("이 레벨에 도달하면 켜집니다.")]
        public int unlockLevel = 5;

        [Tooltip("켜질 배경 조각 오브젝트")]
        public GameObject piece;

        [System.NonSerialized] public bool unlocked;
        [System.NonSerialized] public SpriteRenderer[] sprites;
        [System.NonSerialized] public Color[] spriteColors;
        [System.NonSerialized] public Graphic[] graphics;
        [System.NonSerialized] public Color[] graphicColors;
    }

    [Header("참조")]
    [Tooltip("현재 레벨을 읽어올 발사대")]
    public LauncherController launcher;

    [Header("배경 조각 (레벨이 낮은 순서대로)")]
    public List<BackgroundPiece> pieces = new List<BackgroundPiece>
    {
        new BackgroundPiece { unlockLevel = 5 },
        new BackgroundPiece { unlockLevel = 10 },
        new BackgroundPiece { unlockLevel = 15 },
        new BackgroundPiece { unlockLevel = 20 },
        new BackgroundPiece { unlockLevel = 25 },
    };

    [Header("연출")]
    [Tooltip("조각이 서서히 나타나는 시간(초). 0이면 바로 켜집니다.")]
    public float fadeDuration = 1.0f;

    [Tooltip("조각이 켜질 때 재생할 효과음 (선택)")]
    public AudioClip unlockClip;

    private int lastCheckedLevel = -1;

    void Awake()
    {
        // 원래 색(투명도)을 기억해두고, 시작할 때는 모두 꺼둡니다.
        foreach (BackgroundPiece p in pieces)
        {
            if (p.piece == null) continue;

            p.sprites = p.piece.GetComponentsInChildren<SpriteRenderer>(true);
            p.spriteColors = new Color[p.sprites.Length];
            for (int i = 0; i < p.sprites.Length; i++) p.spriteColors[i] = p.sprites[i].color;

            p.graphics = p.piece.GetComponentsInChildren<Graphic>(true);
            p.graphicColors = new Color[p.graphics.Length];
            for (int i = 0; i < p.graphics.Length; i++) p.graphicColors[i] = p.graphics[i].color;

            p.unlocked = false;
            p.piece.SetActive(false);
        }
    }

    void Update()
    {
        if (launcher == null) return;

        int level = launcher.currentLevel;
        if (level == lastCheckedLevel) return; // 레벨이 바뀔 때만 확인
        lastCheckedLevel = level;

        foreach (BackgroundPiece p in pieces)
        {
            if (p.unlocked || p.piece == null) continue;
            if (level >= p.unlockLevel) Unlock(p, true);
        }
    }

    private void Unlock(BackgroundPiece p, bool animate)
    {
        p.unlocked = true;
        p.piece.SetActive(true);

        if (animate && fadeDuration > 0f)
            StartCoroutine(FadeIn(p));
        else
            SetAlpha(p, 1f);

        if (animate && unlockClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayClip(unlockClip);
    }

    private IEnumerator FadeIn(BackgroundPiece p)
    {
        float t = 0f;
        SetAlpha(p, 0f);
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // 스킬 연출로 게임이 멈춰도 자연스럽게 진행
            SetAlpha(p, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(p, 1f);
    }

    /// <summary>조각 안의 모든 스프라이트/이미지를 원래 투명도 × k 로 맞춥니다.</summary>
    private void SetAlpha(BackgroundPiece p, float k)
    {
        if (p.sprites != null)
            for (int i = 0; i < p.sprites.Length; i++)
            {
                if (p.sprites[i] == null) continue;
                Color c = p.spriteColors[i];
                c.a *= k;
                p.sprites[i].color = c;
            }

        if (p.graphics != null)
            for (int i = 0; i < p.graphics.Length; i++)
            {
                if (p.graphics[i] == null) continue;
                Color c = p.graphicColors[i];
                c.a *= k;
                p.graphics[i].color = c;
            }
    }

    // ───────────── 테스트용 (Play 중 컴포넌트 ⋮ 메뉴) ─────────────

    [ContextMenu("TEST: 다음 조각 켜기")]
    private void TestNext()
    {
        foreach (BackgroundPiece p in pieces)
        {
            if (p.unlocked || p.piece == null) continue;
            Unlock(p, true);
            return;
        }
    }

    [ContextMenu("TEST: 모든 조각 켜기")]
    private void TestAll()
    {
        foreach (BackgroundPiece p in pieces)
            if (!p.unlocked && p.piece != null) Unlock(p, false);
    }

    [ContextMenu("TEST: 모든 조각 끄기")]
    private void TestReset()
    {
        StopAllCoroutines();
        foreach (BackgroundPiece p in pieces)
        {
            if (p.piece == null) continue;
            p.unlocked = false;
            SetAlpha(p, 1f);
            p.piece.SetActive(false);
        }
        // 지금 레벨로 바로 다시 켜지지 않도록, 다음 레벨업부터 다시 확인
        lastCheckedLevel = launcher != null ? launcher.currentLevel : -1;
    }
}
