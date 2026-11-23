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
/// [1.1] 미리 받아둔 광고는 static으로 보관합니다. 다시시작(씬 다시 불러오기)을 해도
///       버리지 않고 그대로 재사용하므로, 쓰지 않을 광고를 반복해서 요청하지 않습니다.
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

    [Header("유럽 광고 동의 (GDPR / UMP) 테스트")]
    [Tooltip("체크하면 내 기기를 유럽 유저처럼 취급해서 동의 창을 띄웁니다. 테스트할 때만 켜고, 출시 빌드에서는 반드시 끄세요.")]
    public bool consentForceEEAForTest = false;

    [Tooltip("동의 창 테스트용 기기 ID. 앱 실행 후 Logcat / Xcode 콘솔에서 'ConsentDebugSettings' 또는 " +
             "'addTestDeviceHashedId' 문구 옆에 나오는 값을 넣으세요.")]
    public string[] consentTestDeviceHashedIds;

    [Header("로드 실패 시 재시도 간격(초)")]
    public float retryDelay = 10f;

    // SDK 초기화는 앱 실행 중 한 번이면 충분합니다. Restart로 씬이 다시 로드되어도
    // 이 값은 static이라 유지되므로 중복 초기화를 하지 않습니다.
    private static bool sdkInitialized = false;
    private static bool sdkInitializing = false;

    // [1.1] 씬이 다시 로드되어도 유지되도록 static으로 보관 (다시시작 때 광고를 버리고 새로 요청하지 않음)
    private static RewardedAd rewardedAd;
    private static bool isLoading;
    private static bool showing;

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

        if (sdkInitializing) return; // 이미 동의 확인 / 초기화 진행 중 (끝나면 광고를 받아둠)
        sdkInitializing = true;

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

        // [1.1] 유럽 광고 동의(GDPR)를 먼저 확인한 뒤 AdMob을 시작합니다.
        //       한국·미국 등은 창 없이 바로 넘어갑니다.
        ConsentManager.Gather(consentForceEEAForTest, consentTestDeviceHashedIds, () =>
        {
            if (!ConsentManager.CanRequestAds)
            {
                // 동의 정보를 아직 못 받음 (첫 실행인데 인터넷이 없는 경우 등). 다음 씬 시작 때 다시 시도.
                sdkInitializing = false;
                Debug.LogWarning("[AdMob] 아직 광고를 요청할 수 없는 상태입니다. (동의 정보 없음)");
                return;
            }

            MobileAds.Initialize(status =>
            {
                sdkInitialized = true;
                sdkInitializing = false;
                Debug.Log("[AdMob] SDK 초기화 완료");
                LoadAd(); // 받아둔 광고는 static이라 씬이 바뀌었어도 다음 씬에서 그대로 씁니다
            });
        });
    }

    [ContextMenu("TEST: 광고 동의 초기화 (다음 실행 때 동의 창 다시 보기)")]
    private void TestResetConsent() => ConsentManager.ResetForTest();

    void OnDestroy()
    {
        CancelInvoke();
        // [1.1] 받아둔 광고는 static이라 파괴하지 않고 다음 씬에서 그대로 씁니다.
    }

    // ───────────── 광고 로드 ─────────────

    private void LoadAd()
    {
        if (isLoading) return;

        // [1.1] 이미 보여줄 수 있는 광고가 있으면 새로 요청하지 않음 (다시시작 후 재사용)
        if (rewardedAd != null && rewardedAd.CanShowAd() && !showing) return;

        rewardedAd?.Destroy();
        rewardedAd = null;
        isLoading = true;

        Debug.Log($"[AdMob] 광고 로드 시작: {AdUnitId}");

        RewardedAd.Load(AdUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            isLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdMob] 광고 로드 실패: {error}\n{retryDelay}초 후 다시 시도합니다.");
                if (this != null) Invoke(nameof(LoadAd), retryDelay); // 씬이 바뀌었으면 다음 씬의 Start에서 다시 요청
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