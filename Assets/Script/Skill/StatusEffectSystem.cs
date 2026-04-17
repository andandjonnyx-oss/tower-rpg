using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状態異常の付与判定・ダメージ計算を担う静的ユーティリティクラス。
///
/// 【持続型デバフ】（戦闘終了後も残る、セーブ対象）
///   毒（Poison）:
///     戦闘中: ターン終了時に最大HPの5%ダメージ（四捨五入、最低1）
///     塔移動中: 1歩ごとに最大HPの3%ダメージ（四捨五入、最低1）、10%で自然治癒
///     戦闘終了後もプレイヤーの毒状態は残る
///   麻痺（Paralyze）:
///     戦闘中: ターン開始時に20%で行動キャンセル（プレイヤー・敵双方）
///     塔移動中: 移動コルーチンのウェイトが3倍、10%で自然治癒
///   暗闇（Blind）:
///     戦闘中: プレイヤーが暗闇→敵の回避力2倍、敵が暗闇→基礎命中率半分
///     塔移動中: 背景が暗転、10%で自然治癒
///   石化（Petrify）:
///     戦闘中: DEF/MDEF倍率増加、残0で敗北（Phase B 実装済み）
///     塔移動中: 10%で残ターン-1（自然治癒ではない専用処理）、残0で30秒ロック
///     自然治癒の対象外。専用アイテム/スキルでのみ解除可能（Phase D）
///
/// 【戦闘限定デバフ】（BattleSceneController側で管理）
///   気絶（Stun）:
///     戦闘中のみ。付与されたターンの敵の行動がスキップされる。
///     1ターン限定で、ターン終了時に自動解除される。
///     プレイヤー→敵のみ対応。
///
/// 【戦闘限定バフ】（BattleSceneController側で管理）
///   怒り（Rage / バーサク）:
///     使用者自身に付与するバフ。攻撃力大幅UP+通常攻撃のみ可能。
///     3ターン or 戦闘終了で解除。
///     敵の場合: actionRange を 1 に固定（最初のアクション＝通常攻撃のみ）。
///
/// 【戦闘限定パラメータバフ/デバフ】（BattleSceneController側で管理）
///   5種ペア: 攻撃(ATK), 魔攻/回避(MATK/EVA), 防御(DEF), 魔防(MDEF), 運(LUC)
///   共通仕様:
///     - 反対効果を解除してから付与（後優先ルール）
///     - 同効果の重ねがけはターン数リセット・率上書き
///     - 戦闘限定（セーブ対象外）
///     - 敵の MagicAttackDown/Up は回避力として適用される
///
/// 付与判定:
///   実質命中率 = 基礎命中率 × (1 - 耐性/100)
///   耐性50なら、ポイズン（80%）の実質命中率は 80 × (1 - 50/100) = 40%
///
/// ★ブラッシュアップ:
///   プレイヤーの状態異常耐性は装備品＋パッシブの合算で計算する。
///   EquipmentCalculator.GetStatusEffectResistance(effect)
///   + PassiveCalculator.CalcStatusEffectResistance(effect)
/// </summary>
public static class StatusEffectSystem
{
    // =========================================================
    // 毒の定数
    // =========================================================

    /// <summary>戦闘中の毒ダメージ: 最大HPの何%か。</summary>
    private const float BattlePoisonPercent = 5f;

    /// <summary>塔移動中の毒ダメージ: 最大HPの何%か。</summary>
    private const float TowerPoisonPercent = 3f;

    /// <summary>塔移動中の自然治癒確率（%）。毒・麻痺・暗闇共通。</summary>
    private const float TowerNaturalCureChance = 10f;

    // =========================================================
    // 麻痺の定数
    // =========================================================

    /// <summary>麻痺による行動キャンセル確率（%）。</summary>
    public const float ParalyzeCancelChance = 20f;

    // =========================================================
    // 沈黙の定数
    // =========================================================
    /// <summary>沈黙中の敵が魔法系スキルを使用した時の失敗確率（%）。</summary>
    public const float SilenceFailChance = 70f;


    // =========================================================
    // 怒りの定数
    // =========================================================

    /// <summary>怒り（バーサク）の持続ターン数。</summary>
    public const int RageDuration = 4;

    /// <summary>怒り（バーサク）の攻撃力倍率。</summary>
    public const float RageAttackMultiplier = 1.5f;

    // =========================================================
    // バフ/デバフの定数
    // =========================================================

