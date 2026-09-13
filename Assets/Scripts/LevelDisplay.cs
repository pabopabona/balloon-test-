using UnityEngine;
using TMPro;

/// <summary>
/// 현재 레벨을 화면에 표시하는 UI 텍스트 컨트롤러.
/// GameManager의 OnLevelUp 이벤트를 구독해서 실시간으로 갱신하고,
/// 시작 시에는 LauncherController의 currentLevel 초기값을 그대로 보여줍니다.
/// TextMeshPro(TMP) 기준으로 작성했습니다. 일반 UI Text를 쓰신다면 아래 참고 주석을 확인하세요.
/// </summary>
public class LevelDisplay : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LauncherController launcher;

    [Header("UI")]
    public TMP_Text levelText;

    [Tooltip("텍스트 표시 형식. {0} 자리에 레벨 숫자가 들어갑니다.")]
    public string format = "Lv. {0}";

    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnLevelUp += HandleLevelUp;

        RefreshDisplay();
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int newLevel)
    {
        UpdateText(newLevel);
    }

    /// <summary>
    /// 씬 시작 시 또는 재활성화 시 현재 레벨 값을 즉시 반영합니다.
    /// </summary>
    private void RefreshDisplay()
    {
        int level = launcher != null ? launcher.currentLevel : 1;
        UpdateText(level);
    }

    private void UpdateText(int level)
    {
        if (levelText != null)
            levelText.text = string.Format(format, level);
    }
}

/*
[일반 UI Text(Legacy)를 사용하는 경우]
1. 상단의 "using TMP_Text levelText;" 대신 아래처럼 바꾸세요:
   using UnityEngine.UI;
   public Text levelText;
2. 나머지 코드는 동일하게 사용 가능합니다 (Text와 TMP_Text 둘 다 .text 프로퍼티를 가짐).
*/