#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// iOS ビルド後に生成された Xcode プロジェクトへ ATT 対応を注入する。
///
///   1. AppTrackingTransparency.framework の weak link 追加
///      （ATTPlugin.mm が参照するが、Unity は .mm から自動でリンクしてくれない）
///   2. Info.plist の NSUserTrackingUsageDescription 設定
///      （空だと ATT ダイアログが表示されず、審査でリジェクトされる）
///
/// PostProcessBuild の順序を 1000 と大きく取り、Google Mobile Ads プラグインの
/// 後処理より後に走らせて最終値を確定させている。
/// </summary>
public static class IosPostProcessBuild
{
    /// <summary>
    /// ATT ダイアログに表示される説明文。App Store 審査で読まれる。
    /// ⚠️ Assets &gt; Google Mobile Ads &gt; Settings の
    ///    userTrackingUsageDescription と同じ文言に揃えること。
    /// </summary>
    private const string TrackingUsageDescription =
        "あなたの興味に合わせた広告を表示するために、デバイスの識別子を使用します。" +
        "許可しない場合も広告は表示されますが、内容が最適化されません。";

    [PostProcessBuild(1000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        AddAttFramework(pathToBuiltProject);
        SetTrackingUsageDescription(pathToBuiltProject);
    }

    private static void AddAttFramework(string pathToBuiltProject)
    {
        string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        // ATTPlugin.mm は UnityFramework ターゲット側にコンパイルされるため、
        // フレームワークも同ターゲットに追加する必要がある。
        // 第3引数 true = weak link。iOS 14 未満の端末でも起動できるようにする。
        proj.AddFrameworkToProject(
            proj.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
        proj.AddFrameworkToProject(
            proj.GetUnityMainTargetGuid(), "AppTrackingTransparency.framework", true);

        proj.WriteToFile(projPath);
        Debug.Log("[IosPostProcessBuild] AppTrackingTransparency.framework を追加した");
    }

    private static void SetTrackingUsageDescription(string pathToBuiltProject)
    {
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
        plist.WriteToFile(plistPath);

        Debug.Log("[IosPostProcessBuild] NSUserTrackingUsageDescription を設定した");
    }
}
#endif
