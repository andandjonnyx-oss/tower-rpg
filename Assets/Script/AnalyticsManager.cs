using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Analytics;
using UnityEngine;
using UnityEngine.UnityConsent;

/// <summary>
/// Unity Gaming Services (UGS) Analytics へのイベント送信を一元化する静的クラス。
///
/// 【設計方針】
///   - 観測点（BattleSceneController の勝敗判定など）には Send 系メソッドを 1 行差し込むだけ。
///   - 初期化・同意の有無・例外はすべてこのクラスが吸収する。
///     未初期化／オフライン／SDK未導入でも、呼び出し側は例外を気にせず呼べる
///     （内部で try-catch し、失敗してもゲーム進行を止めない）。
///   - イベント名・パラメータ名は Unity Dashboard の Event Manager で
///     同名・同型で定義しておくこと（未定義だとサーバ側で破棄される）。
///
/// 【イベント定義（Dashboard 側で登録が必要）】
///   game_over:
///     floor (INT), step (INT), level (INT)
///   boss_defeated:
///     boss_floor (INT), level (INT),
///     str (INT), vit (INT), int_stat (INT), dex (INT), luc (INT)
///   ※ "int" は予約語的に紛らわしいため、パラメータ名は int_stat としている。
///
/// 【初期化】
///   タイトル等の起動シーンで一度だけ AnalyticsManager.InitializeAsync() を await する。
///   同意フローは自前 UI のオン/オフに合わせて GrantConsent()/RevokeConsent() を呼ぶ。
/// </summary>
public static class AnalyticsManager
{
    /// <summary>UGS 初期化が完了したかどうか。Send 系は false の間は黙って何もしない。</summary>
    public static bool IsReady { get; private set; } = false;

    /// <summary>
    /// UGS を初期化する。起動シーンの起動処理で一度だけ await して呼ぶ。
    /// 多重呼び出しは無害（IsReady で弾く）。
    /// オフラインでも例外を投げず、IsReady は false のままになる。
    /// </summary>
    public static async System.Threading.Tasks.Task InitializeAsync()
    {
        if (IsReady) return;

        try
        {
            await UnityServices.InitializeAsync();
            IsReady = true;
            Debug.Log("[AnalyticsManager] UGS 初期化完了");
        }
        catch (System.Exception e)
        {
            // オフライン・設定未済などでも、ここで握りつぶしてゲーム進行を優先する。
            Debug.LogWarning($"[AnalyticsManager] UGS 初期化失敗（解析は無効のまま継続）: {e.Message}");
            IsReady = false;
        }
    }

    /// <summary>
    /// ユーザーがデータ収集に同意した時に呼ぶ。
    /// 同意済みなら InitializeAsync 完了時点で自動的に収集が始まるため、
    /// 自前同意 UI を出すなら「オン」操作でこれを呼ぶ。
    /// </summary>
    public static void GrantConsent()
    {
        try
        {
            EndUserConsent.SetConsentState(new ConsentState
            {
                AnalyticsIntent = ConsentStatus.Granted,
            });
            Debug.Log("[AnalyticsManager] データ収集に同意（収集開始）");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AnalyticsManager] 同意設定に失敗: {e.Message}");
        }
    }

    /// <summary>
    /// ユーザーが同意を撤回した時に呼ぶ。以後の収集を停止する。
    /// </summary>
    public static void RevokeConsent()
    {
        try
        {
            EndUserConsent.SetConsentState(new ConsentState
            {
                AnalyticsIntent = ConsentStatus.Denied,
            });
            Debug.Log("[AnalyticsManager] データ収集の同意を撤回（収集停止）");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AnalyticsManager] 同意撤回に失敗: {e.Message}");
        }
    }

    // =========================================================
    // 観測点から呼ぶ Send 系メソッド
    // =========================================================

    /// <summary>
    /// ゲームオーバー（全滅/ギブアップ）地点を記録する。
    /// 呼び出し例（敗北確定箇所）:
    ///   AnalyticsManager.SendGameOver(GameState.I.floor, GameState.I.step, GameState.I.level);
    /// 引数を渡さず GameState.I から自動取得したい場合は SendGameOver() を使う。
    /// </summary>
    public static void SendGameOver(int floor, int step, int level)
    {
        if (!IsReady) return;

        try
        {
            CustomEvent e = new CustomEvent("game_over")
            {
                { "floor", floor },
                { "step",  step  },
                { "level", level },
            };
            AnalyticsService.Instance.RecordEvent(e);
            Debug.Log($"[AnalyticsManager] game_over 記録: floor={floor}, step={step}, level={level}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AnalyticsManager] game_over 送信失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// GameState.I から floor/step/level を自動取得してゲームオーバーを記録する簡易版。
    /// 敗北確定箇所（DetermineVictoryTransition の敗北ルート等）に 1 行差し込む用途。
    /// </summary>
    public static void SendGameOver()
    {
        var gs = GameState.I;
        if (gs == null) return;
        SendGameOver(gs.floor, gs.step, gs.level);
    }

    /// <summary>
    /// ボス撃破時のプレイヤーステータス（STR～LUC）を記録する。
    /// bossFloor には撃破したボスの階（70 / 90 / 100 など）を渡す。
    /// ステータスは「振り分けた生値（baseXxx）」を記録する。
    /// 装備・パッシブ込みの実効値を見たい場合は呼び出し側で
    ///   GameState.I.Attack / MagicAttack / Luck 等を別途渡す設計に変更すること。
    ///
    /// 呼び出し例（ボス勝利確定箇所）:
    ///   AnalyticsManager.SendBossDefeated(70);
    /// </summary>
    public static void SendBossDefeated(int bossFloor, int level, int str, int vit, int intStat, int dex, int luc)
    {
        if (!IsReady) return;

        try
        {
            CustomEvent e = new CustomEvent("boss_defeated")
            {
                { "boss_floor", bossFloor },
                { "level",      level     },
                { "str",        str       },
                { "vit",        vit       },
                { "int_stat",   intStat   },
                { "dex",        dex       },
                { "luc",        luc       },
            };
            AnalyticsService.Instance.RecordEvent(e);
            Debug.Log($"[AnalyticsManager] boss_defeated 記録: F{bossFloor}, Lv{level}, "
                    + $"STR={str}, VIT={vit}, INT={intStat}, DEX={dex}, LUC={luc}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AnalyticsManager] boss_defeated 送信失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// GameState.I から level / baseSTR～baseLUC を自動取得してボス撃破を記録する簡易版。
    /// ボス勝利確定箇所に 1 行差し込む用途。
    ///   AnalyticsManager.SendBossDefeated(70);
    /// </summary>
    public static void SendBossDefeated(int bossFloor)
    {
        var gs = GameState.I;
        if (gs == null) return;
        SendBossDefeated(
            bossFloor,
            gs.level,
            gs.baseSTR,
            gs.baseVIT,
            gs.baseINT,
            gs.baseDEX,
            gs.baseLUC);
    }
}