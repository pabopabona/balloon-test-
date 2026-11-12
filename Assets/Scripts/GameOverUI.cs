using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// GameManager의 OnGameOver 이벤트를 구독해서 게임오버 패널을 띄우는 스크립트.
/// 패널은 평소에는 비활성화 상태로 두고, 게임오버 시에만 활성화됩니다.
/// 재시작 버튼과 연결할 RestartGame() 메서드도 포함되어 있습니다.
///
/// 게임오버가 된 뒤 Show Delay(초)만큼 기다렸다가 패널을 띄웁니다.
/// 패널에 Canvas Group이 있으면 Fade Duration 동안 서서히 나타납니다.
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

    [Header("띄우는 타이밍")]
    [Tooltip("게임오버가 된 뒤 게임오버 화면이 뜨기까지 기다리는 시간(초). 0이면 바로 뜹니다.")]
    public float showDelay = 1.0f;

    [Tooltip("게임오버 화면이 서서히 나타나는 시간(초). 패널에 Canvas Group이 있을 때만 적용됩니다. 0이면 한 번에 나타납니다.")]
    public float fadeDuration = 0.3f;

    /// <summary>게임오버 패널이 실제로 화면에 뜬 순간 호출됩니다. (GameOverRating이 사용)</summary>
    public event Action OnPanelShown;

    private Coroutine showRoutine;

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
        if (showRoutine != null) return; // 게임오버가 두 번 들어와도 한 번만 띄움
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // 게임 시간이 멈춰 있어도 흐르도록 실제 시간으로 기다림
        if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);

        if (messageText != null)
        {
            // 사유별 문구를 다르게 보여주고 싶다면 여기서 reason 값에 따라 분기하면 됩니다.
            messageText.text = "Game Over";
        }

        CanvasGroup group = gameOverPanel != null ? gameOverPanel.GetComponent<CanvasGroup>() : null;
        if (group != null && fadeDuration > 0f)
        {
            group.alpha = 0f;
            group.interactable = false;   // 다 뜨기 전에는 Restart 버튼이 눌리지 않음
            group.blocksRaycasts = false;
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        OnPanelShown?.Invoke(); // 등급 문구, 폭죽 등이 이때 시작됩니다.

        if (group != null && fadeDuration > 0f)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// 재시작 버튼의 OnClick에 이 메서드를 연결하세요.
    /// 현재 씬을 다시 로드해서 모든 게임 상태를 처음으로 되돌립니다.
    /// </summary>
    public void RestartGame()
    {
        // 재시작이라는 표시를 남겨서, 다음 씬 로드에서 로딩/닉네임 입력을 건너뛰게 합니다.
        SessionState.IsRestart = true;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}