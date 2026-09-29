using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// 구글 AdMob 보상형 광고. FakeRewardedADService와 같은 RewardedADService를 상속하므로,
/// 하트 쪽에서 광고 서비스 참조만 이 컴포넌트로 바꿔주면 나머지 코드는 그대로 동작합니다.
///
/// 동작 흐름
///   1) Start에서 AdMob SDK 초기화 → 끝나면 광고 1개를 미리 로드
///   2) Show() 호출 → 미리 받아둔 광고를 전체 화면으로 표시
///   3) 끝까지 시청하면 onRewarded, 중간에 닫거나 실패하면 onClosedWithoutReward
///   4) 광고가 닫히면 다음 광고를 바로 다시 로드 (다음 버튼 클릭 때 기다리지 않도록)
///
/// 광고 단위 ID 기본값은 구글 공식 "테스트 ID"라 눌러도 수익·정지 위험이 없습니다.
/// 출시 직전에만 본인 AdMob 광고 단위 ID로 교체하세요.
///
/// FakeRewardedADService처럼 항상 켜져 있는 오브젝트에 붙이세요.
/// </summary>
public class AdMobRewardedADService : RewardedADService
{
    [Header("광고 단위 ID (기본값 = 구글 공식 테스트 ID)")]
    public string androidAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    public string iosAdUnitId = "ca-app-pub-3940256099942544/1712485313";

    [Header("테스트 기기 ID (실제 광고 ID로 바꾼 뒤에 사용)")]
    [Tooltip("실제 광고 ID로 개발할 때, 여기 등록한 기기에는 테스트 광고만 나옵니다. " +
             "기기 ID는 앱 실행 후 Logcat에서 'test device' 문구로 검색하면 찾을 수 있습니다.")]
    public string[] testDeviceIds;

    [Header("모바일이 아닐 때 대신 쓸 광고 (윈도우 빌드용)")]
    [Tooltip("AdMob은 안드로이드/iOS에서만 동작합니다. 윈도우 밸런스 테스트 빌드에서는 " +
             "여기 연결한 FakeRewardedADService가 대신 동작합니다.")]
    public RewardedADService nonMobileFallback;

    [Header("로드 실패 시 재시도 간격(초)")]
    public float retryDelay = 10f;

    // SDK 초기화는 앱 실행 중 한 번이면 충분합니다. Restart로 씬이 다시 로드되어도
    // 이 값은 static이라 유지되므로 중복 초기화를 하지 않습니다.
    private static bool sdkInitialized = false;

    private RewardedAd rewardedAd;
    private bool isLoading;
    private bool showing;

    private bool UseFallback
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return false;
#else
            return nonMobileFallback != null;
#endif
        }
    }

    private string AdUnitId
    {
        get
        {
#if UNITY_IOS
            return iosAdUnitId;
#else
            return androidAdUnitId;
#endif
        }
    }

    public override bool IsReady
    {
        get
        {
            if (UseFallback) return nonMobileFallback.IsReady;
            return !showing && rewardedAd != null && rewardedAd.CanShowAd();
        }
    }

    void Start()
    {
        if (UseFallback) return;

        if (sdkInitialized)
        {
            LoadAd();
            return;
        }

        // 광고 이벤트를 유니티 메인 스레드에서 받도록 설정.
        // 이게 없으면 콜백 안에서 UI/하트를 건드릴 때 크래시가 날 수 있습니다.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        if (testDeviceIds != null && testDeviceIds.Length > 0)
        {
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TestDeviceIds = new List<string>(testDeviceIds)
            });
        }

        MobileAds.Initialize(status =>
        {
            sdkInitialized = true;
            Debug.Log("[AdMob] SDK 초기화 완료");
            if (this != null) LoadAd();   // 그 사이 씬이 바뀌어 파괴됐으면 건너뜀
        });
    }

    void OnDestroy()
    {
        CancelInvoke();
        rewardedAd?.Destroy();
        rewardedAd = null;
    }

    // ───────────── 광고 로드 ─────────────

    private void LoadAd()
    {
        if (isLoading) return;

        rewardedAd?.Destroy();
        rewardedAd = null;
        isLoading = true;

        Debug.Log($"[AdMob] 광고 로드 시작: {AdUnitId}");

        RewardedAd.Load(AdUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (this == null) { ad?.Destroy(); return; }
            isLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdMob] 광고 로드 실패: {error}\n{retryDelay}초 후 다시 시도합니다.");
                Invoke(nameof(LoadAd), retryDelay);
                return;
            }

            Debug.Log("[AdMob] 광고 로드 성공 (표시 준비 완료)");
            rewardedAd = ad;
        });
    }

    // ───────────── 광고 표시 ─────────────

    public override void Show(Action onRewarded, Action onClosedWithoutReward)
    {
        if (UseFallback)
        {
            nonMobileFallback.Show(onRewarded, onClosedWithoutReward);
            return;
        }

        if (!IsReady)
        {
            Debug.LogWarning("[AdMob] 아직 광고가 준비되지 않았습니다.");
            onClosedWithoutReward?.Invoke();
            if (!showing) LoadAd();
            return;
        }

        showing = true;
        bool earnedReward = false;
        bool finished = false;

        // 광고가 닫히거나 실패했을 때 한 번만 결과를 알려주는 함수
        void Finish()
        {
            if (finished) return;
            finished = true;
            showing = false;

            if (earnedReward) onRewarded?.Invoke();
            else onClosedWithoutReward?.Invoke();

            LoadAd(); // 다음 광고 미리 받아두기
        }

        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log($"[AdMob] 광고 닫힘 (보상: {earnedReward})");
            Finish();
        };

        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning($"[AdMob] 광고 표시 실패: {error}");
            Finish();
        };

        // 이 콜백은 "보상 조건(끝까지 시청)을 채웠을 때" 호출됩니다.
        // 실제 지급은 광고가 닫힌 뒤(Finish)에 해서, 광고 화면 뒤에서 UI가 바뀌지 않게 합니다.
        rewardedAd.Show((Reward reward) =>
        {
            earnedReward = true;
            Debug.Log($"[AdMob] 보상 조건 달성: {reward.Type} x{reward.Amount}");
        });
    }

    // ───────────── 디버그 ─────────────

    /// <summary>
    /// 광고 검사기(Ad Inspector)를 엽니다. 광고가 안 뜰 때 원인을 기기 화면에서 확인할 수 있습니다.
    /// 테스트용 버튼의 OnClick()에 연결하세요. (테스트 기기/테스트 광고에서만 열립니다)
    /// </summary>
    public void OpenAdInspector()
    {
        MobileAds.OpenAdInspector(error =>
        {
            if (error != null) Debug.LogWarning($"[AdMob] 광고 검사기 열기 실패: {error}");
        });
    }
}