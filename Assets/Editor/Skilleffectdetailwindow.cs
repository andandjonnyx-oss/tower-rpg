using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// エフェクト詳細ウィンドウ。
/// SkillEffectData（SO）の情報と、このエフェクトを使用しているスキルの一覧を表示する。
/// </summary>
public class SkillEffectDetailWindow : EditorWindow
{
    private SkillEffectData effectData;
    private Vector2 scrollPos;

    // このエフェクトを参照しているスキル一覧（キャッシュ）
    private List<SkillData> referencingSkills = new();

    public static void Open(SkillEffectData target)
    {
        var window = GetWindow<SkillEffectDetailWindow>("Effect Detail");
        window.SetEffect(target);
        window.minSize = new Vector2(420, 400);
        window.Show();
    }

    public void SetEffect(SkillEffectData target)
    {
        effectData = target;
        RefreshReferencingSkills();
        Repaint();
    }

    private void OnGUI()
    {
        if (effectData == null)
        {
            EditorGUILayout.HelpBox("エフェクトが選択されていません。", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawBasicSection();
        EditorGUILayout.Space();

        DrawGenreSection();
        EditorGUILayout.Space();

        DrawReferencingSkillsSection();
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

        EditorGUILayout.LabelField("アセット名", effectData.name ?? "");
        EditorGUILayout.LabelField("効果名", effectData.effectName ?? "");
        EditorGUILayout.LabelField("クラス", effectData.GetType().Name);

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // ジャンル固有情報
    // =========================================================
    private void DrawGenreSection()
    {
        EditorGUILayout.LabelField("ジャンル固有情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (effectData is StatusAilmentEffectData)
        {
            EditorGUILayout.LabelField("ジャンル", "状態異常（付与/回復）");
            EditorGUILayout.LabelField("備考", "状態異常の種類やモードはスキル側の SkillEffectEntry で設定");
        }
        else if (effectData is HealEffectData healData)
        {
            EditorGUILayout.LabelField("ジャンル", "HP回復");
            EditorGUILayout.LabelField("計算式タイプ", healData.formulaType.ToString());

            string formulaDesc;
            switch (healData.formulaType)
            {
                case HealFormulaType.Fixed:
                    formulaDesc = "intValue がそのまま回復量";
                    break;
                case HealFormulaType.MaxHpPercent:
                    formulaDesc = "最大HP × intValue%";
                    break;
                case HealFormulaType.IntMultiplier:
                    formulaDesc = "INT × intValue";
                    break;
                case HealFormulaType.StrMultiplier:
                    formulaDesc = "STR × intValue";
                    break;
                default:
                    formulaDesc = "-";
                    break;
            }
            EditorGUILayout.LabelField("計算式", formulaDesc);
        }
        else if (effectData is LevelDrainEffectData)
        {
            EditorGUILayout.LabelField("ジャンル", "レベルドレイン");
            EditorGUILayout.LabelField("備考", "intValue = ドレイン量、chance = 発動率");
        }
        else
        {
            EditorGUILayout.LabelField("ジャンル", "不明（" + effectData.GetType().Name + "）");
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 参照しているスキル一覧
    // =========================================================
    private void DrawReferencingSkillsSection()
    {
        EditorGUILayout.LabelField($"使用しているスキル（{referencingSkills.Count} 件）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (referencingSkills.Count == 0)
        {
            EditorGUILayout.LabelField("このエフェクトを使用しているスキルはありません。");
        }
        else
        {
            for (int i = 0; i < referencingSkills.Count; i++)
            {
                var sk = referencingSkills[i];
                if (sk == null) continue;

                EditorGUILayout.BeginHorizontal();

                string label = $"{sk.skillId ?? ""} - {sk.skillName ?? ""}";
                EditorGUILayout.LabelField(label);

                if (GUILayout.Button("詳細", GUILayout.Width(60)))
                {
                    SkillDetailWindow.Open(sk);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();

        if (GUILayout.Button("参照スキル再検索", GUILayout.Height(24)))
        {
            RefreshReferencingSkills();
        }
    }

    // =========================================================
    // 説明
    // =========================================================
    private void DrawDescriptionSection()
    {
        EditorGUILayout.LabelField("説明", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (string.IsNullOrWhiteSpace(effectData.description))
        {
            EditorGUILayout.LabelField("説明なし");
        }
        else
        {
            EditorGUILayout.HelpBox(effectData.description, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 参照スキル検索
    // =========================================================
    private void RefreshReferencingSkills()
    {
        referencingSkills.Clear();
        if (effectData == null) return;

        // 全 SkillData アセットを検索
        string[] guids = AssetDatabase.FindAssets("t:SkillData");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill == null) continue;
            if (skill.additionalEffects == null) continue;

            for (int i = 0; i < skill.additionalEffects.Count; i++)
            {
                var entry = skill.additionalEffects[i];
                if (entry != null && entry.effectData == effectData)
                {
                    referencingSkills.Add(skill);
                    break; // 同じスキルを複数回追加しない
                }
            }
        }

        // ID でソート
        referencingSkills.Sort((a, b) =>
        {
            string idA = a.skillId ?? "";
            string idB = b.skillId ?? "";
            return string.Compare(idA, idB, System.StringComparison.OrdinalIgnoreCase);
        });
    }
}