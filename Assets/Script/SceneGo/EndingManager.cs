using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// エンディング進行の中央管理（static クラス）。
///
/// フロー:
///   100階ボス撃破 → ED会話(EndingTalkEventId) → スタッフロール(StaffRollSceneName)
///   → エピローグ(EpilogueEventId, 終了時に図鑑全開放) → タイトルへ
///
/// 中断復帰:
///   GameState.endingPhase をセーブしておき、タイトルの「スタート」から
///   TryResumeEnding() で中断したフェーズの先頭に復帰する。
/// </summary>
public static class EndingManager
{
    // =========================================================
    // イベントID / シーン名（プロジェクトに合わせて変更可）
    // =========================================================

    /// <summary>100階ボス勝利後に再生するED会話イベントのID。
    /// BattleSceneController が命名規則 "BOSS_F{階:D2}_VICTORY" で
    /// pendingEventId をセットするため、ED会話イベントの id もこれに合わせる。</summary>
    public const string EndingTalkEventId = "BOSS_F100_VICTORY";

    /// <summary>スタッフロール後に再生するエピローグ会話イベントのID。</summary>
    public const string EpilogueEventId = "ED_EPILOGUE";

    public const string StaffRollSceneName = "StaffRoll";
    public const string TalkSceneName = "Talk";
    public const string TitleSceneName = "Title";

    // =========================================================
    // endingPhase の値
    // =========================================================
    public const int PhaseNone = 0;       // 未開始
    public const int PhaseEndingTalk = 1; // ED会話中
    public const int PhaseStaffRoll = 2;  // スタッフロール中
    public const int PhaseEpilogue = 3;   // エピローグ中
    public const int PhaseCleared = 4;    // クリア済み

    /// <summary>ゲームクリア済みかどうか。</summary>
    public static bool IsCleared
        => GameState.I != null && GameState.I.endingPhase >= PhaseCleared;

    /// <summary>
    /// 100階ボス（最終形態）を撃破済みかどうか。
    /// bossPhaseF100 と既読フラグの両方を見る保険付き。
    /// </summary>
    private static bool IsFinalBossDefeated(GameState gs)
    {
        return gs.bossPhaseF100 >= 2 || gs.IsPlayed("BOSS_F100");
    }

    // =========================================================
    // タイトルからの復帰
    // =========================================================

    /// <summary>
    /// エンディング中断からの再開チェック。
    /// TitleUIManager.OnStart() の SaveManager.Load() 直後に呼ぶ。
    /// 戻り値: true = エンディング該当シーンへ遷移した（呼び出し側は以降の処理を中断する）
    /// </summary>
    public static bool TryResumeEnding()
    {
        var gs = GameState.I;
        if (gs == null) return false;

        int phase = gs.endingPhase;

        // 保険: 100階ボス撃破済みなのにフェーズ未記録なら ED 会話から開始する。
        // （ボス勝利直後〜ED会話開始前のごく短い間に中断した場合のフォロー）
        if (phase == PhaseNone && IsFinalBossDefeated(gs))
            phase = PhaseEndingTalk;

        switch (phase)
        {
            case PhaseEndingTalk:
                gs.endingPhase = PhaseEndingTalk;
                SaveManager.Save();
                gs.pendingEventId = EndingTalkEventId;
                gs.talkReturnScene = null;
                SceneManager.LoadScene(TalkSceneName);
                return true;

            case PhaseStaffRoll:
                SceneManager.LoadScene(StaffRollSceneName);
                return true;

            case PhaseEpilogue:
                gs.pendingEventId = EpilogueEventId;
                gs.talkReturnScene = null;
                SceneManager.LoadScene(TalkSceneName);
                return true;

            default:
                return false; // 未開始 or クリア済み → 通常開始
        }
    }

    // =========================================================
    // 会話イベント終了フック
    // =========================================================

    /// <summary>
    /// TalkRunner.Finish() から呼ばれる。
    /// 終了したイベントがED会話/エピローグなら次のフェーズへ遷移する。
    /// 戻り値: true = 遷移した（TalkRunner は ReturnToPreviousScene を呼ばない）
    /// </summary>
    public static bool HandleTalkFinished(string eventId)
    {
        var gs = GameState.I;
        if (gs == null) return false;

        if (eventId == EndingTalkEventId || eventId == EndingTalkEventId + "_EVENT")
        {
            gs.talkReturnScene = null; // ボス戦由来の戻り先をクリア
            gs.endingPhase = PhaseStaffRoll;
            SaveManager.Save();        // ここで中断してもスタッフロール先頭から再開できる
            SceneManager.LoadScene(StaffRollSceneName);
            return true;
        }

        if (eventId == EpilogueEventId)
        {
            gs.talkReturnScene = null;
            gs.endingPhase = PhaseCleared;
            gs.zukanAllUnlocked = true; // ★図鑑全開放
            SaveManager.Save();
            Debug.Log("[Ending] ゲームクリア！ 図鑑を全開放しました");
            SceneManager.LoadScene(TitleSceneName);
            return true;
        }

        return false;
    }

    // =========================================================
    // スタッフロール終了
    // =========================================================

    /// <summary>
    /// StaffRollController から呼ばれる（エンディングモード時のみ）。
    /// フェーズを進めてエピローグ会話へ遷移する。
    /// </summary>
    public static void HandleStaffRollFinished()
    {
        var gs = GameState.I;
        if (gs == null)
        {
            SceneManager.LoadScene(TitleSceneName);
            return;
        }

        gs.endingPhase = PhaseEpilogue;
        SaveManager.Save();            // ここで中断してもエピローグ先頭から再開できる
        gs.pendingEventId = EpilogueEventId;
        gs.talkReturnScene = null;
        SceneManager.LoadScene(TalkSceneName);
    }
}