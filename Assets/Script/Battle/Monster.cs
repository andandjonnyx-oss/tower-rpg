using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Monster")]
public class Monster : ScriptableObject
{
    [Header("ID")]
    public string ID;

    [Header("Name")]
    public string Mname;

    [Header("Image")]
    public Sprite Image;

    [Header("Range")]
    public int Minfloor;
    public int Minstep;
    public int Maxfloor;
    public int Maxstep;


    [Header("èoåªêßå‰")]
    public int Weight = 1;
    public bool IsBoss;
    public bool IsUnique;

    [Header("Stats")]
    public int MaxHp;
    public int Attack;
    public int Defense;
    public int Speed;

    [Header("Reward")]
    public int Exp;
    public int Gold;

    [Header("ê‡ñæ")]
    [TextArea(3, 6)]
    public string Help;

}
