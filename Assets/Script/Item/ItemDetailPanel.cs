using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテム詳細パネル（全シーン共通）。
/// アイテム情報を表示し、IItemContext から受け取ったボタン定義に従ってボタンを動的に構成する。
/// バトル中に武器を選択した場合、スキルのクールタイム情報を表示する。
/// </summary>
public class ItemDetailPanel : MonoBehaviour
{
    [Header("Root (SetActive で表示/非表示)")]
    [SerializeField] private GameObject detailRoot;

    [Header("Item Info")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image itemImage;

    [Header("Skill Cooldown (バトル中のみ表示)")]
    [SerializeField] private TMP_Text skillCooldownText;

    [Header("Buttons (Inspector でアサイン、最大数分用意)")]
    [SerializeField] private Button[] buttons;
    [SerializeField] private TMP_Text[] buttonTexts;

    private void Start()
    {
    }

    private void Awake()
    {
        Hide();
    }

    /// <summary>
    /// 詳細パネルを表示する。
    /// </summary>
    public void Show(InventoryItem invItem, IItemContext context, bool fromInventory)
    {
        if (invItem?.data == null) { Hide(); return; }

        var data = invItem.data;

        // アイテム情報
        if (itemNameText != null) itemNameText.text = data.itemName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (itemImage != null)
        {
            itemImage.sprite = data.icon;
            itemImage.enabled = data.icon != null;
        }

        // スキルクールタイム表示（バトル中 + 武器のみ）
        UpdateSkillCooldownDisplay(invItem);

        // ボタン構成
        var buttonDefs = context.GetButtons(invItem, fromInventory);
        SetupButtons(buttonDefs);

        if (detailRoot != null) detailRoot.SetActive(true);
    }

    /// <summary>
    /// 詳細パネルを非表示にする。
    /// </summary>
    public void Hide()
    {
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    /// <summary>
    /// バトル中かつ武器の場合、スキルのクールタイム情報を表示する。
    /// それ以外では非表示。
    /// </summary>
    private void UpdateSkillCooldownDisplay(InventoryItem invItem)
    {
        if (skillCooldownText == null) return;

        // バトル中でなければ非表示
        bool inBattle = GameState.I != null && GameState.I.isInBattle;
        if (!inBattle || invItem?.data == null || invItem.data.category != ItemCategory.Weapon)
        {
            skillCooldownText.gameObject.SetActive(false);
            return;
        }

        // 武器にスキルがなければ非表示
        if (invItem.data.skills == null || invItem.data.skills.Length == 0)
        {
            skillCooldownText.gameObject.SetActive(false);
            return;
        }

        // スキルごとのCT情報を組み立てる
        var lines = new List<string>();
        for (int i = 0; i < invItem.data.skills.Length; i++)
        {
            var skill = invItem.data.skills[i];
            if (skill == null) continue;

            int remaining = 0;
            if (invItem.skillCooldowns.ContainsKey(skill.skillId))
                remaining = invItem.skillCooldowns[skill.skillId];

            if (remaining > 0)
                lines.Add($"{skill.skillName}：CT{remaining}");
            else
                lines.Add($"{skill.skillName}：使用可能");
        }

        if (lines.Count > 0)
        {
            skillCooldownText.text = string.Join("\n", lines);
            skillCooldownText.gameObject.SetActive(true);
        }
        else
        {
            skillCooldownText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// ボタンを役割(role)ごとの固定スロットに配置する。
    /// スロット: 0=左上(Primary), 1=右上(Secondary), 2=左下(Discard), 3=右下(Transfer)
    /// 該当ロールのボタンが無いスロットは非表示（位置は詰めない）。
    /// 同一ロールが複数来た場合は最初の1つを採用し、以降は無視する。
    /// </summary>
    private void SetupButtons(List<DetailButtonDef> defs)
    {
        if (buttons == null) return;

        // role → スロットindex の対応
        // ButtonRole の値（Primary=0,Secondary=1,Discard=2,Transfer=3）が
        // そのままスロットindexと一致する設計。
        // 各スロットに入れる def を決める（無ければ null）。
        DetailButtonDef[] slotDefs = new DetailButtonDef[buttons.Length];

        if (defs != null)
        {
            foreach (var def in defs)
            {
                if (def == null) continue;
                int slot = (int)def.role;
                if (slot < 0 || slot >= slotDefs.Length) continue; // 安全
                if (slotDefs[slot] == null)        // 既に埋まっていれば無視
                    slotDefs[slot] = def;
            }
        }

        // 各スロットを設定
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            var def = slotDefs[i];
            if (def != null)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].interactable = def.interactable;

                if (buttonTexts != null && i < buttonTexts.Length && buttonTexts[i] != null)
                    buttonTexts[i].text = def.label;

                buttons[i].onClick.RemoveAllListeners();
                var action = def.onClick;
                buttons[i].onClick.AddListener(() => action?.Invoke());
            }
            else
            {
                // 該当ロールのボタンが無いスロットは非表示（位置は維持）
                buttons[i].gameObject.SetActive(false);
            }
        }
    }
}