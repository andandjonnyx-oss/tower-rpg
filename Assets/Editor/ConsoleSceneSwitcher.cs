using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ビルド設定のシーンリストを「モバイル版セット」と「コンソール版セット」で
/// 切り替えるエディタツール。
///
/// 【仕組み（CLAUDE.md 第11節）】
///   コンソール用にレイアウトを作り替えたシーンは Assets/Scenes/Console/ に
///   **モバイル版と同じファイル名**で置く。SceneManager.LoadScene("Battle") は
///   ビルド設定リスト内の名前で解決されるため、リストの差し替えだけで
///   全ての遷移コードが無改修のままコンソール版シーンを読むようになる。
///
///   - コンソール版に切り替え: リストの各シーンについて Console/ に同名ファイルが
///     あればそちらのパスへ差し替える（無いシーンは共有のまま）
///   - モバイル版に戻す: Console/ 配下を指すエントリを元のパスへ戻す
///   どちらも冪等（何度実行しても同じ結果）。
///
/// 【運用】
///   - エディタでコンソール版シーンを再生テストする時もこのメニューで切り替える
///     （再生時のシーン名解決もこのリストに従うため）
///   - ⚠️ モバイル向けビルド前は必ずモバイル版へ戻すこと。
///     スクリプティング定義シンボル CONSOLE_BUILD の付け外しと対で運用する。
/// </summary>
public static class ConsoleSceneSwitcher
{
    private const string ScenesDir = "Assets/Scenes/";
    private const string ConsoleDir = "Assets/Scenes/Console/";

    [MenuItem("Tools/コンソール版シーン/シーンリストをコンソール版に切り替え")]
    private static void SwitchToConsole()
    {
        int replaced = Apply(toConsole: true);
        Debug.Log($"[ConsoleSceneSwitcher] コンソール版へ切り替え: {replaced} 件のシーンを Console/ 版に差し替えました。\n" +
                  "⚠️ CONSOLE_BUILD シンボルの設定も忘れずに（Player Settings > Scripting Define Symbols）。");
    }

    [MenuItem("Tools/コンソール版シーン/シーンリストをモバイル版に戻す")]
    private static void SwitchToMobile()
    {
        int replaced = Apply(toConsole: false);
        Debug.Log($"[ConsoleSceneSwitcher] モバイル版へ復帰: {replaced} 件のシーンを共有版に戻しました。\n" +
                  "⚠️ CONSOLE_BUILD シンボルを外すのも忘れずに。");
    }

    [MenuItem("Tools/コンソール版シーン/現在の状態を表示")]
    private static void ShowStatus()
    {
        int consoleCount = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var s in EditorBuildSettings.scenes)
        {
            bool isConsole = s.path.StartsWith(ConsoleDir);
            if (isConsole) consoleCount++;
            sb.AppendLine((isConsole ? "  [Console] " : "  [共有]    ") + s.path);
        }
        string mode = consoleCount > 0 ? $"コンソール版（{consoleCount}件差し替え中）" : "モバイル版（全て共有シーン）";
        Debug.Log($"[ConsoleSceneSwitcher] 現在のシーンリスト: {mode}\n{sb}");
    }

    /// <summary>
    /// シーンリストを一括変換する。戻り値は差し替えた件数。
    /// </summary>
    private static int Apply(bool toConsole)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int replaced = 0;

        for (int i = 0; i < scenes.Count; i++)
        {
            string path = scenes[i].path;
            string fileName = Path.GetFileName(path);

            if (toConsole)
            {
                if (path.StartsWith(ConsoleDir)) continue; // 既にコンソール版
                string consolePath = ConsoleDir + fileName;
                if (File.Exists(consolePath))
                {
                    scenes[i] = new EditorBuildSettingsScene(consolePath, scenes[i].enabled);
                    replaced++;
                }
            }
            else
            {
                if (!path.StartsWith(ConsoleDir)) continue; // 既に共有版
                string mobilePath = ScenesDir + fileName;
                if (File.Exists(mobilePath))
                {
                    scenes[i] = new EditorBuildSettingsScene(mobilePath, scenes[i].enabled);
                    replaced++;
                }
                else
                {
                    Debug.LogWarning($"[ConsoleSceneSwitcher] 共有版が見つかりません: {mobilePath}（このエントリは据え置き）");
                }
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        return replaced;
    }
}