    /// <summary>バフ/デバフの持続ターン数のデフォルト値。SkillEffectEntry.duration が 0 の場合に使用。</summary>
    public const int DefaultBuffDebuffDuration = 5;

    // =========================================================
    // 石化の塔歩行定数（Phase C 追加）
    // =========================================================

    /// <summary>塔移動中に石化残ターンが進行する確率（%）。自然治癒ではなく専用処理。</summary>
    private const float TowerPetrifyProgressChance = 10f;

    // =========================================================
    // バフ/デバフ: 全ペアの定義
    // =========================================================

    /// <summary>
    /// バフ/デバフのペア定義。
    /// Down ↔ Up のマッピングに使用する。
    /// </summary>
    private static readonly StatusEffect[][] BuffDebuffPairs = new StatusEffect[][]
    {
        new[] { StatusEffect.DefenseDown,      StatusEffect.DefenseUp },
        new[] { StatusEffect.AttackDown,       StatusEffect.AttackUp },
        new[] { StatusEffect.MagicAttackDown,  StatusEffect.MagicAttackUp },
        new[] { StatusEffect.MagicDefenseDown, StatusEffect.MagicDefenseUp },
        new[] { StatusEffect.LuckDown,         StatusEffect.LuckUp },
    };

    // =========================================================
    // バフ/デバフ: 反対効果のペア判定
    // =========================================================

