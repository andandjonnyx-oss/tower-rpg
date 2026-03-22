using UnityEngine;

/// <summary>
/// Main シーンに配置するコンポーネント。
/// シーン開始時に HP/MP を全回復し、オートセーブを実行する。
/// どのシーンで中断しても、次回起動時は Main シーンから全回復状態で再開される。
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

        // オートセーブ実行
        SaveManager.Save();
        Debug.Log("[MainSceneAutoSave] オートセーブ完了");
    }
}