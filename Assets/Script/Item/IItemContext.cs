using System;
using System.Collections.Generic;

/// <summary>
/// ボタンの役割。固定スロット配置に使う。
/// Primary=使う/装備/与える等, Secondary=食べる等,
/// Discard=捨てる, Transfer=預ける/引き出す。
/// </summary>
public enum ButtonRole
{
    Primary = 0,
    Secondary = 1,
    Discard = 2,
    Transfer = 3,
}

/// <summary>
/// ボタン1個分の定義。ラベルと、押された時の処理を持つ。
/// </summary>
public class DetailButtonDef
{
    public string label;
    public bool interactable;
    public Action onClick;
    public ButtonRole role;

    public DetailButtonDef(string label, Action onClick,
        bool interactable = true, ButtonRole role = ButtonRole.Primary)
    {
        this.label = label;
        this.onClick = onClick;
        this.interactable = interactable;
        this.role = role;
    }
}

/// <summary>
/// シーンごとのアイテムUI制御インターフェース。
/// 各シーンのコントローラーがこれを実装し、ItemDetailPanel に渡す。
/// </summary>
public interface IItemContext
{
    /// <summary>
    /// 選択されたアイテムに対して表示するボタン一覧を返す。
    /// fromInventory: true=所持品側, false=倉庫側など（シーンによって意味が変わる）
    /// </summary>
    List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory);

    /// <summary>
    /// ボタン操作後にスロット表示を更新する。
    /// </summary>
    void RefreshSlots();
}