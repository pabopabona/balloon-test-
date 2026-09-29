using System;
using UnityEngine;

/// <summary>
/// 보상형 광고를 보여주는 서비스의 공통 껍데기(추상 클래스).
/// 지금은 FakeRewardedAdService(가짜 광고)가 이걸 구현하고 있고, 나중에 Unity Ads 같은
/// 실제 SDK용 클래스를 이 클래스를 상속해서 만들면, 하트/시작 화면 쪽 코드는 그대로 두고
/// Inspector에서 연결만 바꿔주면 됩니다.
/// </summary>
public abstract class RewardedADService : MonoBehaviour
{
    /// <summary>지금 광고를 보여줄 수 있는 상태인지.</summary>
    public abstract bool IsReady { get; }

    /// <summary>
    /// 광고를 보여줍니다.
    /// onRewarded: 광고를 끝까지 봐서 보상을 줘야 할 때 호출
    /// onClosedWithoutReward: 보상 없이 닫혔거나 실패했을 때 호출
    /// </summary>
    public abstract void Show(Action onRewarded, Action onClosedWithoutReward);
}