using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 테스트용 가짜 보상형 광고. 실제 광고 대신 전체 화면 패널을 몇 초간 보여주고,
/// 끝나면 보상 콜백을 호출합니다. 패널의 "닫기" 버튼을 중간에 누르면 보상 없이 닫혀서
/// "광고를 끝까지 안 봤을 때"의 동작도 테스트할 수 있습니다.
///
/// 이 스크립트는 항상 켜져 있는 오브젝트에 붙이세요(패널 자체에 붙이면 패널이 꺼진 동안
/// 동작하지 못합니다).
/// </summary>
public class FakeRewardedADService : RewardedADService
{
    [Header("가짜 광고 UI")]
    [Tooltip("광고 중에 화면 전체를 덮을 패널 (평소엔 비활성화 상태로 두세요)")]
    public GameObject fakeAdPanel;

    [Tooltip("남은 초를 보여줄 텍스트 (선택)")]
    public TMP_Text countdownText;

    [Header("설정")]
    [Tooltip("가짜 광고가 지속되는 시간(초)")]
    public float adDuration = 3f;

    private bool showing;
    private Action rewardedCallback;
    private Action closedCallback;
    private Coroutine routine;

    public override bool IsReady => !showing;

    void Awake()
    {
        if (fakeAdPanel != null) fakeAdPanel.SetActive(false);
    }

    public override void Show(Action onRewarded, Action onClosedWithoutReward)
    {
        if (showing)
        {
            onClosedWithoutReward?.Invoke();
            return;
        }

        showing = true;
        rewardedCallback = onRewarded;
        closedCallback = onClosedWithoutReward;

        if (fakeAdPanel != null) fakeAdPanel.SetActive(true);
        routine = StartCoroutine(PlayFakeAd());
    }

    private IEnumerator PlayFakeAd()
    {
        float remaining = adDuration;

        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        Finish(true);
    }

    /// <summary>패널의 "닫기" 버튼 On Click()에 연결하세요. 보상 없이 광고를 닫습니다.</summary>
    public void OnCloseEarlyPressed()
    {
        if (!showing) return;

        if (routine != null) StopCoroutine(routine);
        Finish(false);
    }

    private void Finish(bool rewarded)
    {
        showing = false;
        if (fakeAdPanel != null) fakeAdPanel.SetActive(false);

        Action onRewarded = rewardedCallback;
        Action onClosed = closedCallback;
        rewardedCallback = null;
        closedCallback = null;

        if (rewarded) onRewarded?.Invoke();
        else onClosed?.Invoke();
    }
}
