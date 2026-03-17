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

        DrawRewardSection();
        EditorGUILayout.Space();

        DrawHelpSection();

        EditorGUILayout.EndScrollView();
    }

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

    private void DrawEncounterSection()
    {
        EditorGUILayout.LabelField("出現範囲", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Min Floor", monster.Minfloor.ToString());
        EditorGUILayout.LabelField("Min Step", monster.Minstep.ToString());
        EditorGUILayout.LabelField("Max Floor", monster.Maxfloor.ToString());
        EditorGUILayout.LabelField("Max Step", monster.Maxstep.ToString());
        EditorGUILayout.LabelField("範囲", $"{monster.Minfloor}F {monster.Minstep}STEP ～ {monster.Maxfloor}F {monster.Maxstep}STEP");

        EditorGUILayout.EndVertical();
    }

    private void DrawSpawnControlSection()
    {
        EditorGUILayout.LabelField("出現制御", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Weight", monster.Weight.ToString());
        EditorGUILayout.LabelField("Is Boss", monster.IsBoss ? "True" : "False");
        EditorGUILayout.LabelField("Is Unique", monster.IsUnique ? "True" : "False");

        EditorGUILayout.EndVertical();
    }

    private void DrawStatsSection()
    {
        EditorGUILayout.LabelField("ステータス", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Max HP", monster.MaxHp.ToString());
        EditorGUILayout.LabelField("Attack", monster.Attack.ToString());
        EditorGUILayout.LabelField("Defense", monster.Defense.ToString());
        EditorGUILayout.LabelField("Speed", monster.Speed.ToString());

        EditorGUILayout.EndVertical();
    }

    private void DrawRewardSection()
    {
        EditorGUILayout.LabelField("報酬", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Exp", monster.Exp.ToString());
        EditorGUILayout.LabelField("Gold", monster.Gold.ToString());

        EditorGUILayout.EndVertical();
    }

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