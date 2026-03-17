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

        DrawCategorySection();
        EditorGUILayout.Space();

        DrawStackSection();
        EditorGUILayout.Space();

        DrawTypeSpecificSection();
        EditorGUILayout.Space();

        DrawDescriptionSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBasicSection()
    {
        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("ID", item.itemId);
        EditorGUILayout.LabelField("名前", item.itemName);

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

    private void DrawRangeSection()
    {
        EditorGUILayout.LabelField("出現範囲", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Min Floor", item.Minfloor.ToString());
        EditorGUILayout.LabelField("Min Step", item.Minstep.ToString());
        EditorGUILayout.LabelField("Max Floor", item.Maxfloor.ToString());
        EditorGUILayout.LabelField("Max Step", item.Maxstep.ToString());
        EditorGUILayout.LabelField("範囲", $"{item.Minfloor}F {item.Minstep}STEP ～ {item.Maxfloor}F {item.Maxstep}STEP");

        EditorGUILayout.EndVertical();
    }

    private void DrawCategorySection()
    {
        EditorGUILayout.LabelField("カテゴリ", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Category", item.category.ToString());

        EditorGUILayout.EndVertical();
    }

    private void DrawStackSection()
    {
        EditorGUILayout.LabelField("スタック設定", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Stackable", item.stackable ? "True" : "False");
        EditorGUILayout.LabelField("Max Stack", item.maxStack.ToString());

        EditorGUILayout.EndVertical();
    }

    private void DrawTypeSpecificSection()
    {
        EditorGUILayout.LabelField("カテゴリ別情報", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        switch (item.category)
        {
            case ItemCategory.Consumable:
                EditorGUILayout.LabelField("Heal Amount", item.healAmount.ToString());
                break;

            case ItemCategory.Weapon:
                EditorGUILayout.LabelField("Weapon Type", string.IsNullOrWhiteSpace(item.weaponType) ? "なし" : item.weaponType);
                EditorGUILayout.LabelField("Attack Power", item.attackPower.ToString());
                break;

            case ItemCategory.Magic:
                EditorGUILayout.LabelField("Magic ID", string.IsNullOrWhiteSpace(item.magicId) ? "なし" : item.magicId);
                break;
        }

        EditorGUILayout.EndVertical();
    }

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
}