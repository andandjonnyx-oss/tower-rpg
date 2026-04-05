using UnityEditor;
using UnityEngine;

public class ItemDetailWindow : EditorWindow
{
    private ItemData item;
    private Vector2 scrollPos;

    public static void Open(ItemData target)
    {
        var window = GetWindow<ItemDetailWindow>("Item Detail");
        window.SetItem(target);
        window.minSize = new Vector2(420, 520);
        window.Show();
    }

    public void SetItem(ItemData target)
    {
        item = target;
        Repaint();
    }

    private void OnGUI()
    {
        if (item == null)
        {
            EditorGUILayout.HelpBox("アイテムが選択されていません。", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawBasicSection();
        EditorGUILayout.Space();

        DrawRangeSection();
        EditorGUILayout.Space();

        DrawStackSection();
        EditorGUILayout.Space();

        // カテゴリ別のセクションを表示
        switch (item.category)
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

        EditorGUILayout.LabelField("ID", item.itemId);
        EditorGUILayout.LabelField("名前", item.itemName);
        EditorGUILayout.LabelField("カテゴリ", item.category.ToString());
        EditorGUILayout.LabelField("ソート順", item.sortOrder.ToString());

        EditorGUILayout.Space();

        if (item.icon != null)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(item.icon);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(item.icon);
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
    private void DrawRangeSection()
    {
        EditorGUILayout.LabelField("出現範囲", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("範囲",
            $"{item.Minfloor}F {item.Minstep}STEP ～ {item.Maxfloor}F {item.Maxstep}STEP");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // スタック設定
    // =========================================================
    private void DrawStackSection()
    {
        EditorGUILayout.LabelField("スタック設定", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Stackable", item.stackable ? "True" : "False");
        EditorGUILayout.LabelField("Max Stack", item.maxStack.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 消費アイテム
    // =========================================================
    private void DrawConsumableSection()
    {
        EditorGUILayout.LabelField("消費アイテム情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("回復量", item.healAmount.ToString());
        EditorGUILayout.LabelField("毒回復", item.curesPoison ? "○" : "×");

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 武器
    // =========================================================
    private void DrawWeaponSection()
    {
        // --- 基本攻撃性能 ---
        EditorGUILayout.LabelField("武器 - 基本性能", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("武器属性", item.weaponAttribute.ToJapanese());
        EditorGUILayout.LabelField("攻撃力", item.attackPower.ToString());
        EditorGUILayout.LabelField("基礎命中率", $"{item.baseHitRate}%");

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- 状態異常付与 ---
        if (item.weaponInflictEffect != StatusEffect.None)
        {
            EditorGUILayout.LabelField("武器 - 状態異常付与", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("付与状態異常", item.weaponInflictEffect.ToString());
            EditorGUILayout.LabelField("付与率", $"{item.weaponInflictChance}%");

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // --- 武器スキル ---
        if (item.skills != null && item.skills.Length > 0)
        {
            EditorGUILayout.LabelField("武器 - スキル", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            for (int i = 0; i < item.skills.Length; i++)
            {
                var skill = item.skills[i];
                if (skill != null)
                {
                    EditorGUILayout.LabelField($"  [{i}]",
                        $"{skill.skillName} ({skill.skillAttribute.ToJapanese()}, x{skill.damageMultiplier}, CT{skill.cooldownTurns})");
                }
                else
                {
                    EditorGUILayout.LabelField($"  [{i}]", "null");
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // --- 装備ステータス ---
        DrawEquipStatsSection();

        // --- 装備属性耐性 ---
        if (item.equipResistances != null && item.equipResistances.Length > 0)
        {
            EditorGUILayout.LabelField("武器 - 装備属性耐性", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            for (int i = 0; i < item.equipResistances.Length; i++)
            {
                var r = item.equipResistances[i];
                if (r != null)
                {
                    EditorGUILayout.LabelField($"  {r.attribute.ToJapanese()}", $"+{r.value}");
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // --- 装備状態異常耐性 ---
        if (item.equipStatusEffectResistances != null && item.equipStatusEffectResistances.Length > 0)
        {
            EditorGUILayout.LabelField("武器 - 装備状態異常耐性", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            for (int i = 0; i < item.equipStatusEffectResistances.Length; i++)
            {
                var r = item.equipStatusEffectResistances[i];
                if (r != null)
                {
                    EditorGUILayout.LabelField($"  {r.statusEffect.ToString()}", $"+{r.value}");
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 装備時ステータス補正を表示する。
    /// 0 でない値のみ表示する。
    /// </summary>
    private void DrawEquipStatsSection()
    {
        // 表示する値があるかチェック
        bool hasAny = item.equipDefense != 0
            || item.equipMagicAttack != 0
            || item.equipMagicDefense != 0
            || item.equipLuck != 0
            || item.equipMaxHp != 0
            || item.equipMaxMp != 0
            || item.equipAccuracy != 0
            || item.equipEvasion != 0
            || item.equipCritical != 0;

        if (!hasAny) return;

        EditorGUILayout.LabelField("武器 - 装備ステータス補正", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (item.equipDefense != 0)
            EditorGUILayout.LabelField("防御力", FormatBonus(item.equipDefense));
        if (item.equipMagicAttack != 0)
            EditorGUILayout.LabelField("魔法攻撃力", FormatBonus(item.equipMagicAttack));
        if (item.equipMagicDefense != 0)
            EditorGUILayout.LabelField("魔法防御力", FormatBonus(item.equipMagicDefense));
        if (item.equipLuck != 0)
            EditorGUILayout.LabelField("運", FormatBonus(item.equipLuck));
        if (item.equipMaxHp != 0)
            EditorGUILayout.LabelField("最大HP", FormatBonus(item.equipMaxHp));
        if (item.equipMaxMp != 0)
            EditorGUILayout.LabelField("最大MP", FormatBonus(item.equipMaxMp));
        if (item.equipAccuracy != 0)
            EditorGUILayout.LabelField("命中力", FormatBonus(item.equipAccuracy));
        if (item.equipEvasion != 0)
            EditorGUILayout.LabelField("回避率", $"{FormatBonus(item.equipEvasion)}%");
        if (item.equipCritical != 0)
            EditorGUILayout.LabelField("クリティカル率", $"{FormatBonus(item.equipCritical)}%");

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    // =========================================================
    // 魔法アイテム
    // =========================================================
    private void DrawMagicSection()
    {
        // --- 魔法スキル ---
        EditorGUILayout.LabelField("魔法 - スキル", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (item.magicSkill != null)
        {
            var ms = item.magicSkill;
            EditorGUILayout.LabelField("スキル名", ms.skillName);
            EditorGUILayout.LabelField("属性", ms.skillAttribute.ToJapanese());

            // ダメージ式プレビュー
            if (ms.IsNonDamage)
            {
                EditorGUILayout.LabelField("ダメージ", "非ダメージ（効果のみ）");
            }
            else if (ms.damageMultiplier > 0f && ms.bonusDamage > 0)
            {
                EditorGUILayout.LabelField("ダメージ", $"MagicAtk×{ms.damageMultiplier:F1}+{ms.bonusDamage}");
            }
            else if (ms.damageMultiplier > 0f)
            {
                EditorGUILayout.LabelField("ダメージ", $"MagicAtk×{ms.damageMultiplier:F1}");
            }
            else
            {
                EditorGUILayout.LabelField("ダメージ", $"固定{ms.bonusDamage}");
            }

            EditorGUILayout.LabelField("MP消費", ms.mpCost.ToString());
        }
        else
        {
            EditorGUILayout.LabelField("魔法スキル", "なし（パッシブ専用）");
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- パッシブ効果 ---
        EditorGUILayout.LabelField("魔法 - パッシブ効果", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (item.passiveEffects != null && item.passiveEffects.Length > 0)
        {
            for (int i = 0; i < item.passiveEffects.Length; i++)
            {
                var pe = item.passiveEffects[i];
                if (pe == null) continue;

                string target = pe.effectType == PassiveType.StatBonus
                    ? pe.targetStat.ToString()
                    : pe.targetAttribute.ToJapanese();
                EditorGUILayout.LabelField($"  [{i}]",
                    $"{pe.effectType.ToJapanese()} ({target}) +{pe.value}");
            }
        }
        else
        {
            EditorGUILayout.LabelField("パッシブ効果", "なし");
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // 説明
    // =========================================================
    private void DrawDescriptionSection()
    {
        EditorGUILayout.LabelField("説明", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (string.IsNullOrWhiteSpace(item.description))
        {
            EditorGUILayout.LabelField("説明なし");
        }
        else
        {
            EditorGUILayout.HelpBox(item.description, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // ユーティリティ
    // =========================================================
    private string FormatBonus(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }
}