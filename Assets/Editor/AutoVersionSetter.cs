using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System;

public class AutoVersionSetter : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // 年月日だけ（例: 20250828）
        string dateVersion = DateTime.Now.ToString("yyyyMMdd");
        PlayerSettings.bundleVersion = dateVersion;  // ← Application.version に入る

#if UNITY_ANDROID
        // Androidは整数必須なので "yyMMdd" を使う（例: 250828）
        PlayerSettings.Android.bundleVersionCode = int.Parse(DateTime.Now.ToString("yyMMdd"));
#endif

#if UNITY_IOS
        PlayerSettings.iOS.buildNumber = dateVersion;
#endif

        UnityEngine.Debug.Log($"[AutoVersionSetter] version set to {dateVersion}");
    }
}
