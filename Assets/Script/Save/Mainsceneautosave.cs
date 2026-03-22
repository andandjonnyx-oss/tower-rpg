using UnityEngine;

/// <summary>
/// Main シーンに配置するコンポーネント。
/// シーン開始時に HP/MP を全回復し、オートセーブを実行する。
/// 即時セーブと併用: 通常の操作は各マネージャーが即時セーブするが、
/// Main 到着時は全回復を反映してセーブし直す役割を担う。
/// </summary>
public class MainSceneAutoSave : MonoBehaviour
{
    private void Start()
    {
        // HP/MP 全回復（Main シーン = 安全地帯）
        if (GameState.I != null)
        {
            GameState.I.currentHp = GameState.I.maxHp;
            GameState.I.currentMp = GameState.I.maxMp;

            // バトル中フラグをクリア（戦闘中に中断して戻ってきた場合のため）
            GameState.I.isInBattle = false;
            GameState.I.battleTurnConsumed = false;
            GameState.I.battleItemActionLog = "";
        }

        // オートセーブ実行（全回復状態を保存）
        SaveManager.Save();
        Debug.Log("[MainSceneAutoSave] Main到着: 全回復 + オートセーブ完了");
    }
}