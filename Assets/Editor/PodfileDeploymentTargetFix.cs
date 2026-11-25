#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// iOS 빌드 때 CocoaPods 라이브러리(AdMob 등)의 최소 iOS 버전을 15.0으로 맞춰줍니다.
///
/// 최신 Xcode는 iOS 15.0 미만을 지원하지 않는데, AdMob 관련 Pod들은 12.0으로 설정되어 있어서
/// "IPHONEOS_DEPLOYMENT_TARGET is set to 12.0" 빌드 오류가 납니다.
///
/// External Dependency Manager가 Podfile을 만든 직후(순서 40), pod install(순서 50) 전에
/// Podfile 끝에 post_install 설정을 덧붙입니다. 그래서 Unity에서 다시 빌드해도 자동으로 적용됩니다.
///
/// 반드시 Assets/Editor 폴더 안에 두세요.
/// </summary>
public static class PodfileDeploymentTargetFix
{
    private const string MinimumIOSVersion = "15.0";

    [PostProcessBuild(45)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string podfilePath = Path.Combine(buildPath, "Podfile");
        if (!File.Exists(podfilePath))
        {
            Debug.LogWarning("[PodfileFix] Podfile을 찾지 못했습니다. (External Dependency Manager 설정 확인)");
            return;
        }

        string text = File.ReadAllText(podfilePath);
        if (text.Contains("IPHONEOS_DEPLOYMENT_TARGET")) return; // 이미 적용됨

        text += "\npost_install do |installer|\n" +
                "  installer.pods_project.targets.each do |t|\n" +
                "    t.build_configurations.each do |config|\n" +
                "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '" + MinimumIOSVersion + "'\n" +
                "    end\n" +
                "  end\n" +
                "end\n";

        File.WriteAllText(podfilePath, text);
        Debug.Log($"[PodfileFix] Pod들의 최소 iOS 버전을 {MinimumIOSVersion}으로 설정했습니다.");
    }
}
#endif
