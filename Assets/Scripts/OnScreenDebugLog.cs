using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 화면 안에 직접 로그를 띄워주는 간단한 디버그 콘솔.
/// PC 연결 없이 폰만으로도 Debug.Log/Warning/Error 내용을 바로 확인할 수 있습니다.
///
/// 사용법: 화면 좌측 상단을 손가락으로 3번 연속 탭하면 로그 창이 켜지고 꺼집니다.
/// Development Build가 아니어도 동작하지만, 실제 배포용 빌드에는 포함하지 않는 걸 권장합니다.
/// </summary>
public class OnScreenDebugLog : MonoBehaviour
{
    [Tooltip("화면에 최대 몇 줄까지 보여줄지")]
    public int maxLines = 25;

    [Tooltip("시작할 때부터 로그 창을 켜둘지 여부")]
    public bool visibleOnStart = true;

    private readonly List<string> logLines = new List<string>();
    private bool isVisible;
    private Vector2 scrollPosition;

    // 좌측 상단 3연속 탭 감지용
    private int tapCount = 0;
    private float lastTapTime = 0f;
    private const float tapWindow = 0.5f; // 이 시간(초) 안에 3번 탭해야 인정
    private readonly Rect tapZone = new Rect(0, 0, 200, 200); // 좌측 상단 200x200 영역

    void Awake()
    {
        isVisible = visibleOnStart;
    }

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error => "<color=#ff5555>[ERROR]</color> ",
            LogType.Exception => "<color=#ff5555>[EXCEPTION]</color> ",
            LogType.Warning => "<color=#ffcc55>[WARN]</color> ",
            _ => ""
        };

        logLines.Add(prefix + logString);

        if (logLines.Count > maxLines)
        {
            logLines.RemoveAt(0);
        }

        scrollPosition = new Vector2(0, float.MaxValue); // 항상 최신 로그로 자동 스크롤
    }

    void Update()
    {
        // 화면 좌측 상단 탭으로 로그 창 토글 (마우스 클릭도 같이 지원, 에디터 테스트용)
        bool tappedInZone = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) && tapZone.Contains(Input.mousePosition))
        {
            tappedInZone = true;
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector2 touchPos = Input.GetTouch(0).position;
            // 터치 좌표는 아래가 0이므로, 화면 좌표계에 맞게 Y를 뒤집어서 비교
            Vector2 flipped = new Vector2(touchPos.x, Screen.height - touchPos.y);
            if (tapZone.Contains(flipped))
            {
                tappedInZone = true;
            }
        }
#endif

        if (tappedInZone)
        {
            if (Time.unscaledTime - lastTapTime > tapWindow)
            {
                tapCount = 0;
            }

            tapCount++;
            lastTapTime = Time.unscaledTime;

            if (tapCount >= 3)
            {
                isVisible = !isVisible;
                tapCount = 0;
            }
        }
    }

    void OnGUI()
    {
        if (!isVisible) return;

        float width = Screen.width;
        float height = Screen.height * 0.5f;

        GUI.Box(new Rect(0, 0, width, height), "");

        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = Mathf.Max(20, Screen.width / 45),
            wordWrap = true
        };

        StringBuilder sb = new StringBuilder();
        foreach (string line in logLines)
        {
            sb.AppendLine(line);
        }

        Rect viewRect = new Rect(10, 10, width - 20, height - 20);
        scrollPosition = GUI.BeginScrollView(
            viewRect, scrollPosition,
            new Rect(0, 0, width - 40, Mathf.Max(height, textStyle.CalcHeight(new GUIContent(sb.ToString()), width - 40)))
        );

        GUI.Label(new Rect(0, 0, width - 40, 10000), sb.ToString(), textStyle);

        GUI.EndScrollView();
    }
}