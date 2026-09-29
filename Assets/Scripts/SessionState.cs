/// <summary>
/// 씬을 다시 불러와도(재시작) 유지되어야 하는 "이번 앱 실행 동안만의" 상태.
/// static 변수는 SceneManager.LoadScene으로 씬을 다시 불러와도 지워지지 않고,
/// 앱을 완전히 종료했다가 다시 켜면 자동으로 초기값으로 돌아갑니다.
/// </summary>
public static class SessionState
{
    /// <summary>
    /// true면 이번 씬 로드는 "게임오버 후 재시작"입니다. 로딩 화면과 닉네임 입력을 건너뛰고
    /// 바로 시작 화면(하트 확인)으로 가기 위해 사용합니다. 한 번 사용하면 LoadingScreenController가 꺼줍니다.
    /// </summary>
    public static bool IsRestart;
}
