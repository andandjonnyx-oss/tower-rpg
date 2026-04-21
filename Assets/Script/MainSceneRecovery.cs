using UnityEngine;

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
        var gs = GameState.I;
        if (gs == null) return;

        gs.currentHp = gs.maxHp;
        gs.currentMp = gs.maxMp;
        gs.ClearAllStatusEffects();
        SaveManager.Save();
        TowerState.ResetStorageAdFlag();

        Debug.Log($"[Main] 全回復: HP={gs.currentHp}/{gs.maxHp} MP={gs.currentMp}/{gs.maxMp} 状態異常クリア");
    }
}