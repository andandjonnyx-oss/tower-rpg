using System.Collections.Generic;

/// <summary>
/// 魔法セレクターの「前回選択した魔法」を記憶する static クラス。
/// インデックスではなく skillId で記憶するため、
/// リストの並びや内容が変わっても正しい魔法に復元できる。
///
/// 区切り（リセットタイミング）:
///   BattleSkillId : 戦闘の新規開始時にクリア → その戦闘の間だけ保持
///                   （アイテム画面との往復・第二形態のシーン再読込では保持される）
///   FieldSkillId  : 戦闘の新規開始時にクリア → 「戦闘に入るまでの塔内部」が一区切り
///                   （塔内の歩行・会話・倉庫往復では保持される）
///   両方         : Main 帰還時にクリア（MainSceneRecovery から呼ぶ）
///
/// GameSettings.KeepMagicSelection が OFF の場合、Restore は何もしない。
/// </summary>
public static class MagicSelectionMemory
{
    /// <summary>戦闘中に選択した魔法の skillId。</summary>
    public static string BattleSkillId;

    /// <summary>塔内で選択した魔法の skillId。</summary>
    public static string FieldSkillId;

    public static void ClearBattle() => BattleSkillId = null;
    public static void ClearField() => FieldSkillId = null;

    public static void ClearAll()
    {
        BattleSkillId = null;
        FieldSkillId = null;
    }

    /// <summary>
    /// 記憶している skillId に一致する項目がリストにあれば、セレクターの選択を復元する。
    /// SetOptions() の直後に呼ぶ（SetOptions が選択を先頭にリセットするため）。
    /// 一致する項目がない場合（武器変更でスキルが消えた等）は先頭のまま。
    /// </summary>
    public static void Restore(MagicSelector selector, List<SkillData> list, bool isBattle)
    {
        if (!GameSettings.KeepMagicSelection) return;
        if (selector == null || list == null) return;

        string id = isBattle ? BattleSkillId : FieldSkillId;
        if (string.IsNullOrEmpty(id)) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].skillId == id)
            {
                selector.SetValue(i);
                return;
            }
        }
    }
}