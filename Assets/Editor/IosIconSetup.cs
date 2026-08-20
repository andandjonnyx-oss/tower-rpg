using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// iOS のアプリアイコンを1枚の画像から全スロットへ一括設定する。
///
/// iOS はアイコンスロットが19個あり、手作業で埋めるのは非現実的なため用意した。
/// Unity はビルド時に各サイズへ自動でリサイズするので、
/// 1024x1024 を全スロットに割り当てておけばよい。
///
/// 【元画像の要件】
///   - 1024x1024
///   - アルファチャンネル無し（透過があると App Store Connect が ITMS-90717 で拒否する）
///   - 角丸を付けない（iOS 側が自動でマスクするため、自分で丸めると二重に欠ける）
/// </summary>
public static class IosIconSetup
{
    private const string IconPath = "Assets/Art/AppIcon/icon_ios_1024.png";

    [MenuItem("Tools/iOS/アプリアイコンを一括設定")]
    public static void ApplyIcons()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            Debug.LogError($"[IosIconSetup] アイコンが見つからない: {IconPath}");
            return;
        }

        int slots = 0;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
        {
            // PlatformIconKind には GetPlatformIcons/SetPlatformIcons を使う。
            // 旧 API の GetIconSizes/SetIcons は IconKind 用で型が合わない。
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            if (icons == null || icons.Length == 0) continue;

            foreach (var platformIcon in icons)
            {
                // iOS はレイヤー1枚だが、将来の仕様変更に備えて最大数まで埋める。
                for (int layer = 0; layer < platformIcon.maxLayerCount; layer++)
                {
                    platformIcon.SetTexture(icon, layer);
                }
                slots++;
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[IosIconSetup] {slots} 個のアイコンスロットに {IconPath} を設定した。" +
                  "File > Save Project で ProjectSettings.asset に保存すること。");
    }
}
