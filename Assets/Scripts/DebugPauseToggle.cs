using UnityEngine;

/// <summary>
/// 화면 우측 상단을 3번 연속 탭하면 게임을 그 순간 그대로 멈추는(Time.timeScale = 0)
/// 디버그용 일시정지 토글. 버그(풍선 겹침, 밀려났다 되돌아오는 현상 등)를 발견한 즉시
/// 멈춰서 스크린샷을 찍을 수 있게 해줍니다.
///
/// OnScreenDebugLog(좌측 상단 3탭으로 로그 창 토글)와 짝을 이루도록 설계했습니다.
/// 일시정지 중에도 터치 감지 자체는 계속 살아있어서, 로그 창을 열거나 다시 재개할 수 있습니다.
/// </summary>
public class DebugPauseToggle : MonoBehaviour
{
    private bool isPaused = false;
    private float savedTimeScale = 1f;

    private int tapCount = 0;
    private float lastTapTime = 0f;
    private const float tapWindow = 0.5f;
    private const float tapZoneSize = 200f;

    void Update()
    {
        Rect tapZone = new Rect(Screen.width - tapZoneSize, 0, tapZoneSize, tapZoneSize);
        bool tapped = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) && tapZone.Contains(Input.mousePosition))
        {
            tapped = true;
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector2 touchPos = Input.GetTouch(0).position;
            Vector2 flipped = new Vector2(touchPos.x, Screen.height - touchPos.y);
            if (tapZone.Contains(flipped))
            {
                tapped = true;
            }
        }
#endif

        if (tapped)
        {
            if (Time.unscaledTime - lastTapTime > tapWindow)
            {
                tapCount = 0;
            }

            tapCount++;
            lastTapTime = Time.unscaledTime;

            if (tapCount >= 3)
            {
                TogglePause();
                tapCount = 0;
            }
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = savedTimeScale;
        }
    }

    void OnGUI()
    {
        if (!isPaused) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.Max(28, Screen.width / 25),
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.red;

        float boxWidth = Screen.width * 0.6f;
        GUI.Box(new Rect((Screen.width - boxWidth) / 2f, 20, boxWidth, 70), "⏸ PAUSED (우측 상단 3탭으로 재개)", style);
    }
}