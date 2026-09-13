using UnityEngine;
using TMPro;

/// <summary>
/// 현재 점수를 화면에 표시하는 UI 텍스트 컨트롤러.
/// GameManager의 OnScoreChanged 이벤트를 구독해서 실시간으로 갱신합니다.
/// TextMeshPro(TMP) 기준으로 작성했습니다.
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;

    [Header("UI")]
    public TMP_Text scoreText;

    [Tooltip("텍스트 표시 형식. {0} 자리에 점수 숫자가 들어갑니다.")]
    public string format = "Score: {0}";

    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnScoreChanged += HandleScoreChanged;

        RefreshDisplay();
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int newScore)
    {
        UpdateText(newScore);
    }

    private void RefreshDisplay()
    {
        int score = gameManager != null ? gameManager.Score : 0;
        UpdateText(score);
    }

    private void UpdateText(int score)
    {
        if (scoreText != null)
            scoreText.text = string.Format(format, score);
    }
}

/*
[일반 UI Text(Legacy)를 사용하는 경우]
1. 상단의 "using TMP_Text scoreText;" 대신 아래처럼 바꾸세요:
   using UnityEngine.UI;
   public Text scoreText;
2. 나머지 코드는 동일하게 사용 가능합니다.
*/