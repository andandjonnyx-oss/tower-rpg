using System;
using System.Collections.Generic;

[Serializable]
public class InventoryItem
{
    // 所持品1個ごとの固有ID。取得時に GUID で発行する。
    public string uid;
    // マスターデータへの参照。
    public ItemData data;

    public InventoryItem(ItemData data)
    {
        //GUID ランダムな文字列　例："7c9ec9ed-c93a-4d4c-83f3-4a93cc8c767d"
        this.uid = Guid.NewGuid().ToString();
        this.data = data;
    }

    // key = skillId, value = 残りクールタイムターン数
    public Dictionary<string, int> skillCooldowns = new();

    // =========================================================
    // スキル クールタイム ヘルパー
    // =========================================================

    /// <summary>
    /// 指定スキルが使用可能かどうか。
    /// クールダウンが 0 以下、またはまだ辞書に登録されていなければ使用可能。
    /// </summary>
    public bool CanUseSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;
        if (!skillCooldowns.ContainsKey(skillId)) return true;
        return skillCooldowns[skillId] <= 0;
    }

    /// <summary>
    /// スキルを使用し、クールダウンをセットする。
    /// SkillData の cooldownTurns をそのまま登録する。
    /// </summary>
    public void UseSkill(SkillData skill)
    {
        if (skill == null) return;
        skillCooldowns[skill.skillId] = skill.cooldownTurns;
    }

    /// <summary>
    /// 毎ターン呼び出し。全スキルのクールダウンを 1 減算する。
    /// </summary>
    public void TickCooldowns()
    {
        var keys = new List<string>(skillCooldowns.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            skillCooldowns[keys[i]]--;
            if (skillCooldowns[keys[i]] < 0)
                skillCooldowns[keys[i]] = 0;
        }
    }

    /// <summary>
    /// 戦闘終了時に呼び出し。全クールダウンをリセットする。
    /// </summary>
    public void ResetAllCooldowns()
    {
        skillCooldowns.Clear();
    }
}