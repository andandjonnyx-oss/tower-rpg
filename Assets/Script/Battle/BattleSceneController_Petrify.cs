using UnityEngine;

/// <summary>
/// BattleSceneController の石化管理パート（partial class）。
///
/// 【Phase A の責務】
///   - 敵側の石化状態フィールド宣言
///   - 初期化 / リセット
///   - 付与処理（残ターンセット含む）のヘルパー
///
/// 【Phase B1 の追加責務】
///   - 毎ターン終了時のカウントダウン（TickPlayerPetrifyTurns / TickEnemyPetrifyTurns）
///   - 0到達時の状態フラグ提供（EnemyPetrifyJustReachedZero）
///   - プレイヤー石化の強制解除（ClearPlayerPetrify） — Continue復帰用
///
/// 【Phase B2 以降で追加予定】
///   - DEF/MDEF 倍率計算メソッド
///
/// 【設計メモ】
///   石化は bool フラグだけでなく「残ターン数」「最大ターン数」を持つため、
///   他の持続型デバフ（enemyIsPoisoned 等）と同じパターンではなく
///   独立したパートとして管理する。
///
///   プレイヤー側の石化状態は GameState.isPetrified / playerPetrifyTurns /
///   playerPetrifyMaxTurns に保持されており、ここでは敵側のみフィールドを持つ。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // 敵側の石化フィールド
    // =========================================================

    /// <summary>戦闘中の敵の石化状態。戦闘終了でリセット。</summary>
    private static bool enemyIsPetrified = false;

    /// <summary>
    /// 石化の残りターン数。0 で撃破扱い（Phase B1 以降で判定）。
    /// 初期付与時はモンスターなら 10。
    /// </summary>
    private static int enemyPetrifyTurns = 0;

    /// <summary>
    /// 石化の最大ターン数（DEF/MDEF倍率計算用）。
    /// 付与時に固定され、進行しても不変。Phase B2 以降で倍率計算に使用。
    /// </summary>
    private static int enemyPetrifyMaxTurns = 0;

    /// <summary>
    /// 敵の石化がこの AfterEnemyAction ターンで 0 に到達したかのフラグ。
    /// TickEnemyPetrifyTurns で true になり、呼び出し元（AfterEnemyAction）が
    /// OnVictory ルートへ分岐する判定に使う。
    /// ティックのたびに先頭で false リセットされる。
    /// </summary>
    private static bool enemyPetrifyJustReachedZero = false;

    // =========================================================
    // 定数
    // =========================================================

    /// <summary>プレイヤーの石化初期残ターン数。</summary>
    public const int PlayerPetrifyInitialTurns = 5;

    /// <summary>モンスターの石化初期残ターン数。</summary>
    public const int EnemyPetrifyInitialTurns = 10;

    // =========================================================
    // 初期化 / リセット
    // =========================================================

    /// <summary>
    /// 敵側の石化フィールドをリセットする。
    /// 戦闘開始時および戦闘終了時に呼ぶ。
    /// プレイヤー側の石化は戦闘終了後も継続するため、ここではリセットしない。
    /// </summary>
    public static void ResetEnemyPetrifyFields()
    {
        enemyIsPetrified = false;
        enemyPetrifyTurns = 0;
        enemyPetrifyMaxTurns = 0;
        enemyPetrifyJustReachedZero = false;
    }

    // =========================================================
    // 付与ヘルパー（Phase A）
    // =========================================================

    /// <summary>
    /// プレイヤーに石化を付与する。既に石化中の場合は残ターンを1減らす（最低1でクランプ）。
    /// 耐性チェックや発動率判定は呼び出し元で行うこと。
    ///
    /// 戻り値: ログ用のメッセージ。
    ///   新規付与時: "石化した！"
    ///   既存石化の進行時: "石化が進行した！（残り○ターン）"
    ///   既に残1ターンの場合: "石化が進行した！（残り1ターン）" ※残ターン据え置き
    /// </summary>
    public static string InflictPetrifyToPlayer()
    {
        if (GameState.I == null) return "";

        if (GameState.I.isPetrified)
        {
            // 既に石化中: 残ターンを1減らす（ただし最低1でクランプ）
            if (GameState.I.playerPetrifyTurns > 1)
            {
                GameState.I.playerPetrifyTurns--;
            }
            Debug.Log($"[Petrify] Player petrify progressed: turns={GameState.I.playerPetrifyTurns}/{GameState.I.playerPetrifyMaxTurns}");
            return $"石化が進行した！（残り{GameState.I.playerPetrifyTurns}ターン）";
        }
        else
        {
            // 新規付与
            GameState.I.isPetrified = true;
            GameState.I.playerPetrifyTurns = PlayerPetrifyInitialTurns;
            GameState.I.playerPetrifyMaxTurns = PlayerPetrifyInitialTurns;
            Debug.Log($"[Petrify] Player is now petrified: turns={PlayerPetrifyInitialTurns}");
            return "石化した！";
        }
    }

    /// <summary>
    /// 敵に石化を付与する。既に石化中の場合は残ターンを1減らす（最低1でクランプ）。
    /// 耐性チェックや発動率判定は呼び出し元で行うこと。
    ///
    /// 戻り値: ログ用のメッセージ（敵名は呼び出し元で整形）。
    /// </summary>
    public static string InflictPetrifyToEnemy()
    {
        if (enemyIsPetrified)
        {
            // 既に石化中: 残ターンを1減らす（ただし最低1でクランプ）
            if (enemyPetrifyTurns > 1)
            {
                enemyPetrifyTurns--;
            }
            Debug.Log($"[Petrify] Enemy petrify progressed: turns={enemyPetrifyTurns}/{enemyPetrifyMaxTurns}");
            return $"石化が進行した！（残り{enemyPetrifyTurns}ターン）";
        }
        else
        {
            // 新規付与
            enemyIsPetrified = true;
            enemyPetrifyTurns = EnemyPetrifyInitialTurns;
            enemyPetrifyMaxTurns = EnemyPetrifyInitialTurns;
            Debug.Log($"[Petrify] Enemy is now petrified: turns={EnemyPetrifyInitialTurns}");
            return "石化した！";
        }
    }

    // =========================================================
    // ターンカウントダウン（Phase B1）
    // =========================================================

    /// <summary>
    /// プレイヤーの石化残ターンを1減らし、ログ用メッセージを返す。
    /// 戦闘中の毎ターン終了時（AfterEnemyAction）から呼ばれる。
    ///
    /// 挙動:
    ///   - 石化中でなければ何もせず空文字を返す
    ///   - 残ターン > 1: 1減らす → "石化の進行… あと○ターン"
    ///   - 残ターン = 1 → 0 到達: 0にして "石化が完成してしまった！"
    ///     敗北判定は呼び出し元で `GameState.I.isPetrified && playerPetrifyTurns <= 0` を見る
    ///   - 残ターン <= 0（既に完成済み）: 空文字を返す（二重処理防止）
    /// </summary>
    public static string TickPlayerPetrifyTurns()
    {
        if (GameState.I == null) return "";
        if (!GameState.I.isPetrified) return "";
        if (GameState.I.playerPetrifyTurns <= 0) return ""; // 既に完成済み

        GameState.I.playerPetrifyTurns--;

        if (GameState.I.playerPetrifyTurns <= 0)
        {
            Debug.Log("[Petrify] Player petrify reached zero (defeat condition met)");
            return "石化が完成してしまった！";
        }

        Debug.Log($"[Petrify] Player petrify tick: turns={GameState.I.playerPetrifyTurns}/{GameState.I.playerPetrifyMaxTurns}");
        return $"石化の進行… あと{GameState.I.playerPetrifyTurns}ターン";
    }

    /// <summary>
    /// 敵の石化残ターンを1減らし、ログ用メッセージを返す。
    /// 戦闘中の毎ターン終了時（AfterEnemyAction）から呼ばれる。
    ///
    /// 挙動:
    ///   - 石化中でなければ何もせず空文字を返す
    ///   - 残ターン > 1: 1減らす → "{敵名} の石化の進行… あと○ターン"
    ///   - 残ターン = 1 → 0 到達: 0にして "{敵名} は石像と化した！"
    ///     同時に EnemyPetrifyJustReachedZero フラグを立てる（呼び出し元が勝利処理へ分岐）
    ///   - 残ターン <= 0（既に完成済み）: 空文字を返す（二重処理防止）
    ///
    /// この呼び出しの先頭で enemyPetrifyJustReachedZero は常に false リセットされる。
    /// </summary>
    public static string TickEnemyPetrifyTurns(string enemyName)
    {
        enemyPetrifyJustReachedZero = false; // 毎ティック先頭で必ずリセット

        if (!enemyIsPetrified) return "";
        if (enemyPetrifyTurns <= 0) return ""; // 既に完成済み

        enemyPetrifyTurns--;

        if (enemyPetrifyTurns <= 0)
        {
            enemyPetrifyJustReachedZero = true;
            Debug.Log("[Petrify] Enemy petrify reached zero (victory condition met)");
            return $"{enemyName} は石像と化した！";
        }

        Debug.Log($"[Petrify] Enemy petrify tick: turns={enemyPetrifyTurns}/{enemyPetrifyMaxTurns}");
        return $"{enemyName} の石化の進行… あと{enemyPetrifyTurns}ターン";
    }

    // =========================================================
    // プレイヤー石化の強制解除（Continue復帰用）
    // =========================================================

    /// <summary>
    /// プレイヤーの石化を強制解除する。
    /// Continue（広告視聴→復活）で FullRecover と組み合わせて呼ばれる想定。
    /// Phase B1 では関数を用意するのみで、呼び出しは Phase D で追加予定。
    ///
    /// GameState.ClearAllStatusEffects() でも同様の解除が行われるため、
    /// FullRecover 経由の復活ルートでは既にカバーされている。
    /// このメソッドは「HPは減らさず石化だけ解除したい」特殊ケース用。
    /// </summary>
    public static void ClearPlayerPetrify()
    {
        if (GameState.I == null) return;
        GameState.I.isPetrified = false;
        GameState.I.playerPetrifyTurns = 0;
        GameState.I.playerPetrifyMaxTurns = 0;
        Debug.Log("[Petrify] Player petrify cleared");
    }

    // =========================================================
    // DEF/MDEF 倍率計算（Phase B2）
    // =========================================================

    /// <summary>
    /// 石化によるDEF/MDEF倍率のステップ値（プレイヤー側）。
    /// 残ターンが減るほど倍率が上がる。残1ターン時に×2.0。
    /// 計算式: 1.0 + (maxTurns - remainingTurns + 1) × step
    /// </summary>
    private const float PlayerPetrifyStep = 0.2f;

    /// <summary>
    /// 石化によるDEF/MDEF倍率のステップ値（敵側）。
    /// 残1ターン時に×2.0。
    /// </summary>
    private const float EnemyPetrifyStep = 0.1f;

    /// <summary>
    /// プレイヤーの石化DEF/MDEF倍率を返す。
    /// 石化中でなければ 1.0f（変化なし）。
    ///
    /// 例（maxTurns=5, step=0.2）:
    ///   残5 → 1.0 + (5-5+1)×0.2 = 1.2
    ///   残3 → 1.0 + (5-3+1)×0.2 = 1.6
    ///   残1 → 1.0 + (5-1+1)×0.2 = 2.0
    /// </summary>
    public static float GetPlayerPetrifyDefMultiplier()
    {
        if (GameState.I == null) return 1f;
        if (!GameState.I.isPetrified) return 1f;
        if (GameState.I.playerPetrifyTurns <= 0) return 1f; // 完成済み（敗北判定中）

        float maxTurns = GameState.I.playerPetrifyMaxTurns;
        float remaining = GameState.I.playerPetrifyTurns;
        float mult = 1f + (maxTurns - remaining + 1f) * PlayerPetrifyStep;
        return mult;
    }

    /// <summary>
    /// 敵の石化DEF/MDEF倍率を返す。
    /// 石化中でなければ 1.0f（変化なし）。
    ///
    /// 例（maxTurns=10, step=0.1）:
    ///   残10 → 1.0 + (10-10+1)×0.1 = 1.1
    ///   残5  → 1.0 + (10-5+1)×0.1  = 1.6
    ///   残1  → 1.0 + (10-1+1)×0.1  = 2.0
    /// </summary>
    public static float GetEnemyPetrifyDefMultiplier()
    {
        if (!enemyIsPetrified) return 1f;
        if (enemyPetrifyTurns <= 0) return 1f; // 完成済み（撃破判定中）

        float maxTurns = enemyPetrifyMaxTurns;
        float remaining = enemyPetrifyTurns;
        float mult = 1f + (maxTurns - remaining + 1f) * EnemyPetrifyStep;
        return mult;
    }

    // =========================================================
    // 参照ヘルパー
    // =========================================================

    /// <summary>敵が石化中かどうか。外部から読み取るためのプロパティ。</summary>
    public static bool EnemyIsPetrified => enemyIsPetrified;

    /// <summary>敵の石化残りターン数。</summary>
    public static int EnemyPetrifyTurnsRemaining => enemyPetrifyTurns;

    /// <summary>敵の石化最大ターン数（倍率計算用）。</summary>
    public static int EnemyPetrifyMaxTurnsValue => enemyPetrifyMaxTurns;

    /// <summary>
    /// 直近の TickEnemyPetrifyTurns で敵の石化が 0 到達したか。
    /// AfterEnemyAction がこのフラグを見て勝利処理へ分岐する。
    /// </summary>
    public static bool EnemyPetrifyJustReachedZero => enemyPetrifyJustReachedZero;

    /// <summary>
    /// プレイヤーの石化が完成（残ターン0）しているか。
    /// AfterEnemyAction が敗北判定に使う。
    /// </summary>
    public static bool PlayerPetrifyReachedZero
    {
        get
        {
            if (GameState.I == null) return false;
            return GameState.I.isPetrified && GameState.I.playerPetrifyTurns <= 0;
        }
    }
}