    /// <summary>
    /// 指定した状態異常の反対効果を返す。
    /// 全バフ/デバフペアに対応。
    /// ペアがない場合は StatusEffect.None を返す。
    /// </summary>
    public static StatusEffect GetOpposite(StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.DefenseDown: return StatusEffect.DefenseUp;
            case StatusEffect.DefenseUp: return StatusEffect.DefenseDown;
            case StatusEffect.AttackDown: return StatusEffect.AttackUp;
            case StatusEffect.AttackUp: return StatusEffect.AttackDown;
            case StatusEffect.MagicAttackDown: return StatusEffect.MagicAttackUp;
            case StatusEffect.MagicAttackUp: return StatusEffect.MagicAttackDown;
            case StatusEffect.MagicDefenseDown: return StatusEffect.MagicDefenseUp;
            case StatusEffect.MagicDefenseUp: return StatusEffect.MagicDefenseDown;
            case StatusEffect.LuckDown: return StatusEffect.LuckUp;
            case StatusEffect.LuckUp: return StatusEffect.LuckDown;
            default: return StatusEffect.None;
        }
    }

    /// <summary>
    /// 指定した状態異常がバフ/デバフ系かどうかを返す。
    /// </summary>
    public static bool IsBuffDebuff(StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.DefenseDown:
            case StatusEffect.DefenseUp:
            case StatusEffect.AttackDown:
            case StatusEffect.AttackUp:
            case StatusEffect.MagicAttackDown:
            case StatusEffect.MagicAttackUp:
            case StatusEffect.MagicDefenseDown:
            case StatusEffect.MagicDefenseUp:
            case StatusEffect.LuckDown:
            case StatusEffect.LuckUp:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 指定した状態異常がデバフ（Down系）かどうかを返す。
    /// バフ（Up系）は false。バフ/デバフ以外も false。
    /// </summary>
    public static bool IsDebuff(StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.DefenseDown:
            case StatusEffect.AttackDown:
            case StatusEffect.MagicAttackDown:
            case StatusEffect.MagicDefenseDown:
            case StatusEffect.LuckDown:
                return true;
            default:
                return false;
        }
    }

    // =========================================================
    // 汎用：付与判定
    // =========================================================

    /// <summary>
    /// 状態異常の付与判定を行う。
    ///
    /// 計算式:
    ///   実質命中率 = baseChance × (1 - targetResistance / 100)
    ///   例: baseChance=80, targetResistance=50 → 80 × 0.5 = 40%
    ///
    /// </summary>
    /// <param name="baseChance">状態異常の基礎付与率（%）。例: ポイズン=80</param>
    /// <param name="targetResistance">対象の状態異常耐性値。例: 毒耐性=50</param>
    /// <returns>true: 付与成功、false: 付与失敗</returns>
    public static bool TryInflict(float baseChance, int targetResistance)
    {
        // 耐性が100以上なら完全耐性
        if (targetResistance >= 100) return false;

        float effectiveChance = baseChance * (1f - targetResistance / 100f);
        if (effectiveChance <= 0f) return false;

        float roll = Random.Range(0f, 100f);
        bool success = roll < effectiveChance;

        Debug.Log($"[StatusEffect] TryInflict: base={baseChance}% resist={targetResistance} " +
                  $"effective={effectiveChance:F2}% roll={roll:F2} success={success}");

        return success;
    }

    // =========================================================
    // 汎用：プレイヤーの状態異常耐性取得
    // =========================================================

    /// <summary>
    /// プレイヤーの指定状態異常の耐性値を返す。
    /// 装備品（100%反映）＋パッシブ（重複ルール適用）の合算。
    ///
    /// Down系（デバフ）が指定された場合は StatusEffect.Debuff の耐性を返す。
    /// これにより、デバフ耐性を装備/パッシブで設定すれば
    /// ATK/DEF/MATK/MDEF/LUC の全デバフに対して一括で耐性が適用される。
    /// </summary>
    public static int GetPlayerResistance(StatusEffect effect)
    {
        // Down系はすべて Debuff（一括耐性）に変換する
        StatusEffect resistKey = effect;
        if (IsDebuff(effect))
        {
            resistKey = StatusEffect.Debuff;
        }

        int equipRes = EquipmentCalculator.GetStatusEffectResistance(resistKey);
        int passiveRes = PassiveCalculator.CalcStatusEffectResistance(resistKey);
        return equipRes + passiveRes;
    }
    // =========================================================
    // 汎用：敵の状態異常耐性取得
    // =========================================================

    /// <summary>
    /// 敵の指定状態異常の耐性値を返す。
    /// Monster.GetStatusEffectResistance() を経由する。
    /// </summary>
    public static int GetEnemyResistance(StatusEffect effect, Monster monster)
    {
        if (monster == null) return 0;
        return monster.GetStatusEffectResistance(effect);
    }

    // =========================================================
    // 汎用：プレイヤーが状態異常にかかっているか
    // =========================================================

    /// <summary>
    /// プレイヤーが指定の持続型状態異常にかかっているかを返す。
    /// 持続型デバフ（Poison/Paralyze/Blind/Silence/Petrify）に対応。
    /// 戦闘限定（Stun/Rage/バフ/デバフ）は BattleSceneController 側で管理。
    /// </summary>
    public static bool IsPlayerAffected(StatusEffect effect)
    {
        if (GameState.I == null) return false;
        switch (effect)
        {
            case StatusEffect.Poison: return GameState.I.isPoisoned;
            case StatusEffect.Paralyze: return GameState.I.isParalyzed;
            case StatusEffect.Blind: return GameState.I.isBlind;
            case StatusEffect.Silence: return GameState.I.isSilenced;
            case StatusEffect.Petrify: return GameState.I.isPetrified;


            default: return false;
        }
    }

    // =========================================================
    // 汎用：プレイヤーの持続型状態異常をセット/解除
    // =========================================================

    /// <summary>
    /// プレイヤーの持続型状態異常フラグを設定する。
    /// Petrify の場合、false（解除）時に残ターン/最大ターンもリセットする。
    /// </summary>
    public static void SetPlayerAilment(StatusEffect effect, bool value)
    {
        if (GameState.I == null) return;
        switch (effect)
        {
            case StatusEffect.Poison: GameState.I.isPoisoned = value; break;
            case StatusEffect.Paralyze: GameState.I.isParalyzed = value; break;
            case StatusEffect.Blind: GameState.I.isBlind = value; break;
            case StatusEffect.Silence: GameState.I.isSilenced = value; break;
            case StatusEffect.Petrify:
                GameState.I.isPetrified = value;
                if (!value)
                {
                    // 石化解除時は残ターン/最大ターンもリセット
                    GameState.I.playerPetrifyTurns = 0;
                    GameState.I.playerPetrifyMaxTurns = 0;
                }
                break;

        }
    }

    // =========================================================
    // 汎用：プレイヤーへの状態異常付与
    // =========================================================

    /// <summary>
    /// プレイヤーに持続型状態異常を付与する試行。耐性を考慮する。
    /// 既に同じ状態異常にかかっている場合は判定せず false を返す。
    /// </summary>
    /// <param name="effect">付与する状態異常</param>
    /// <param name="baseChance">基礎付与率（%）</param>
    /// <returns>true: 付与した、false: 付与失敗（耐性 or 確率外れ or 既に罹患）</returns>
    public static bool TryInflictPlayer(StatusEffect effect, float baseChance)
    {
        if (GameState.I == null) return false;
        if (IsPlayerAffected(effect)) return false;

        int resistance = GetPlayerResistance(effect);
        bool inflicted = TryInflict(baseChance, resistance);

        if (inflicted)
        {
            SetPlayerAilment(effect, true);
            Debug.Log($"[StatusEffect] Player is now {effect}!");
        }

        return inflicted;
    }

    // =========================================================
    // 汎用：プレイヤーの状態異常治癒
    // =========================================================

    /// <summary>
    /// プレイヤーの指定状態異常を治癒する。
    /// </summary>
    public static void CurePlayer(StatusEffect effect)
    {
        if (GameState.I == null) return;
        SetPlayerAilment(effect, false);
        Debug.Log($"[StatusEffect] Player {effect} cured!");
    }

    // =========================================================
    // 汎用：麻痺の行動キャンセル判定
    // =========================================================

    /// <summary>
    /// 麻痺による行動キャンセル判定を行う。
    /// ParalyzeCancelChance（20%）の確率で行動がキャンセルされる。
    /// </summary>
    /// <returns>true: 行動キャンセル（麻痺で動けない）</returns>
    public static bool CheckParalyzeCancel()
    {
        float roll = Random.Range(0f, 100f);
        bool cancel = roll < ParalyzeCancelChance;
        Debug.Log($"[StatusEffect] ParalyzeCancel: chance={ParalyzeCancelChance}% roll={roll:F2} cancel={cancel}");
        return cancel;
    }

    /// <summary>
    /// 沈黙による魔法失敗判定を行う。
    /// SilenceFailChance（70%）の確率で魔法が失敗する。
    /// </summary>
    /// <returns>true: 魔法失敗</returns>
    public static bool CheckSilenceFail()
    {
        float roll = Random.Range(0f, 100f);
        bool fail = roll < SilenceFailChance;
        Debug.Log($"[StatusEffect] SilenceFail: chance={SilenceFailChance}% roll={roll:F2} fail={fail}");
        return fail;
    }



    // =========================================================
    // 毒ダメージ計算
    // =========================================================

    /// <summary>
    /// 戦闘中の毒ダメージを計算する。
    /// 最大HPの5%（四捨五入）。最低1ダメージ保証。
    /// </summary>
    /// <param name="maxHp">対象の最大HP</param>
    /// <returns>毒ダメージ量</returns>
    public static int CalcBattlePoisonDamage(int maxHp)
    {
        int damage = Mathf.FloorToInt(maxHp * BattlePoisonPercent / 100f + 0.5f);
        if (damage < 1) damage = 1;
        return damage;
    }

    /// <summary>
    /// 塔移動中の毒ダメージを計算する。
    /// 最大HPの3%（四捨五入）。最低1ダメージ保証。
    /// </summary>
    /// <param name="maxHp">対象の最大HP</param>
    /// <returns>毒ダメージ量</returns>
    public static int CalcTowerPoisonDamage(int maxHp)
    {
        int damage = Mathf.FloorToInt(maxHp * TowerPoisonPercent / 100f + 0.5f);
        if (damage < 1) damage = 1;
        return damage;
    }

    /// <summary>
    /// 塔移動中の自然治癒判定を行う。
    /// 10%の確率で治癒する。毒・麻痺・暗闇共通。
    /// </summary>
    /// <returns>true: 治癒、false: 継続</returns>
    public static bool TryNaturalCure()
    {
        float roll = Random.Range(0f, 100f);
        bool cured = roll < TowerNaturalCureChance;

        Debug.Log($"[StatusEffect] TryNaturalCure: chance={TowerNaturalCureChance}% " +
                  $"roll={roll:F2} cured={cured}");

        return cured;
    }

    // =========================================================
    // プレイヤーへの毒ダメージ適用（戦闘中）
    // =========================================================

    /// <summary>
    /// 戦闘中のプレイヤー毒ダメージを適用する。
    /// GameState.isPoisoned が true の場合のみダメージを与える。
    /// HPが0以下になっても最低0にクランプする（戦闘不能判定は呼び出し元で行う）。
    /// </summary>
    /// <returns>与えたダメージ量。毒でなければ0。</returns>
    public static int ApplyBattlePoisonToPlayer()
    {
        if (GameState.I == null) return 0;
        if (!GameState.I.isPoisoned) return 0;

        int damage = CalcBattlePoisonDamage(GameState.I.maxHp);
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;

        Debug.Log($"[StatusEffect] Player poison damage: {damage} (HP: {GameState.I.currentHp}/{GameState.I.maxHp})");
        return damage;
    }

    /// <summary>
    /// 塔移動中のプレイヤー毒ダメージを適用し、自然治癒も判定する。
    /// </summary>
    /// <param name="damage">出力: 受けたダメージ量</param>
    /// <param name="cured">出力: 自然治癒したかどうか</param>
    /// <returns>true: 毒状態だった（ダメージ発生）、false: 毒でなかった</returns>
    public static bool ApplyTowerPoisonToPlayer(out int damage, out bool cured)
    {
        damage = 0;
        cured = false;

        if (GameState.I == null) return false;
        if (!GameState.I.isPoisoned) return false;

        // ダメージ適用
        damage = CalcTowerPoisonDamage(GameState.I.maxHp);
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 1) GameState.I.currentHp = 1; // 塔移動中は最低HP1保証

        // 自然治癒判定
        cured = TryNaturalCure();
        if (cured)
        {
            GameState.I.isPoisoned = false;
            Debug.Log("[StatusEffect] Player poison cured naturally!");
        }

        return true;
    }

    // =========================================================
    // 塔移動中の状態異常ステップ処理（統合）
    // =========================================================

    /// <summary>
    /// 塔移動中の全状態異常効果を処理する。
    /// 1歩進むごとに呼ばれる。
    /// 各状態異常の自然治癒判定を行い、ログメッセージを返す。
    /// 毒ダメージの適用もここで行う。
    ///
    /// 麻痺の移動遅延・暗闘の背景暗転は呼び出し元（TowerState）で処理する。
    ///
    /// Phase C 追加: 石化の歩行進行処理。
    /// 10% の確率で残ターン-1。自然治癒ではなく専用処理。
    /// 残ターンが 0 に到達した場合、petrifyReachedZero = true を返す。
    /// 呼び出し元（TowerState）が 30 秒ロックを発動する。
    /// </summary>
    /// <param name="logs">出力: 表示用ログメッセージのリスト</param>
    /// <param name="petrifyReachedZero">出力: 石化残ターンが 0 に到達したか</param>
    public static void ApplyTowerStepEffects(out List<string> logs, out bool petrifyReachedZero)
    {
        logs = new List<string>();
        petrifyReachedZero = false;
        var gs = GameState.I;
        if (gs == null) return;

        // --- 毒: ダメージ + 自然治癒 ---
        if (gs.isPoisoned)
        {
            int dmg;
            bool cured;
            ApplyTowerPoisonToPlayer(out dmg, out cured);
            if (cured)
            {
                logs.Add($"毒のダメージ！ HP-{dmg}");
                logs.Add("毒が自然に治った！");
            }
            else
            {
                logs.Add($"毒のダメージ！ HP-{dmg}");
            }
        }

        // --- 麻痺: 自然治癒のみ（移動遅延は TowerState 側で処理） ---
        if (gs.isParalyzed)
        {
            if (TryNaturalCure())
            {
                gs.isParalyzed = false;
                logs.Add("麻痺が自然に治った！");
            }
        }

        // --- 暗闇: 自然治癒のみ（背景暗転は TowerState 側で処理） ---
        if (gs.isBlind)
        {
            if (TryNaturalCure())
            {
                gs.isBlind = false;
                logs.Add("暗闇が自然に治った！");
            }
        }

        // --- 沈黙: 自然治癒のみ ---
        if (gs.isSilenced)
        {
            if (TryNaturalCure())
            {
                gs.isSilenced = false;
                logs.Add("沈黙が自然に治った！");
            }
        }

        // --- 石化: 10% で残ターン-1（Phase C 追加） ---
        // 自然治癒対象外。TryNaturalCure() は使わない。
        // 専用の確率判定で残ターンを減らす。
        if (gs.isPetrified && gs.playerPetrifyTurns > 0)
        {
            float roll = Random.Range(0f, 100f);
            bool progressed = roll < TowerPetrifyProgressChance;
            Debug.Log($"[StatusEffect] TowerPetrifyProgress: chance={TowerPetrifyProgressChance}% " +
                      $"roll={roll:F2} progressed={progressed}");

            if (progressed)
            {
                gs.playerPetrifyTurns--;
                if (gs.playerPetrifyTurns <= 0)
                {
                    // 残ターン 0 到達 → 呼び出し元で 30 秒ロック発動
                    petrifyReachedZero = true;
                    logs.Add("石化が完成した！");
                    Debug.Log("[StatusEffect] Tower petrify reached zero!");
                }
                else if (gs.playerPetrifyTurns == 1)
                {
                    logs.Add("もうすぐ完全に石化してしまう！");
                }
                else
                {
                    logs.Add("石化が進行した…");
                }
            }
        }

    }

    /// <summary>
    /// 後方互換用: out 引数なしの旧シグネチャ。
    /// petrifyReachedZero を捨てる場合に使う。
    /// ※ TowerState の呼び出し元を新シグネチャに更新した後は不要だが、
    ///   他の呼び出し元があった場合に備えて残す。
    /// </summary>
    public static void ApplyTowerStepEffects(out List<string> logs)
    {
        bool dummy;
        ApplyTowerStepEffects(out logs, out dummy);
    }

    // =========================================================
    // バフ/デバフ: ステータスへの汎用適用計算
    // =========================================================

    /// <summary>
    /// バフ/デバフの効果率をステータス値に適用する汎用メソッド。
    /// 防御・攻撃・魔攻・魔防・運のいずれにも使用できる。
    ///
    /// isBuffed == true の場合: baseStat × (1 + rate/100) に増加
    /// isDebuffed == true の場合: baseStat × (1 - rate/100) に減少
    /// 両方 true になることはない（後優先ルールで排他制御されている）。
    /// 結果は最低0にクランプ。
    /// </summary>
    /// <param name="baseStat">基礎ステータス値</param>
    /// <param name="isBuffed">バフがかかっているか</param>
    /// <param name="buffRate">バフの効果率（%）</param>
    /// <param name="isDebuffed">デバフがかかっているか</param>
    /// <param name="debuffRate">デバフの効果率（%）</param>
    /// <returns>バフ/デバフ適用後のステータス値</returns>
    public static int ApplyStatBuffDebuff(int baseStat,
        bool isBuffed, float buffRate,
        bool isDebuffed, float debuffRate)
    {
        if (isDebuffed && debuffRate > 0f)
        {
            int result = Mathf.FloorToInt(baseStat * (1f - debuffRate / 100f) + 0.5f);
            if (result < 0) result = 0;
            return result;
        }

        if (isBuffed && buffRate > 0f)
        {
            int result = Mathf.FloorToInt(baseStat * (1f + buffRate / 100f) + 0.5f);
            return result;
        }

        return baseStat;
    }

    /// <summary>
    /// 防御力専用のバフ/デバフ適用（後方互換）。
    /// 新規コードは ApplyStatBuffDebuff を使用すること。
    /// </summary>
    public static int ApplyDefenseBuffDebuff(int baseDefense,
        bool isBuffed, float buffRate,
        bool isDebuffed, float debuffRate)
    {
        return ApplyStatBuffDebuff(baseDefense, isBuffed, buffRate, isDebuffed, debuffRate);
    }

    // =========================================================
    // 後方互換用メソッド（既存の呼び出し元を壊さない）
    // =========================================================

    /// <summary>
    /// プレイヤーの毒耐性値を返す（後方互換）。
    /// 新規コードは GetPlayerResistance(StatusEffect.Poison) を使用すること。
    /// </summary>
    public static int GetPlayerPoisonResistance()
    {
        return GetPlayerResistance(StatusEffect.Poison);
    }

    /// <summary>
    /// プレイヤーに毒を付与する試行（後方互換）。
    /// 新規コードは TryInflictPlayer(StatusEffect.Poison, baseChance) を使用すること。
    /// </summary>
    public static bool TryPoisonPlayer(float baseChance)
    {
        return TryInflictPlayer(StatusEffect.Poison, baseChance);
    }

    /// <summary>
    /// プレイヤーの毒を治癒する（後方互換）。
    /// 新規コードは CurePlayer(StatusEffect.Poison) を使用すること。
    /// </summary>
    public static void CurePlayerPoison()
    {
        CurePlayer(StatusEffect.Poison);
    }

    /// <summary>
    /// 敵へのスタン付与判定を行う（後方互換）。
    /// 新規コードは TryInflict(baseChance, GetEnemyResistance(Stun, monster)) を使用すること。
    /// </summary>
    public static bool TryStunEnemy(float baseChance, int targetResistance)
    {
        return TryInflict(baseChance, targetResistance);
    }

    /// <summary>
    /// 敵のスタン耐性を取得する（後方互換）。
    /// 新規コードは GetEnemyResistance(StatusEffect.Stun, monster) を使用すること。
    /// </summary>
    public static int GetEnemyStunResistance(Monster monster)
    {
        return GetEnemyResistance(StatusEffect.Stun, monster);
    }
}