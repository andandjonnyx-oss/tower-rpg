using UnityEditor;
using UnityEngine;

public class MonsterDetailWindow : EditorWindow
{
    private Monster monster;
    private Vector2 scrollPos;

    public static void Open(Monster target)
    {
        var window = GetWindow<MonsterDetailWindow>("Monster Detail");
        window.SetMonster(target);
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    public void SetMonster(Monster target)
    {
        monster = target;
        Repaint();
    }

    private void OnGUI()
    {
        if (monster == null)
        {
            EditorGUILayout.HelpBox("モンスターが選択されていません。", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawBasicSection();
        EditorGUILayout.Space();

        DrawEncounterSection();
        EditorGUILayout.Space();

        DrawSpawnControlSection();
        EditorGUILayout.Space();

        DrawStatsSection();
        EditorGUILayout.Space();

        DrawHitEvasionSection();
        EditorGUILayout.Space();

        DrawStatusResistanceSection();
        EditorGUILayout.Space();

        DrawRewardSection();
        EditorGUILayout.Space();

        DrawActionPatternSection();
        EditorGUILayout.Space();

        DrawHelpSection();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // 基本情報
    // =========================================================
    private void DrawBasicSection()
    {
        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("ID", monster.ID);
        EditorGUILayout.LabelField("名前", monster.Mname);

        EditorGUILayout.Space();

        if (monster.Image != null)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(monster.Image);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(monster.Image);
            }

            GUILayout.Label(preview, GUILayout.Width(96), GUILayout.Height(96));
        }
        else
        {
            EditorGUILayout.LabelField("画像", "なし");
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 出現範囲
    // =========================================================
    private void DrawEncounterSection()
    {
        EditorGUILayout.LabelField("出現範囲", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("範囲",
            $"{monster.Minfloor}F {monster.Minstep}STEP ～ {monster.Maxfloor}F {monster.Maxstep}STEP");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 出現制御
    // =========================================================
    private void DrawSpawnControlSection()
    {
        EditorGUILayout.LabelField("出現制御", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Weight", monster.Weight.ToString());
        EditorGUILayout.LabelField("Is Boss", monster.IsBoss ? "○" : "×");
        EditorGUILayout.LabelField("Is Unique", monster.IsUnique ? "○" : "×");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // ステータス
    // =========================================================
    private void DrawStatsSection()
    {
        EditorGUILayout.LabelField("ステータス", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Max HP", monster.MaxHp.ToString());
        EditorGUILayout.LabelField("Attack", monster.Attack.ToString());
        EditorGUILayout.LabelField("Defense", monster.Defense.ToString());
        EditorGUILayout.LabelField("Magic Defense", monster.MagicDefense.ToString());
        EditorGUILayout.LabelField("Speed", monster.Speed.ToString());
        EditorGUILayout.LabelField("Luck", monster.Luck.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 命中・回避
    // =========================================================
    private void DrawHitEvasionSection()
    {
        EditorGUILayout.LabelField("命中・回避", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("回避力", monster.Evasion.ToString());
        EditorGUILayout.LabelField("基礎命中率", $"{monster.BaseHitRate}%");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 状態異常耐性
    // =========================================================
    private void DrawStatusResistanceSection()
    {
        EditorGUILayout.LabelField("状態異常耐性", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("毒耐性", $"{monster.PoisonResistance}%");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 報酬
    // =========================================================
    private void DrawRewardSection()
    {
        EditorGUILayout.LabelField("報酬", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Exp", monster.Exp.ToString());
        EditorGUILayout.LabelField("Gold", monster.Gold.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 行動パターン
    // =========================================================
    private void DrawActionPatternSection()
    {
        EditorGUILayout.LabelField("行動パターン", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("基準行動レンジ", monster.baseActionRange.ToString());

        if (monster.actions == null || monster.actions.Length == 0)
        {
            EditorGUILayout.LabelField("行動テーブル", "なし（通常攻撃のみ）");
        }
        else
        {
            EditorGUILayout.Space();

            int prevThreshold = 0;
            for (int i = 0; i < monster.actions.Length; i++)
            {
                var entry = monster.actions[i];
                if (entry == null) continue;

                // 確率範囲を表示
                string rangeStr = $"{prevThreshold}～{entry.threshold - 1}";

                if (entry.skill != null)
                {
                    var sk = entry.skill;
                    string detail = FormatSkillDetail(sk);
                    EditorGUILayout.LabelField($"  [{i}] {rangeStr}", detail);
                }
                else
                {
                    EditorGUILayout.LabelField($"  [{i}] {rangeStr}", "通常攻撃（フォールバック）");
                }

                prevThreshold = entry.threshold;
            }
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// スキルの詳細情報を文字列化する。
    /// NormalAttack と SkillAttack は同一処理に統合されたため、
    /// 表示も統一する。Preemptive は先制マークを付ける。
    /// </summary>
    private string FormatSkillDetail(SkillData sk)
    {
        // Idle は特別扱い
        if (sk.actionType == MonsterActionType.Idle)
        {
            return "何もしない";
        }

        // 先制マーク
        string preemptiveMark = (sk.actionType == MonsterActionType.Preemptive) ? "【先制】" : "";

        // 非ダメージスキル
        if (sk.IsNonDamage)
        {
            string detail = $"{preemptiveMark}{sk.skillName} (効果のみ)";
            detail += FormatAdditionalEffects(sk);
            return detail;
        }

        // ダメージスキル（NormalAttack / SkillAttack / Preemptive 共通）
        {
            string detail = $"{preemptiveMark}{sk.skillName} ({sk.damageCategory}, {sk.skillAttribute.ToJapanese()}";

            // ダメージ式の表示
            if (sk.damageMultiplier > 0f && sk.bonusDamage > 0)
                detail += $", x{sk.damageMultiplier}+{sk.bonusDamage}";
            else if (sk.damageMultiplier > 0f)
                detail += $", x{sk.damageMultiplier}";
            else if (sk.bonusDamage > 0)
                detail += $", 固定{sk.bonusDamage}";

            detail += $", 命中{sk.baseHitRate}%)";
            detail += FormatAdditionalEffects(sk);
            return detail;
        }
    }

    /// <summary>
    /// スキルの追加効果リストを文字列で表示する。
    /// </summary>
    private string FormatAdditionalEffects(SkillData skill)
    {
        if (skill.additionalEffects == null || skill.additionalEffects.Count == 0)
            return "";

        string result = "";
        for (int j = 0; j < skill.additionalEffects.Count; j++)
        {
            var eff = skill.additionalEffects[j];
            if (eff == null || eff.effectData == null) continue;

            string effName = !string.IsNullOrEmpty(eff.effectData.effectName)
                ? eff.effectData.effectName
                : eff.effectData.GetType().Name;

            if (eff.effectData is StatusAilmentEffectData)
            {
                if (eff.ailmentMode == AilmentMode.Inflict)
                {
                    result += $" +[{eff.targetStatusEffect}付与 {eff.chance}%]";
                }
                else
                {
                    result += $" +[{eff.targetStatusEffect}回復]";
                }
            }
            else if (eff.effectData is LevelDrainEffectData)
            {
                int amt = (eff.intValue > 0) ? eff.intValue : 1;
                result += $" +[{effName} Lv-{amt}";
                if (eff.chance < 100) result += $" {eff.chance}%";
                result += "]";
            }
            else if (eff.effectData is HealEffectData healData)
            {
                string formula = healData.formulaType.ToString();
                result += $" +[{effName} {formula}:{eff.intValue}";
                if (eff.chance < 100) result += $" {eff.chance}%";
                result += "]";
            }
            else
            {
                result += $" +[{effName}]";
            }
        }

        return result;
    }

    // =========================================================
    // 説明
    // =========================================================
    private void DrawHelpSection()
    {
        EditorGUILayout.LabelField("説明", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (string.IsNullOrWhiteSpace(monster.Help))
        {
            EditorGUILayout.LabelField("説明なし");
        }
        else
        {
            EditorGUILayout.HelpBox(monster.Help, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }
}