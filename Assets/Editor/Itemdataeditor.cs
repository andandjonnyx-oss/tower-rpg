using UnityEditor;
using UnityEngine;

/// <summary>
/// ItemData のカスタムインスペクター。
/// category（Consumable / Weapon / Magic）に応じて
/// 関連するフィールドのみを表示し、誤設定を防ぐ。
/// 共通フィールドは常に表示する。
/// </summary>
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    // 折りたたみ状態（EditorPrefs で永続化）
    private static bool foldCommon = true;
    private static bool foldConsumable = true;
    private static bool foldWeapon = true;
    private static bool foldMagic = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var categoryProp = serializedObject.FindProperty("category");
        ItemCategory cat = (ItemCategory)categoryProp.enumValueIndex;

        // =========================================================
        // 共通フィールド（全カテゴリ共通）
        // =========================================================
        foldCommon = EditorGUILayout.Foldout(foldCommon, "■ 共通", true, EditorStyles.foldoutHeader);
        if (foldCommon)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Minfloor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Minstep"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Maxfloor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Maxstep"));

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(categoryProp);

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cannotDiscard"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("transformInto"));

            var transformIntoProp = serializedObject.FindProperty("transformInto");
            if (transformIntoProp.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("transformChance"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("sortOrder"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // =========================================================
        // カテゴリ別に色付きヘッダーで表示
        // =========================================================
        switch (cat)
        {
            case ItemCategory.Consumable:
                DrawConsumableSection();
                break;
            case ItemCategory.Weapon:
                DrawWeaponSection();
                break;
            case ItemCategory.Magic:
                DrawMagicSection();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================
    // Consumable セクション
    // =========================================================
    private void DrawConsumableSection()
    {
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // 緑系
        foldConsumable = EditorGUILayout.Foldout(foldConsumable, "★ 消費アイテム専用", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = Color.white;

        if (!foldConsumable) return;

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("回復", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("healAmount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mpHealAmount"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("状態異常回復", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesPoison"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesParalyze"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesBlind"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesSilence"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesPetrify"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesCharm"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesCurse"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("curesGlass"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("ステータスポイント", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("statusPointGain"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("戦闘攻撃アイテム", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleDamage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleAttribute"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleDamageCategory"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleOnly"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("ボス餌付け", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bossFeedItem"));

        EditorGUI.indentLevel--;
    }

    // =========================================================
    // Weapon セクション
    // =========================================================
    private void DrawWeaponSection()
    {
        GUI.backgroundColor = new Color(1f, 0.8f, 0.7f); // オレンジ系
        foldWeapon = EditorGUILayout.Foldout(foldWeapon, "★ 武器専用", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = Color.white;

        if (!foldWeapon) return;

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("基本性能", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponAttribute"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackPower"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseHitRate"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("武器スキル", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skills"), true);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("通常攻撃時の状態異常付与", EditorStyles.boldLabel);
        DrawFilteredEnumPopup(serializedObject.FindProperty("weaponInflictEffect"),
            "Weapon Inflict Effect", IsAilmentOrNone);
        var inflictProp = serializedObject.FindProperty("weaponInflictEffect");
        if (inflictProp.enumValueIndex != 0) // None 以外
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponInflictChance"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("通常攻撃時のデバフ付与", EditorStyles.boldLabel);
        DrawFilteredEnumPopup(serializedObject.FindProperty("weaponInflictDebuff"),
            "Weapon Inflict Debuff", IsBuffDebuffOrNone);
        var debuffProp = serializedObject.FindProperty("weaponInflictDebuff");
        if (debuffProp.enumValueIndex != 0) // None 以外
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponDebuffRate"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponDebuffDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponDebuffChance"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("装備ステータス補正", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipDefense"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipMagicAttack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipMagicDefense"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipLuck"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipMaxHp"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipMaxMp"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("命中・回避・クリティカル補正", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipAccuracy"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipEvasion"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipCritical"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("装備耐性", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipResistances"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("equipStatusEffectResistances"), true);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("食べられる武器", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isEdible"));

        var isEdibleProp = serializedObject.FindProperty("isEdible");
        if (isEdibleProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatHealAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresPoison"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresParalyze"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresBlind"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresSilence"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresPetrify"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresCharm"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresCurse"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eatCuresGlass"));
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    // =========================================================
    // Magic セクション
    // =========================================================
    private void DrawMagicSection()
    {
        GUI.backgroundColor = new Color(0.7f, 0.8f, 1f); // 青系
        foldMagic = EditorGUILayout.Foldout(foldMagic, "★ 魔法アイテム専用", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = Color.white;

        if (!foldMagic) return;

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("魔法スキル", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("magicSkill"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("パッシブ効果", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("passiveEffects"), true);

        EditorGUI.indentLevel--;
    }

    // =========================================================
    // フィルタ付き enum ポップアップ
    // =========================================================

    /// <summary>
    /// StatusEffect enum の中から filter に合致する値のみをポップアップに表示する。
    /// </summary>
    private void DrawFilteredEnumPopup(SerializedProperty prop, string label,
        System.Func<StatusEffect, bool> filter)
    {
        // フィルタに合致する値を収集
        var allValues = System.Enum.GetValues(typeof(StatusEffect));
        var filtered = new System.Collections.Generic.List<StatusEffect>();
        var names = new System.Collections.Generic.List<string>();

        foreach (StatusEffect val in allValues)
        {
            if (filter(val))
            {
                filtered.Add(val);
                names.Add(val.ToString());
            }
        }

        // 現在の値がフィルタ内のどこにあるか
        StatusEffect current = (StatusEffect)prop.enumValueIndex;
        int selectedIndex = filtered.IndexOf(current);
        if (selectedIndex < 0) selectedIndex = 0; // 見つからなければ None（先頭）

        int newIndex = EditorGUILayout.Popup(label, selectedIndex, names.ToArray());
        if (newIndex >= 0 && newIndex < filtered.Count)
        {
            prop.enumValueIndex = (int)filtered[newIndex];
        }
    }

    /// <summary>状態異常（+ None）のみ通すフィルタ。</summary>
    private static bool IsAilmentOrNone(StatusEffect e)
    {
        switch (e)
        {
            case StatusEffect.None:
            case StatusEffect.Poison:
            case StatusEffect.Stun:
            case StatusEffect.Paralyze:
            case StatusEffect.Blind:
            case StatusEffect.Silence:
            case StatusEffect.Petrify:
            case StatusEffect.Charm:
            case StatusEffect.Curse:
            case StatusEffect.Glass:
            case StatusEffect.Rage:
                return true;
            default:
                return false;
        }
    }

    /// <summary>バフ/デバフ（+ None）のみ通すフィルタ。</summary>
    private static bool IsBuffDebuffOrNone(StatusEffect e)
    {
        switch (e)
        {
            case StatusEffect.None:
            case StatusEffect.DefenseDown:
            case StatusEffect.DefenseUp:
            case StatusEffect.AttackDown:
            case StatusEffect.AttackUp:
            case StatusEffect.MagicAttackDown:
            case StatusEffect.MagicAttackUp:
            case StatusEffect.MagicDefenseDown:
            case StatusEffect.MagicDefenseUp:
            case StatusEffect.LuckDown:
            case StatusEffect.LuckUp:
                return true;
            default:
                return false;
        }
    }

}