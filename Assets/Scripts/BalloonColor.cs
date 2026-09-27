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
    Gray
}

/// <summary>
/// BalloonColor(enum)를 실제 화면에 보여줄 Color(RGB)로 변환해주는 유틸리티.
/// 이 색은 실제 이미지 스프라이트가 연결 안 됐을 때만 사용되는 "임시 틴트" 색입니다.
/// </summary>
public static class BalloonColorUtil
{
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
            default: return Color.white;
        }
    }

    /// <summary>
    /// 전체 색상 중 완전히 무작위로 하나 뽑습니다. (레벨 제한 없이) - 안전장치/폴백용으로만 사용하세요.
    /// 실제 게임에서는 GameManager.GetRandomColorForLevel()을 사용해서 레벨에 맞는 색만 뽑아야 합니다.
    /// </summary>
    public static BalloonColor GetRandom()
    {
        System.Array values = System.Enum.GetValues(typeof(BalloonColor));
        return (BalloonColor)values.GetValue(Random.Range(0, values.Length));
    }
}