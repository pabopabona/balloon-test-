using UnityEngine;

/// <summary>
/// 풍선 색깔 종류. 필요에 따라 색을 추가/삭제하세요.
/// 순서를 바꾸면 Balloon/BalloonSpawner의 스프라이트 배열 인덱스도 같이 밀리니 주의하세요.
/// </summary>
public enum BalloonColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Purple,
    Rainbow,
    Gray,
    Brick   // 벽돌: 매칭되지 않고, 회색(번개)과 레인보우 폭발로만 깨짐. 반드시 맨 뒤에 두세요(기존 값이 밀리지 않도록).
}

/// <summary>
/// BalloonColor(enum)를 실제 화면에 보여줄 Color(RGB)로 변환해주는 유틸리티.
/// 이 색은 실제 이미지 스프라이트가 연결 안 됐을 때만 사용되는 "임시 틴트" 색입니다.
/// </summary>
public static class BalloonColorUtil
{
    /// <summary>매칭(같은 색 3개)이 되지 않는 풍선인지. 지금은 벽돌만 해당합니다.</summary>
    public static bool IsUnmatchable(BalloonColor color) => color == BalloonColor.Brick;

    /// <summary>밀려나지 않는 풍선인지. 지금은 벽돌만 해당합니다.</summary>
    public static bool IsImmovable(BalloonColor color) => color == BalloonColor.Brick;

    public static Color ToColor(BalloonColor color)
    {
        switch (color)
        {
            case BalloonColor.Red: return new Color(0.90f, 0.25f, 0.25f);
            case BalloonColor.Blue: return new Color(0.25f, 0.45f, 0.95f);
            case BalloonColor.Green: return new Color(0.30f, 0.80f, 0.35f);
            case BalloonColor.Yellow: return new Color(0.95f, 0.85f, 0.20f);
            case BalloonColor.Purple: return new Color(0.65f, 0.35f, 0.85f);
            case BalloonColor.Rainbow: return new Color(1.00f, 0.45f, 0.75f); // 실제 무지개 이미지 연결 전까지의 임시 색
            case BalloonColor.Gray: return new Color(0.60f, 0.60f, 0.60f);
            case BalloonColor.Brick: return new Color(0.62f, 0.33f, 0.22f); // 실제 벽돌 이미지 연결 전까지의 임시 색
            default: return Color.white;
        }
    }

    /// <summary>
    /// 전체 색상 중 완전히 무작위로 하나 뽑습니다. (레벨 제한 없이) - 안전장치/폴백용으로만 사용하세요.
    /// 실제 게임에서는 GameManager.GetRandomColorForLevel()을 사용해서 레벨에 맞는 색만 뽑아야 합니다.
    /// </summary>
    public static BalloonColor GetRandom()
    {
        // 안전장치용이므로 벽돌은 제외합니다(벽돌은 레벨 규칙으로만 등장).
        System.Array values = System.Enum.GetValues(typeof(BalloonColor));
        BalloonColor picked;
        do
        {
            picked = (BalloonColor)values.GetValue(Random.Range(0, values.Length));
        } while (picked == BalloonColor.Brick);
        return picked;
    }
}