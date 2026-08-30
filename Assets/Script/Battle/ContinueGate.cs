using System;
using UnityEngine;

/// <summary>
/// コンティニュー可否の判断を一元化するゲート。
/// プラットフォーム差（モバイル=広告視聴 / コンソール=残数制）をここに閉じ込め、
/// BattleSceneController はゲートの答えだけを見る。
///
/// 【スクリプティング定義シンボル】
///   CONSOLE_BUILD … Steam/Switch 共通（広告なし・残数制）。
///                    Player Settings > Scripting Define Symbols で
///                    Standalone と Switch のビルドターゲットに設定する。
///                    未定義（モバイル/現行エディタ）では従来の広告挙動のまま。
///   Steam/Switch の個別分岐は Unity 組み込みの UNITY_STANDALONE / UNITY_SWITCH を使う
///   （独自シンボルを増やさない）。
///
/// 【コンソール版の仕様（2026-08-30 確定）】
///   - 道中（通常戦闘）: 1回の冒険（拠点→拠点）につき 3 回まで
///   - ボス戦: 無制限（モバイルの F50/F100 特例は全ボスに一般化）
///   - 残数リセットは MainSceneRecovery（街到着）から。
///     usedStorageAd（第 TowerState 節）と同じ境界・同じ static 生存戦略。
///     アプリ再起動で残数が全快する点も usedStorageAd と同様（許容）。
///
/// ⚠️ static である理由は CLAUDE.md 第1節と同じ。戦闘中のアイテム/装備画面経由で
///    Battle シーンは作り直されるため、インスタンスフィールドでは消える。
/// </summary>
public static class ContinueGate
{
#if CONSOLE_BUILD
    /// <summary>1回の冒険あたりの道中コンティニュー上限。</summary>
    public const int MaxPerAdventure = 3;

    /// <summary>この冒険で消費した道中コンティニュー回数。</summary>
    private static int used = 0;
#endif

    /// <summary>残数0で表示するメッセージ。</summary>
    public const string NoRemainingText = "残り回数がありません。\n帰還します。";

    /// <summary>
    /// いまコンティニューできるか。
    /// モバイル: 常に可（広告視聴が対価）。コンソール: ボスは常に可、道中は残数制。
    /// </summary>
    public static bool CanContinue(bool isBossBattle)
    {
#if CONSOLE_BUILD
        return isBossBattle || used < MaxPerAdventure;
#else
        return true;
#endif
    }

    /// <summary>
    /// コンティニュー確認ポップアップの本文。
    /// 文言のプラットフォーム差もここに集約する（消費側にリテラルを書かない）。
    /// </summary>
    public static string PopupText(bool isBossBattle, bool isFreeBoss)
    {
#if CONSOLE_BUILD
        if (isBossBattle)
            return "戦闘をやり直しますか？\n（全回復、アイテム復活）";
        return $"このSTEPから続けますか？\n（残り{MaxPerAdventure - used}回）";
#else
        // ★F50/F100 は広告不要コンティニュー（モバイルのみの特例・消さないこと）
        if (isFreeBoss)
            return "広告なんて見なくていいから\nかかって来いよ！";
        if (isBossBattle)
            return "広告を視聴して戦闘をやり直しますか？\n（全回復、アイテム復活）";
        return "広告を視聴してこのSTEPから続けますか？";
#endif
    }

    /// <summary>
    /// 「はい」押下後の処理。復活してよくなったら onResult(true) を呼ぶ。
    /// モバイル: （F50/F100 特例を除き）広告視聴後に非同期で返る。
    /// コンソール: 道中なら残数を消費して同期で返る。
    ///
    /// ⚠️ AdManager.ShowRewardedAd のコンティニュー経路はここが唯一の呼び出し点。
    ///    タイムアウト保険（AdTimeoutFallback）は呼び出し側 BattleSceneController が
    ///    従来どおり持つ（onResult の二重発火は adResultHandled で防がれる前提）。
    /// </summary>
    public static void RequestContinue(bool isBossBattle, bool isFreeBoss, Action<bool> onResult)
    {
#if CONSOLE_BUILD
        if (!isBossBattle) used++;
        onResult(true);
#else
        if (isFreeBoss)
        {
            Debug.Log("[ContinueGate] 広告不要コンティニュー対象ボス → 広告スキップで復活");
            onResult(true);
            return;
        }

        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedAd(onResult);
        }
        else
        {
            Debug.LogWarning("[ContinueGate] AdManager.Instance が null — 広告なしで復活");
            onResult(true);
        }
#endif
    }

    /// <summary>
    /// 冒険の開始/終了境界（街到着）で残数をリセットする。
    /// MainSceneRecovery から TowerState.ResetStorageAdFlag() と並べて呼ぶ。
    /// モバイルでは何もしない（呼んで無害）。
    /// </summary>
    public static void ResetForNewAdventure()
    {
#if CONSOLE_BUILD
        used = 0;
#endif
    }
}
