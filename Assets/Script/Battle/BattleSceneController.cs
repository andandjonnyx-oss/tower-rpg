using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 戦闘シーンのメインコントローラー。
/// ターン制（味方→敵→味方…）で戦闘を進行する。
/// </summary>
public class BattleSceneController : MonoBehaviour
{
    [Header("UI - Enemy")]
    [SerializeField] private Image enemyImage;

    [Header("UI - Battle Log")]
    [Tooltip("戦闘ログ表示用 TMP_Text（3行分）")]
    [SerializeField] private TMP_Text battleLogText;

    [Header("UI - Buttons")]
    [SerializeField] private Button attackButton;

    [Header("Scene Names")]
    [SerializeField] private string towerSceneName = "Tower";
    [SerializeField] private string mainSceneName = "Main";

    // 戦闘中の敵HP
    private int enemyCurrentHp;
    private Monster enemyMonster;

    // 戦闘ログ（最大3行）
    private List<string> logLines = new List<string>();
    private const int MaxLogLines = 3;

    private bool battleEnded = false;

    private void Start()
    {
        enemyMonster = BattleContext.EnemyMonster;
        if (enemyMonster == null)
        {
            Debug.LogError("[Battle] EnemyMonster is null");
            return;
        }

        // 敵の表示
        if (enemyImage != null)
        {
            enemyImage.sprite = enemyMonster.Image;
            enemyImage.preserveAspect = true;
        }

        // 敵HPを初期化
        enemyCurrentHp = enemyMonster.MaxHp;

        // 攻撃ボタン設定
        if (attackButton != null)
            attackButton.onClick.AddListener(OnAttackClicked);

        // ログ初期化
        AddLog($"{enemyMonster.Mname} が現れた！");
    }

    // =========================================================
    // プレイヤーターン
    // =========================================================

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン）。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;

        // ボタン連打防止
        SetButtonsInteractable(false);

        // 装備中の武器を取得
        string weaponName;
        WeaponAttribute weaponAttribute;
        int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);

        // ダメージ計算: STR + 武器攻撃力
        int str = (GameState.I != null) ? GameState.I.baseSTR : 1;
        int damage = str + weaponPower;
        if (damage < 1) damage = 1;

        // ダメージ適用
        enemyCurrentHp -= damage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {damage}ダメージ！");

        // 敵撃破判定
        if (enemyCurrentHp <= 0)
        {
            OnVictory();
            return;
        }

        // 敵ターンへ（少し待ってから）
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    // =========================================================
    // 敵ターン
    // =========================================================

    /// <summary>
    /// 敵の攻撃処理。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;

        // 敵の攻撃力（Monster.Attack をそのまま使用）
        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        // プレイヤーにダメージ
        if (GameState.I != null)
        {
            GameState.I.currentHp -= enemyDamage;
            if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
        }

        AddLog($"{enemyMonster.Mname} の攻撃！ {enemyDamage}ダメージ！");

        // プレイヤー敗北判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            OnDefeat();
            return;
        }

        // プレイヤーターンに戻す
        SetButtonsInteractable(true);
    }

    // =========================================================
    // 勝利 / 敗北
    // =========================================================

    /// <summary>
    /// 勝利処理。Towerシーンに戻る。
    /// </summary>
    private void OnVictory()
    {
        battleEnded = true;
        AddLog($"{enemyMonster.Mname} を倒した！");
        SetButtonsInteractable(false);

        Invoke(nameof(ReturnToTower), 1.5f);
    }

    /// <summary>
    /// 敗北処理。HP全回復してMainシーンに戻る。
    /// </summary>
    private void OnDefeat()
    {
        battleEnded = true;
        AddLog("You は倒れた…");
        SetButtonsInteractable(false);

        Invoke(nameof(ReturnToMainWithFullRecover), 1.5f);
    }

    private void ReturnToTower()
    {
        // 勝利
        SceneManager.LoadScene(towerSceneName);
    }

    private void ReturnToMainWithFullRecover()
    {
        // 敗北帰還: HP/MP全回復
        FullRecover();
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// HP/MPを全回復する。
    /// </summary>
    private void FullRecover()
    {
        if (GameState.I == null) return;
        GameState.I.currentHp = GameState.I.maxHp;
        GameState.I.currentMp = GameState.I.maxMp;
        Debug.Log($"[Battle] 全回復: HP={GameState.I.currentHp}/{GameState.I.maxHp}");
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    /// <summary>
    /// 装備中の武器情報を取得する。未装備なら素手。
    /// </summary>
    private void GetEquippedWeaponInfo(out string weaponName, out WeaponAttribute attribute, out int power)
    {
        weaponName = "素手";
        attribute = WeaponAttribute.Strike;
        power = 0;

        if (GameState.I == null || string.IsNullOrEmpty(GameState.I.equippedWeaponUid))
            return;

        if (ItemBoxManager.Instance == null)
            return;

        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].uid == GameState.I.equippedWeaponUid)
            {
                var data = items[i].data;
                if (data != null && data.category == ItemCategory.Weapon)
                {
                    weaponName = data.itemName;
                    attribute = data.weaponAttribute;
                    power = data.attackPower;
                }
                return;
            }
        }

        GameState.I.equippedWeaponUid = "";
    }

    /// <summary>
    /// 攻撃ボタン等の操作可否を切り替える。
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (attackButton != null)
            attackButton.interactable = interactable;
    }

    /// <summary>
    /// 戦闘ログに1行追加する。最大3行を超えたら古いものを破棄。
    /// </summary>
    private void AddLog(string message)
    {
        logLines.Add(message);

        while (logLines.Count > MaxLogLines)
        {
            logLines.RemoveAt(0);
        }

        if (battleLogText != null)
        {
            battleLogText.text = string.Join("\n", logLines);
        }
    }
}