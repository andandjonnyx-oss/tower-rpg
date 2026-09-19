using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
///
/// 【CONSOLE_BUILD シンボルの連動（2026-09-18 追加）】
///   シーンリストの切り替えと同時に、**現在アクティブなビルドターゲット**の
///   スクリプティング定義シンボルへ CONSOLE_BUILD を付け外しする。
///   エディタのコンパイルはアクティブターゲットのシンボルに従うため、ターゲットが
///   Android のままでも、コンソール版へ切り替えればエディタ上でコンソール仕様
///   （広告なし・コンティニュー残数制・consoleLines）を再生テストできる。
///   モバイル版へ戻すと Android / iOS / アクティブターゲットから必ず外す。
///   付け外しのたびにスクリプトの再コンパイルが走る。
///
///   戻し忘れの保険として ConsoleBuildGuard がモバイル向けビルドを止める。
/// </summary>
public static class ConsoleSceneSwitcher
{
    private const string ScenesDir = "Assets/Scenes/";
    private const string ConsoleDir = "Assets/Scenes/Console/";

    [MenuItem("Tools/コンソール版シーン/シーンリストをコンソール版に切り替え")]
    private static void SwitchToConsole()
    {
        int replaced = Apply(toConsole: true);
        var target = ActiveTarget();
        SetSymbol(target, true);
        Debug.Log($"[ConsoleSceneSwitcher] コンソール版へ切り替え: {replaced} 件のシーンを Console/ 版に差し替えました。\n" +
                  $"CONSOLE_BUILD を {target.TargetName} に定義しました（再コンパイルが走ります）。");
    }

    [MenuItem("Tools/コンソール版シーン/シーンリストをモバイル版に戻す")]
    private static void SwitchToMobile()
    {
        int replaced = Apply(toConsole: false);
        SetSymbol(NamedBuildTarget.Android, false);
        SetSymbol(NamedBuildTarget.iOS, false);
        SetSymbol(ActiveTarget(), false);
        Debug.Log($"[ConsoleSceneSwitcher] モバイル版へ復帰: {replaced} 件のシーンを共有版に戻しました。\n" +
                  "CONSOLE_BUILD を Android / iOS / アクティブターゲットから外しました（再コンパイルが走ります）。");
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
        var target = ActiveTarget();
        string sym = HasSymbol(target) ? "定義あり" : "定義なし";
        Debug.Log($"[ConsoleSceneSwitcher] 現在のシーンリスト: {mode}\n" +
                  $"CONSOLE_BUILD（{target.TargetName}）: {sym}\n{sb}");
    }

    // =========================================================
    // CONSOLE_BUILD シンボルの付け外し
    // =========================================================

    internal const string Symbol = "CONSOLE_BUILD";

    internal static NamedBuildTarget ActiveTarget()
        => NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

    internal static bool HasSymbol(NamedBuildTarget target)
    {
        var list = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';'));
        return list.Contains(Symbol);
    }

    internal static bool HasConsoleScenes()
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && s.path.StartsWith(ConsoleDir)) return true;
        return false;
    }

    private static void SetSymbol(NamedBuildTarget target, bool on)
    {
        var list = new List<string>();
        foreach (var s in PlayerSettings.GetScriptingDefineSymbols(target).Split(';'))
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());

        bool has = list.Contains(Symbol);
        if (on == has) return; // 冪等
        if (on) list.Add(Symbol); else list.Remove(Symbol);
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
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

/// <summary>
/// モバイル向けビルドにコンソール仕様が混入するのを止める保険。
/// Android / iOS のビルド開始時に、CONSOLE_BUILD が定義されているか、シーンリストに
/// Console/ 版が残っていればビルドを失敗させる（戻し忘れは無診断で「広告が出ない
/// モバイル版」になるため、気付ける場所がここしかない）。
/// </summary>
public class ConsoleBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var group = report.summary.platformGroup;
        if (group != BuildTargetGroup.Android && group != BuildTargetGroup.iOS) return;

        var target = NamedBuildTarget.FromBuildTargetGroup(group);
        bool symbol = ConsoleSceneSwitcher.HasSymbol(target);
        bool scenes = ConsoleSceneSwitcher.HasConsoleScenes();
        if (!symbol && !scenes) return;

        throw new BuildFailedException(
            "[ConsoleBuildGuard] モバイル向けビルドにコンソール仕様が残っています"
            + (symbol ? " / CONSOLE_BUILD が定義されている" : "")
            + (scenes ? " / シーンリストが Console 版のまま" : "")
            + "。Tools > コンソール版シーン > シーンリストをモバイル版に戻す を実行してください。");
    }
}
