using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// スキル＆エフェクトのビューアーウィンドウ。
/// MonsterDatabaseViewer と同じスタイルで、
/// スキル一覧（Skilllist フォルダ）とエフェクト一覧（Skilleffect フォルダ）を表示する。
///
/// スキルはデータベースを介さず、フォルダを直接スキャンして表示する。
/// 理由: スキルは武器・魔法・モンスター行動から参照されるため、
///       独立したデータベースSOを持つ必要がない。
/// </summary>
public class SkillDatabaseViewer : EditorWindow
{
    private Vector2 scrollPos;
    private string searchText = "";

    // スキル一覧（キャッシュ）
    private List<SkillData> cachedSkills = new();
    private bool showSkillSection = true;

    // エフェクト一覧（キャッシュ）
    private List<SkillEffectData> cachedEffects = new();
    private bool showEffectSection = true;

    private const string SkillFolderPath = "Assets/ScriptableAsset/Skilllist";
    private const string EffectFolderPath = "Assets/ScriptableAsset/Skilleffect";

    [MenuItem("Tools/Skill & Effect Viewer")]
    public static void Open()
    {
        GetWindow<SkillDatabaseViewer>("Skill & Effect Viewer");
    }

    private void OnEnable()
    {
        RefreshSkillList();
        RefreshEffectList();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Skill & Effect Viewer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        searchText = EditorGUILayout.TextField("Search", searchText);

        EditorGUILayout.Space();

        // =========================================================
        // 更新ボタン
        // =========================================================
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("スキルリスト更新", GUILayout.Height(28)))
        {
            RefreshSkillList();
        }

        if (GUILayout.Button("エフェクトリスト更新", GUILayout.Height(28)))
        {
            RefreshEffectList();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // =========================================================
        // スキル一覧
        // =========================================================
        showSkillSection = EditorGUILayout.Foldout(showSkillSection,
            $"■ スキル一覧（{cachedSkills.Count} 件）", true, EditorStyles.foldoutHeader);

        if (showSkillSection)
        {
            if (cachedSkills.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "スキルが見つかりません。\n" +
                    $"パス: {SkillFolderPath}/",
                    MessageType.Info);
            }
            else
            {
                DrawSkillHeader();

                foreach (var skill in cachedSkills)
                {
                    if (skill == null) continue;
                    if (!IsMatchSkill(skill, searchText)) continue;

                    DrawSkillRow(skill);
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // =========================================================
        // エフェクト一覧
        // =========================================================
        showEffectSection = EditorGUILayout.Foldout(showEffectSection,
            $"■ エフェクト一覧（{cachedEffects.Count} 件）", true, EditorStyles.foldoutHeader);

        if (showEffectSection)
        {
            if (cachedEffects.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "エフェクトが見つかりません。\n" +
                    $"パス: {EffectFolderPath}/",
                    MessageType.Info);
            }
            else
            {
                DrawEffectHeader();

                foreach (var effect in cachedEffects)
                {
                    if (effect == null) continue;
                    if (!IsMatchEffect(effect, searchText)) continue;

                    DrawEffectRow(effect);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // スキルリスト読み込み
    // =========================================================
    private void RefreshSkillList()
    {
        cachedSkills.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SkillData",
            new[] { SkillFolderPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill != null)
            {
                cachedSkills.Add(skill);
            }
        }

        // ID でソート
        cachedSkills.Sort((a, b) =>
        {
            string idA = a.skillId ?? "";
            string idB = b.skillId ?? "";
            return string.Compare(idA, idB, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // =========================================================
    // エフェクトリスト読み込み
    // =========================================================
    private void RefreshEffectList()
    {
        cachedEffects.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SkillEffectData",
            new[] { EffectFolderPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var effect = AssetDatabase.LoadAssetAtPath<SkillEffectData>(path);
            if (effect != null)
            {
                cachedEffects.Add(effect);
            }
        }

        // effectName でソート
        cachedEffects.Sort((a, b) =>
        {
            string nameA = a.effectName ?? a.name ?? "";
            string nameB = b.effectName ?? b.name ?? "";
            return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // =========================================================
    // スキル用ヘッダー・行
    // =========================================================
    private void DrawSkillHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("名前", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("ソース", EditorStyles.boldLabel, GUILayout.Width(70));
        GUILayout.Label("属性", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.Label("倍率/固定", EditorStyles.boldLabel, GUILayout.Width(80));
        GUILayout.Label("追加効果", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        GUILayout.Label("詳細", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSkillRow(SkillData skill)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(skill.skillId ?? "", GUILayout.Width(140));
        GUILayout.Label(skill.skillName ?? "", GUILayout.Width(140));
        GUILayout.Label(skill.skillSource.ToString(), GUILayout.Width(70));
        GUILayout.Label(skill.skillAttribute.ToJapanese(), GUILayout.Width(60));

        // 倍率/固定ダメージの表示
        string dmgStr;
        if (skill.IsNonDamage)
            dmgStr = "効果のみ";
        else if (skill.fixedDamage > 0)
            dmgStr = $"固定{skill.fixedDamage}";
        else
            dmgStr = $"x{skill.damageMultiplier}";
        GUILayout.Label(dmgStr, GUILayout.Width(80));

        // 追加効果の数
        int effectCount = (skill.additionalEffects != null) ? skill.additionalEffects.Count : 0;
        GUILayout.Label(effectCount > 0 ? effectCount.ToString() : "-", GUILayout.Width(60));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("詳細", GUILayout.Width(60)))
        {
            SkillDetailWindow.Open(skill);
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // エフェクト用ヘッダー・行
    // =========================================================
    private void DrawEffectHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("アセット名", EditorStyles.boldLabel, GUILayout.Width(180));
        GUILayout.Label("効果名", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("ジャンル", EditorStyles.boldLabel, GUILayout.Width(180));
        GUILayout.FlexibleSpace();
        GUILayout.Label("詳細", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEffectRow(SkillEffectData effect)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(effect.name ?? "", GUILayout.Width(180));
        GUILayout.Label(effect.effectName ?? "", GUILayout.Width(140));
        GUILayout.Label(GetEffectGenreName(effect), GUILayout.Width(180));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("詳細", GUILayout.Width(60)))
        {
            SkillEffectDetailWindow.Open(effect);
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // ユーティリティ
    // =========================================================
    private string GetEffectGenreName(SkillEffectData effect)
    {
        if (effect is StatusAilmentEffectData) return "状態異常（付与/回復）";
        if (effect is HealEffectData healData) return $"HP回復（{healData.formulaType}）";
        if (effect is LevelDrainEffectData) return "レベルドレイン";
        return effect.GetType().Name;
    }

    private bool IsMatchSkill(SkillData skill, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        keyword = keyword.ToLower();
        return (!string.IsNullOrEmpty(skill.skillId) && skill.skillId.ToLower().Contains(keyword))
            || (!string.IsNullOrEmpty(skill.skillName) && skill.skillName.ToLower().Contains(keyword));
    }

    private bool IsMatchEffect(SkillEffectData effect, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        keyword = keyword.ToLower();
        return (!string.IsNullOrEmpty(effect.effectName) && effect.effectName.ToLower().Contains(keyword))
            || (!string.IsNullOrEmpty(effect.name) && effect.name.ToLower().Contains(keyword));
    }
}