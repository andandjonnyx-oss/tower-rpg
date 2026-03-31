using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillEffectEntry のカスタム PropertyDrawer。
/// effectData にアサインされた ScriptableObject のジャンルに応じて、
/// 必要なフィールドだけをインスペクターに表示する。
///
/// 【表示ルール】
///   effectData = null          → effectData のみ表示
///   StatusAilmentEffectData    → ailmentMode, targetStatusEffect, chance（Inflict時のみ）
///   HealEffectData             → intValue（ラベル: SO.formulaType に応じて変化）, chance
///   LevelDrainEffectData       → intValue（ラベル: ドレイン量）, chance
///   その他                     → chance, intValue（汎用表示）
/// </summary>
[CustomPropertyDrawer(typeof(SkillEffectEntry))]
public class SkillEffectEntryDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var effectDataProp = property.FindPropertyRelative("effectData");
        var effectData = effectDataProp.objectReferenceValue as SkillEffectData;

        int lineCount = 1; // effectData は常に1行

        if (effectData == null)
        {
            // effectData 未設定 → 1行のみ
        }
        else if (effectData is StatusAilmentEffectData)
        {
            lineCount += 2; // ailmentMode + targetStatusEffect
            // Inflict モードの場合のみ chance を表示
            var ailmentModeProp = property.FindPropertyRelative("ailmentMode");
            if (ailmentModeProp.enumValueIndex == (int)AilmentMode.Inflict)
            {
                lineCount += 1; // chance
            }
        }
        else if (effectData is HealEffectData)
        {
            lineCount += 2; // intValue + chance
        }
        else if (effectData is LevelDrainEffectData)
        {
            lineCount += 2; // intValue + chance
        }
        else
        {
            lineCount += 2; // chance + intValue（汎用）
        }

        float spacing = 2f;
        return lineCount * (EditorGUIUtility.singleLineHeight + spacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;
        float y = position.y;

        // --- effectData（常に表示）---
        var effectDataProp = property.FindPropertyRelative("effectData");
        var effectDataRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(effectDataRect, effectDataProp, new GUIContent("効果タイプ"));
        y += lineH + spacing;

        var effectData = effectDataProp.objectReferenceValue as SkillEffectData;

        if (effectData == null)
        {
            // effectData 未設定 → 何も表示しない
        }
        else if (effectData is StatusAilmentEffectData)
        {
            DrawStatusAilmentFields(position, property, ref y, lineH, spacing);
        }
        else if (effectData is HealEffectData healData)
        {
            DrawHealFields(position, property, ref y, lineH, spacing, healData);
        }
        else if (effectData is LevelDrainEffectData)
        {
            DrawLevelDrainFields(position, property, ref y, lineH, spacing);
        }
        else
        {
            // 未知のジャンル → 汎用表示
            DrawGenericFields(position, property, ref y, lineH, spacing);
        }

        EditorGUI.EndProperty();
    }

    // =========================================================
    // 状態異常系
    // =========================================================
    private void DrawStatusAilmentFields(Rect position, SerializedProperty property,
        ref float y, float lineH, float spacing)
    {
        // ailmentMode
        var ailmentModeProp = property.FindPropertyRelative("ailmentMode");
        var modeRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(modeRect, ailmentModeProp, new GUIContent("モード"));
        y += lineH + spacing;

        // targetStatusEffect
        var targetProp = property.FindPropertyRelative("targetStatusEffect");
        var targetRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(targetRect, targetProp, new GUIContent("対象状態異常"));
        y += lineH + spacing;

        // chance（Inflict モードの場合のみ）
        if (ailmentModeProp.enumValueIndex == (int)AilmentMode.Inflict)
        {
            var chanceProp = property.FindPropertyRelative("chance");
            var chanceRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.IntSlider(chanceRect, chanceProp, 0, 100, new GUIContent("付与率 (%)"));
            y += lineH + spacing;
        }
    }

    // =========================================================
    // HP回復系
    // =========================================================
    private void DrawHealFields(Rect position, SerializedProperty property,
        ref float y, float lineH, float spacing, HealEffectData healData)
    {
        // intValue（ラベルを formulaType に応じて変更）
        string intLabel = GetHealIntLabel(healData.formulaType);
        var intValueProp = property.FindPropertyRelative("intValue");
        var intRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(intRect, intValueProp, new GUIContent(intLabel));
        y += lineH + spacing;

        // chance
        var chanceProp = property.FindPropertyRelative("chance");
        var chanceRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.IntSlider(chanceRect, chanceProp, 0, 100, new GUIContent("発動率 (%)"));
        y += lineH + spacing;
    }

    private string GetHealIntLabel(HealFormulaType formulaType)
    {
        switch (formulaType)
        {
            case HealFormulaType.Fixed: return "回復量（固定値）";
            case HealFormulaType.MaxHpPercent: return "回復量（最大HP%）";
            case HealFormulaType.IntMultiplier: return "回復量（INT×倍率）";
            case HealFormulaType.StrMultiplier: return "回復量（STR×倍率）";
            default: return "回復量";
        }
    }

    // =========================================================
    // レベルドレイン系
    // =========================================================
    private void DrawLevelDrainFields(Rect position, SerializedProperty property,
        ref float y, float lineH, float spacing)
    {
        // intValue
        var intValueProp = property.FindPropertyRelative("intValue");
        var intRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(intRect, intValueProp, new GUIContent("ドレイン量"));
        y += lineH + spacing;

        // chance
        var chanceProp = property.FindPropertyRelative("chance");
        var chanceRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.IntSlider(chanceRect, chanceProp, 0, 100, new GUIContent("発動率 (%)"));
        y += lineH + spacing;
    }

    // =========================================================
    // 汎用（未知のジャンル用）
    // =========================================================
    private void DrawGenericFields(Rect position, SerializedProperty property,
        ref float y, float lineH, float spacing)
    {
        var chanceProp = property.FindPropertyRelative("chance");
        var chanceRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.IntSlider(chanceRect, chanceProp, 0, 100, new GUIContent("発動率 (%)"));
        y += lineH + spacing;

        var intValueProp = property.FindPropertyRelative("intValue");
        var intRect = new Rect(position.x, y, position.width, lineH);
        EditorGUI.PropertyField(intRect, intValueProp, new GUIContent("数値パラメータ"));
        y += lineH + spacing;
    }
}