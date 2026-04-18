using UnityEngine;

/// <summary>
/// ItemboxContext / StorageContext 共通のアイテム操作ヘルパー。
/// ボタン構築ロジックとアイテム効果適用を一元管理し、
/// 新フラグ追加時に片方だけ更新漏れが起きる事故を防ぐ。
/// </summary>
public static class ItemActionHelper
{
    // =========================================================
    // ボタン構築ヘルパー
    // =========================================================

    /// <summary>
    /// 「捨てる」/「捨てるな」ボタンを構築する。
    /// cannotDiscard なら無効化ラベルを返す。
    /// </summary>
    public static DetailButtonDef BuildDiscardButton(
        InventoryItem invItem, System.Action discardAction)
    {
        if (invItem.data.cannotDiscard)
            return new DetailButtonDef("捨てるな", null, interactable: false);
        else
            return new DetailButtonDef("捨てる", discardAction);
    }

    /// <summary>
    /// 消費アイテムの「使う」/「与える」ボタンを構築する。
    /// battleOnly / bossFeed チェック込み。
    /// ボタンが不要な場合は null を返す。
    /// </summary>
    public static DetailButtonDef BuildUseConsumableButton(
        InventoryItem invItem, bool inBattle, System.Action useAction)
    {
        // battleOnly のアイテムは非バトル時に使えない
        if (invItem.data.battleOnly && !inBattle)
            return null;

        // 餌付け判定
        bool isBossFeed = inBattle
                          && invItem.data.bossFeedItem
                          && BattleContext.EnemyMonster != null
                          && BattleContext.EnemyMonster.acceptsFeedItem;

        string label = isBossFeed ? "与える" : "使う";
        return new DetailButtonDef(label, useAction);
    }

    /// <summary>
    /// 武器の「食べる」ボタンを構築する。
    /// isEdible でなければ null を返す。
    /// </summary>
    public static DetailButtonDef BuildEatWeaponButton(
        InventoryItem invItem, System.Action eatAction)
    {
        if (!invItem.data.isEdible)
            return null;

        return new DetailButtonDef("食べる", eatAction);
    }

    // =========================================================
    // 効果適用ヘルパー
    // =========================================================

    /// <summary>
    /// 消費アイテムの効果を適用する（HP/MP回復、状態異常治療、SP付与）。
    /// RemoveItem の前に呼ぶこと。
    /// </summary>
    public static void ApplyConsumableEffects(InventoryItem invItem)
    {
        if (invItem?.data == null || GameState.I == null) return;

        // HP回復
        if (invItem.data.healAmount > 0)
        {
            GameState.I.currentHp += invItem.data.healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        // MP回復
        if (invItem.data.mpHealAmount > 0)
        {
            GameState.I.currentMp += invItem.data.mpHealAmount;
            if (GameState.I.currentMp > GameState.I.maxMp)
                GameState.I.currentMp = GameState.I.maxMp;
        }

        // 状態異常回復
        ApplyCureEffects(
            invItem.data.curesPoison,
            invItem.data.curesParalyze,
            invItem.data.curesBlind,
            invItem.data.curesSilence,
            invItem.data.curesPetrify,
            invItem.data.curesCharm,    // ← 追加
            invItem.data.curesCurse,    // ← 追加
            invItem.data.curesGlass);

        // ステータスポイント付与
        if (invItem.data.statusPointGain > 0)
        {
            GameState.I.statusPoint += invItem.data.statusPointGain;
            Debug.Log($"[ItemAction] ステータスポイント +{invItem.data.statusPointGain} (合計: {GameState.I.statusPoint})");
        }
    }

    /// <summary>
    /// 武器を食べたときの効果を適用する（HP回復、全 eatCures 治療）。
    /// RemoveItem の前に呼ぶこと。
    /// </summary>
    public static void ApplyEatWeaponEffects(InventoryItem invItem)
    {
        if (invItem?.data == null || GameState.I == null) return;

        // HP回復
        if (invItem.data.eatHealAmount > 0)
        {
            GameState.I.currentHp += invItem.data.eatHealAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        // 状態異常回復
        ApplyCureEffects(
            invItem.data.eatCuresPoison,
            invItem.data.eatCuresParalyze,
            invItem.data.eatCuresBlind,
            invItem.data.eatCuresSilence,
            invItem.data.eatCuresPetrify,
            invItem.data.eatCuresCharm,    // ← 追加
            invItem.data.eatCuresCurse,    // ← 追加
            invItem.data.eatCuresGlass);
    }

    /// <summary>
    /// 装備中の武器を食べる場合の装備解除。
    /// </summary>
    public static void UnequipIfNeeded(InventoryItem invItem)
    {
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
        {
            GameState.I.equippedWeaponUid = "";
            Debug.Log($"[ItemAction] 食べる前に装備を外した: {invItem.data.itemName}");
        }
    }

    // =========================================================
    // 内部ヘルパー
    // =========================================================

    /// <summary>
    /// 状態異常回復の共通処理。UseConsumable / EatWeapon 両方から呼ばれる。
    /// </summary>
    private static void ApplyCureEffects(
        bool poison, bool paralyze, bool blind, bool silence, bool petrify,
        bool charm, bool curse, bool glass)
    {
        if (poison) StatusEffectSystem.CurePlayerPoison();
        if (paralyze) StatusEffectSystem.CurePlayer(StatusEffect.Paralyze);
        if (blind) StatusEffectSystem.CurePlayer(StatusEffect.Blind);
        if (silence) StatusEffectSystem.CurePlayer(StatusEffect.Silence);
        if (petrify) StatusEffectSystem.CurePlayer(StatusEffect.Petrify);
        if (charm) StatusEffectSystem.CurePlayer(StatusEffect.Charm);
        if (curse) StatusEffectSystem.CurePlayer(StatusEffect.Curse);
        if (glass) StatusEffectSystem.CurePlayer(StatusEffect.Glass);
    }
}