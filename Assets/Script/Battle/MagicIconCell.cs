using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 魔法選択ポップアップのアイコンセル（1つ分）。
/// MagicSelector が GridLayoutGroup 配下に Prefab から動的生成する。
/// ItemIconCell と同型の設計。
///
/// 構成:
///   MagicIconCell (Button + Image + MagicIconCell)
///     ├ iconImage      … 魔法アイコン
///     ├ nameText       … 魔法名
///     ├ mpText         … 消費MP（"MP:2" など）
///     └ selectedFrame（任意）… 選択中ハイライト枠
///
/// アイコンは SkillData ではなく魔導書 Item 側（Item.icon）にあるため、
/// セル自身は解決せず、呼び出し元（MagicSelector）が解決して渡した Sprite を表示するだけ。
/// </summary>
public class MagicIconCell : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("魔法アイコン表示用 Image")]
    [SerializeField] private Image iconImage;

    [Tooltip("魔法名表示用 TMP_Text")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("消費MP表示用 TMP_Text")]
    [SerializeField] private TMP_Text mpText;

    [Tooltip("セル全体の Button コンポーネント")]
    [SerializeField] private Button cellButton;

    [Tooltip("（任意）選択中ハイライト枠。未割り当てでも動作する。")]
    [SerializeField] private Image selectedFrame;

    // 内部状態
    private SkillData skill;
    private int index;
    private Action<int> onClickCallback;

    /// <summary>
    /// セルを初期化する。
    /// </summary>
    /// <param name="skill">魔法スキルデータ</param>
    /// <param name="icon">表示するアイコン（魔導書 Item.icon。null 可）</param>
    /// <param name="index">MagicSelector 内のインデックス（onClick で通知する値）</param>
    /// <param name="onClick">タップ時コールバック（引数はこのセルの index）</param>
    public void Setup(SkillData skill, Sprite icon, int index, Action<int> onClick)
    {
        this.skill = skill;
        this.index = index;
        this.onClickCallback = onClick;

        // アイコン
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = (icon != null);
            iconImage.preserveAspect = true;
        }

        // 魔法名
        if (nameText != null)
            nameText.text = (skill != null) ? skill.skillName : "";

        // 消費MP
        if (mpText != null)
            mpText.text = (skill != null) ? $"MP:{skill.mpCost}" : "";

        // ボタン: タップで index を通知
        if (cellButton != null)
        {
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClickCallback?.Invoke(this.index));
        }

        // 選択枠は既定 OFF
        SetSelected(false);
    }

    /// <summary>選択中ハイライトの ON/OFF。selectedFrame 未割り当てなら何もしない。</summary>
    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(selected);
    }

    /// <summary>このセルが指定スキルを表しているか（選択復元・ハイライト用）。</summary>
    public bool RepresentsSkill(SkillData target)
    {
        if (skill == null || target == null) return false;
        if (!string.IsNullOrEmpty(skill.skillId) && !string.IsNullOrEmpty(target.skillId))
            return skill.skillId == target.skillId;
        return skill == target;
    }
}