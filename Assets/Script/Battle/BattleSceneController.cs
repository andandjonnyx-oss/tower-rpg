using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 戦闘シーンのメインコントローラー。
/// 敵の表示、攻撃処理、戦闘ログ、勝利判定を管理する。
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

    /// <summary>
    /// 攻撃ボタンが押された時の処理。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;

        // 装備中の武器を取得
        string weaponName;
        WeaponAttribute weaponAttribute;
        int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);

        // ダメージ計算: STR + 武器攻撃力
        int str = (GameState.I != null) ? GameState.I.baseSTR : 1;
        int damage = str + weaponPower;

        // ダメージを最低1保証
        if (damage < 1) damage = 1;

        // ダメージ適用
        enemyCurrentHp -= damage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        // ログ出力（属性は日本語表示）
        AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {damage}ダメージ！");

        // 敵撃破判定
        if (enemyCurrentHp <= 0)
        {
            OnEnemyDefeated();
        }
    }

    /// <summary>
    /// 装備中の武器情報を取得する。未装備なら素手。
    /// </summary>
    private void GetEquippedWeaponInfo(out string weaponName, out WeaponAttribute attribute, out int power)
    {
        // デフォルト: 素手
        weaponName = "素手";
        attribute = WeaponAttribute.Strike;
        power = 0;

        if (GameState.I == null || string.IsNullOrEmpty(GameState.I.equippedWeaponUid))
            return;

        if (ItemBoxManager.Instance == null)
            return;

        // 装備中の uid からアイテムを検索
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

        // uid が見つからなかった場合（アイテムが消えた等）、素手扱い
        GameState.I.equippedWeaponUid = "";
    }

    /// <summary>
    /// 敵HP0時の処理。
    /// </summary>
    private void OnEnemyDefeated()
    {
        battleEnded = true;

        AddLog($"{enemyMonster.Mname} を倒した！");

        // 攻撃ボタンを無効化
        if (attackButton != null)
            attackButton.interactable = false;

        // 少し待ってからTowerに戻る
        Invoke(nameof(ReturnToTower), 1.5f);
    }

    private void ReturnToTower()
    {
        SceneManager.LoadScene(towerSceneName);
    }

    /// <summary>
    /// 戦闘ログに1行追加する。最大3行を超えたら古いものを破棄。
    /// </summary>
    private void AddLog(string message)
    {
        logLines.Add(message);

        // 最大行数を超えたら古い行を削除
        while (logLines.Count > MaxLogLines)
        {
            logLines.RemoveAt(0);
        }

        // テキスト更新
        if (battleLogText != null)
        {
            battleLogText.text = string.Join("\n", logLines);
        }
    }
}