using UnityEditor;
using UnityEngine;

/// <summary>
/// スキル詳細ウィンドウ。
/// MonsterDetailWindow と同じスタイルで SkillData の全情報を表示する。
/// </summary>
public class SkillDetailWindow : EditorWindow
{
    private SkillData skill;
    private Vector2 scrollPos;

    public static void Open(SkillData target)
    {
        var window = GetWindow<SkillDetailWindow>("Skill Detail");
        window.SetSkill(target);
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    public void SetSkill(SkillData target)
    {
        skill = target;
        Repaint();
    }

    private void OnGUI()
    {
        if (skill == null)
        {
            EditorGUILayout.HelpBox("スキルが選択されていません。", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawBasicSection();
        EditorGUILayout.Space();

        DrawSourceSection();
        EditorGUILayout.Space();

        DrawDamageSection();
        EditorGUILayout.Space();

        DrawCostSection();
        EditorGUILayout.Space();

        DrawHitRateSection();
        EditorGUILayout.Space();

        DrawAdditionalEffectsSection();
        EditorGUILayout.Space();

        DrawDescriptionSection();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // 基本情報
    // =========================================================
    private void DrawBasicSection()
    {
        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("ID", skill.skillId ?? "");
        EditorGUILayout.LabelField("名前", skill.skillName ?? "");
        EditorGUILayout.LabelField("アセット名", skill.name ?? "");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // ソース・行動タイプ
    // =========================================================
    private void DrawSourceSection()
    {
        EditorGUILayout.LabelField("ソース・行動タイプ", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("スキルソース", skill.skillSource.ToString());
        EditorGUILayout.LabelField("モンスター行動タイプ", skill.actionType.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // ダメージ
    // =========================================================
    private void DrawDamageSection()
    {
        EditorGUILayout.LabelField("ダメージ", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("属性", skill.skillAttribute.ToJapanese());
        EditorGUILayout.LabelField("物理/魔法", skill.damageCategory.ToString());
        EditorGUILayout.LabelField("ダメージ倍率", skill.damageMultiplier.ToString("F2"));
        EditorGUILayout.LabelField("ボーナスダメージ", skill.bonusDamage.ToString());

        // ダメージ式のプレビュー
        if (skill.IsNonDamage)
        {
            EditorGUILayout.LabelField("計算式", "非ダメージスキル（追加効果のみ）");
        }
        else if (skill.damageMultiplier > 0f && skill.bonusDamage > 0)
        {
            EditorGUILayout.LabelField("計算式", $"Attack×{skill.damageMultiplier:F1} + {skill.bonusDamage}");
        }
        else if (skill.damageMultiplier > 0f)
        {
            EditorGUILayout.LabelField("計算式", $"Attack×{skill.damageMultiplier:F1}");
        }
        else
        {
            EditorGUILayout.LabelField("計算式", $"固定{skill.bonusDamage}");
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // コスト
    // =========================================================
    private void DrawCostSection()
    {
        EditorGUILayout.LabelField("コスト", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("クールダウン", $"{skill.cooldownTurns} ターン");
        EditorGUILayout.LabelField("MP消費", skill.mpCost.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 命中率
    // =========================================================
    private void DrawHitRateSection()
    {
        EditorGUILayout.LabelField("命中率", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("基礎命中率", $"{skill.baseHitRate}%");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 追加効果
    // =========================================================
    private void DrawAdditionalEffectsSection()
    {
        EditorGUILayout.LabelField("追加効果", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (skill.additionalEffects == null || skill.additionalEffects.Count == 0)
        {
            EditorGUILayout.LabelField("追加効果なし");
        }
        else
        {
            for (int i = 0; i < skill.additionalEffects.Count; i++)
            {
                var entry = skill.additionalEffects[i];
                if (entry == null || entry.effectData == null)
                {
                    EditorGUILayout.LabelField($"  [{i}]", "null");
                    continue;
                }

                string detail = FormatEffectDetail(entry);
                EditorGUILayout.LabelField($"  [{i}]", detail);
            }
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 追加効果の詳細を文字列にフォーマットする。
    /// </summary>
    private string FormatEffectDetail(SkillEffectEntry entry)
    {
        var data = entry.effectData;
        string effName = !string.IsNullOrEmpty(data.effectName)
            ? data.effectName : data.GetType().Name;

        if (data is StatusAilmentEffectData)
        {
            if (entry.ailmentMode == AilmentMode.Inflict)
            {
                return $"{effName}: {entry.targetStatusEffect}付与 {entry.chance}%";
            }
            else
            {
                return $"{effName}: {entry.targetStatusEffect}回復";
            }
        }
        else if (data is HealEffectData healData)
        {
            string formula = GetFormulaLabel(healData.formulaType);
            return $"{effName}: {formula} 値={entry.intValue} 発動率={entry.chance}%";
        }
        else if (data is LevelDrainEffectData)
        {
            int amt = (entry.intValue > 0) ? entry.intValue : 1;
            return $"{effName}: Lv-{amt} 発動率={entry.chance}%";
        }
        else
        {
            return $"{effName}: chance={entry.chance} intValue={entry.intValue}";
        }
    }

    private string GetFormulaLabel(HealFormulaType formulaType)
    {
        switch (formulaType)
        {
            case HealFormulaType.Fixed: return "固定値回復";
            case HealFormulaType.MaxHpPercent: return "最大HP%回復";
            case HealFormulaType.IntMultiplier: return "INT×倍率回復";
            case HealFormulaType.StrMultiplier: return "STR×倍率回復";
            default: return formulaType.ToString();
        }
    }

    // =========================================================
    // 説明
    // =========================================================
    private void DrawDescriptionSection()
    {
        EditorGUILayout.LabelField("説明", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (string.IsNullOrWhiteSpace(skill.description))
        {
            EditorGUILayout.LabelField("説明なし");
        }
        else
        {
            EditorGUILayout.HelpBox(skill.description, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }
}