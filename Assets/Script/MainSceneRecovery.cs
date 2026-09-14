using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メインシーン（街）に配置する。
/// シーン開始時に HP/MP 全回復 + 状態異常クリアを行う。
///
/// これにより「街に戻る = 全回復」のルールが、
/// どの経路でメインに戻っても（帰還/敗北/デバッグ/倉庫）統一される。
///
/// ■ 配置手順:
///   Main シーンの適当な GameObject（例: Canvas や空オブジェクト）にアタッチする。
/// </summary>
public class MainSceneRecovery : MonoBehaviour
{
    private void Start()
    {
        // コントローラー対応: 右側6コマンドを画面上の縦位置順で縦ループ配線する。
        // Main のボタンは Gobutton プレハブ実体で専用コントローラーが無いため、
        // シーン改修なしでここから一括配線する（モバイル/Console 両 Main に効く）。
        WireMainButtonLoop();

        var gs = GameState.I;
        if (gs == null) return;

        gs.currentHp = gs.maxHp;
        gs.currentMp = gs.maxMp;
        gs.ClearAllStatusEffects();
        SaveManager.Save();
        TowerState.ResetStorageAdFlag();
        ContinueGate.ResetForNewAdventure(); // コンソール版のコンティニュー残数を全快

        // 魔法選択保持の記憶をクリア（街に戻る = 全リセット）
        MagicSelectionMemory.ClearAll();

        Debug.Log($"[Main] 全回復: HP={gs.currentHp}/{gs.maxHp} MP={gs.currentMp}/{gs.maxMp} 状態異常クリア");
    }

    /// <summary>
    /// シーン内の操作可能なボタンを画面上の縦位置（上→下）で並べ、縦ループ配線する。
    /// 名前ではなく位置で並べるため、レイアウト差（モバイル/Console）に依存しない。
    /// </summary>
    private void WireMainButtonLoop()
    {
        var found = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var list = new List<Button>();
        foreach (var b in found)
        {
            if (b == null) continue;
            if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
            if (b.navigation.mode == Navigation.Mode.None) continue;
            list.Add(b);
        }

        // 画面上の Y 降順（上のボタンが先頭）
        list.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        var loop = new List<Selectable>(list.Count);
        foreach (var b in list) loop.Add(b);
        ControllerNav.WireVerticalLoop(loop);
    }
}