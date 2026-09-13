using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// GameManager의 OnGameOver 이벤트를 구독해서 게임오버 패널을 띄우는 스크립트.
/// 패널은 평소에는 비활성화 상태로 두고, 게임오버 시에만 활성화됩니다.
/// 재시작 버튼과 연결할 RestartGame() 메서드도 포함되어 있습니다.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;

    [Header("UI")]
    [Tooltip("게임오버 시 활성화할 패널 오브젝트 (평소엔 비활성화 상태로 두세요)")]
    public GameObject gameOverPanel;

    [Tooltip("게임오버 사유/문구를 보여줄 텍스트 (선택 사항)")]
    public TMP_Text messageText;

    void OnEnable()
    {
        if (gameManager != null)
            gameManager.OnGameOver += HandleGameOver;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver(string reason)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (messageText != null)
        {
            // 사유별 문구를 다르게 보여주고 싶다면 여기서 reason 값에 따라 분기하면 됩니다.
            messageText.text = "Game Over";
        }
    }

    /// <summary>
    /// 재시작 버튼의 OnClick에 이 메서드를 연결하세요.
    /// 현재 씬을 다시 로드해서 모든 게임 상태를 처음으로 되돌립니다.
    /// </summary>
    public void RestartGame()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}