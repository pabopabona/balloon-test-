using UnityEngine;

/// <summary>
/// 풍선 색깔 종류. 필요에 따라 색을 추가/삭제하세요.
/// </summary>
public enum BalloonColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Purple
}

/// <summary>
/// BalloonColor(enum)를 실제 화면에 보여줄 Color(RGB)로 변환해주는 유틸리티.
/// 나중에 실제 스프라이트로 교체하더라도, 이 매핑 테이블만 손보면 되도록 분리해뒀습니다.
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
            default: return Color.white;
        }
    }

    /// <summary>
    /// 사용 중인 색상 개수만큼 랜덤 색을 하나 뽑습니다. (다음 풍선 색 뽑기 등에 사용)
    /// </summary>
    public static BalloonColor GetRandom()
    {
        System.Array values = System.Enum.GetValues(typeof(BalloonColor));
        return (BalloonColor)values.GetValue(Random.Range(0, values.Length));
    }
}