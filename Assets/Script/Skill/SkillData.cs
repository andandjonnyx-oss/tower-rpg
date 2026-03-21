using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic")]
    public string skillId;
    public string skillName;
    [TextArea] public string description;

    [Header("Weapon")]
    public WeaponAttribute skillAttribute = WeaponAttribute.Strike;
    public float damageMultiplier;

    [Header("Cooldown")]
    public int cooldownTurns;
}