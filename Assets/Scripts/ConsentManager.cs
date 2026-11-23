using System;
using System.Collections.Generic;
using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>
/// 유럽(EEA), 영국, 스위스 유저의 광고 동의(GDPR)를 Google UMP로 처리합니다.
///
/// 흐름
///   1) 앱 시작 → 동의 정보 갱신 (어느 나라 유저인지, 동의가 필요한지 Google이 판단)
///   2) 동의가 필요한 지역이면 동의 창을 띄움 (한 번 선택하면 다음부터는 뜨지 않음)
///   3) 광고를 요청해도 되는 상태(CanRequestAds)가 되면 AdMob을 초기화
///
/// 한국, 미국 등 동의가 필요 없는 지역에서는 창이 뜨지 않고 바로 넘어갑니다.
/// 에디터와 PC에서는 아무것도 하지 않고 바로 넘어갑니다.
///
/// AdMobRewardedADService가 자동으로 호출하므로 씬에 붙일 필요는 없습니다.
/// 동의 창 문구는 AdMob 웹의 "개인 정보 보호 및 메시지 → 유럽 규정"에서 만들고 게시해야 합니다.
/// </summary>
public static class ConsentManager
{
    private static bool gathering;
    private static readonly List<Action> waiting = new List<Action>();

    /// <summary>광고를 요청해도 되는 상태인지.</summary>
    public static bool CanRequestAds
    {
        get
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return ConsentInformation.CanRequestAds();
#else
            return true;
#endif
        }
    }

    /// <summary>"개인정보 설정" 버튼을 보여줘야 하는지 (유럽 등 동의가 필요한 지역의 유저).</summary>
    public static bool IsPrivacyOptionsRequired
    {
        get
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 동의 정보를 갱신하고, 필요하면 동의 창을 띄운 뒤 onDone을 호출합니다.
    /// 여러 곳에서 동시에 불러도 한 번만 진행합니다.
    /// </summary>
    /// <param name="forceEEAForTest">테스트용: 내 기기를 유럽 유저처럼 취급 (출시 빌드에서는 false)</param>
    /// <param name="testDeviceHashedIds">테스트용 기기 ID (Logcat/Xcode 콘솔에 찍히는 값)</param>
    public static void Gather(bool forceEEAForTest, IList<string> testDeviceHashedIds, Action onDone)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (onDone != null) waiting.Add(onDone);
        if (gathering) return;
        gathering = true;

        var request = new ConsentRequestParameters();

        if (forceEEAForTest)
        {
            request.ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,
                TestDeviceHashedIds = testDeviceHashedIds != null ? new List<string>(testDeviceHashedIds) : new List<string>()
            };
        }

        ConsentInformation.Update(request, (FormError updateError) =>
        {
            if (updateError != null)
            {
                // 인터넷이 없을 때 등. 이전 실행에서 받은 동의가 있으면 그대로 광고를 요청할 수 있습니다.
                Debug.LogWarning($"[Consent] 동의 정보 갱신 실패: {updateError.Message}");
                Finish();
                return;
            }

            // 필요한 지역이면 동의 창을 띄우고, 이미 선택했거나 필요 없는 지역이면 바로 넘어갑니다.
            ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
            {
                if (showError != null)
                    Debug.LogWarning($"[Consent] 동의 창 표시 실패: {showError.Message}");

                Finish();
            });
        });
#else
        onDone?.Invoke();
#endif
    }

    private static void Finish()
    {
        gathering = false;
        Debug.Log($"[Consent] 완료 - 광고 요청 가능: {CanRequestAds}, 개인정보 설정 버튼 필요: {IsPrivacyOptionsRequired}");

        var callbacks = new List<Action>(waiting);
        waiting.Clear();
        foreach (Action cb in callbacks) cb?.Invoke();
    }

    /// <summary>유저가 동의 선택을 다시 할 수 있는 창을 띄웁니다. ("개인정보 설정" 버튼용)</summary>
    public static void ShowPrivacyOptions(Action onClosed = null)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            if (error != null) Debug.LogWarning($"[Consent] 개인정보 설정 창 실패: {error.Message}");
            onClosed?.Invoke();
        });
#else
        Debug.Log("[Consent] (에디터) 개인정보 설정 창은 폰에서만 열립니다.");
        onClosed?.Invoke();
#endif
    }

    /// <summary>테스트용: 저장된 동의 선택을 지웁니다. 다음 실행 때 동의 창이 다시 뜹니다.</summary>
    public static void ResetForTest()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        ConsentInformation.Reset();
        Debug.Log("[Consent] 동의 정보를 초기화했습니다. 앱을 다시 실행하세요.");
#endif
    }
}
