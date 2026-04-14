using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// BattleSceneController のクイズボス処理パート（partial class）。
///
/// 【クイズバトル仕様】
///   - 敵ターンでクイズを出題する。
///   - 攻撃ボタン→選択肢A、防御ボタン→選択肢B に一時変化する。
///   - 魔法/スキル/アイテム/ギブアップは回答中は操作不能のまま。
///   - 正解1回ごとに敵HP1割（MaxHp/10, 切り捨て, 最低1）減少。
///   - 不正解3回でプレイヤー即死。
///   - 10問正解で勝利。
///   - 回答後 → 正解/不正解判定 → AfterEnemyAction → プレイヤーターン（通常操作に復帰）。
///
/// 【使い方】
///   Monster アセットで isQuizBoss = true, quizDatabase を設定する。
///   EnemyTurn() から StartQuizTurn() を呼び出す。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // クイズボス定数
    // =========================================================

    /// <summary>不正解の許容回数。この回数に達するとプレイヤー即死。</summary>
    private const int QuizMaxWrong = 3;

    /// <summary>勝利に必要な正解回数。</summary>
    private const int QuizCorrectToWin = 10;

    // =========================================================
    // クイズボス状態（static: シーンリロード対応）
    // =========================================================

    /// <summary>現在のクイズ正解回数。</summary>
    private static int quizCorrectCount = 0;

    /// <summary>現在のクイズ不正解回数。</summary>
    private static int quizWrongCount = 0;

    /// <summary>今回の戦闘で既に出題済みの問題インデックス。</summary>
    private static List<int> quizUsedIndices = new List<int>();

    /// <summary>クイズ回答待ち中かどうか。</summary>
    private static bool isQuizAnswering = false;

    /// <summary>現在出題中のクイズデータ。</summary>
    private static QuizData currentQuizData = null;

    // =========================================================
    // ボタンテキスト退避用
    // =========================================================

    /// <summary>攻撃ボタンの元テキスト。</summary>
    private string originalAttackLabel = null;

    /// <summary>防御ボタンの元テキスト。</summary>
    private string originalDefendLabel = null;

    // =========================================================
    // クイズボス判定
    // =========================================================

    /// <summary>
    /// 現在の敵がクイズボスかどうか。
    /// </summary>
    private bool IsQuizBoss()
    {
        return enemyMonster != null
            && enemyMonster.isQuizBoss
            && enemyMonster.quizDatabase != null
            && enemyMonster.quizDatabase.quizzes != null
            && enemyMonster.quizDatabase.quizzes.Count > 0;
    }

    // =========================================================
    // クイズ初期化（ResetBattleStatics から呼ぶ）
    // =========================================================

    /// <summary>
    /// クイズ状態をリセットする。ResetBattleStatics() から呼ぶ。
    /// </summary>
    private static void ResetQuizBossStatics()
    {
        quizCorrectCount = 0;
        quizWrongCount = 0;
        quizUsedIndices.Clear();
        isQuizAnswering = false;
        currentQuizData = null;
    }

    // =========================================================
    // クイズ出題（EnemyTurn から呼ばれる）
    // =========================================================

    /// <summary>
    /// クイズボスの敵ターン処理。クイズを出題し、ボタンを差し替える。
    /// </summary>
    private void StartQuizTurn()
    {
        // --- クイズを選択 ---
        QuizData quiz = PickNextQuiz();
        if (quiz == null)
        {
            // 全問出題済みの場合はインデックスをリセットして再出題
            quizUsedIndices.Clear();
            quiz = PickNextQuiz();
        }

        if (quiz == null)
        {
            // それでも取れない場合（データ不備）→ 通常ターンにフォールバック
            Debug.LogWarning("[QuizBoss] クイズデータが取得できません。通常ターンを実行します。");
            AfterEnemyAction();
            return;
        }

        currentQuizData = quiz;
        isQuizAnswering = true;

        // --- ログに出題（改行ごとに1行ずつ表示） ---
        string[] quizLines = quiz.questionText.Split('\n');
        for (int i = 0; i < quizLines.Length; i++)
        {
            string line = quizLines[i].TrimEnd('\r'); // CR除去
            if (i == 0)
                AddLog($"【クイズ】{line}");
            else
                AddLog(line);
        }

        // --- ログ表示後にボタン差し替え ---
        FlushLogsAndThen(() =>
        {
            SwapButtonsToQuiz();
        });
    }

    /// <summary>
    /// まだ出題されていないクイズをランダムに1問選ぶ。
    /// 全問出題済みの場合は null を返す。
    /// </summary>
    private QuizData PickNextQuiz()
    {
        var db = enemyMonster.quizDatabase;
        if (db == null || db.quizzes == null || db.quizzes.Count == 0) return null;

        // 未出題のインデックスを収集
        List<int> available = new List<int>();
        for (int i = 0; i < db.quizzes.Count; i++)
        {
            if (!quizUsedIndices.Contains(i) && db.quizzes[i] != null)
                available.Add(i);
        }

        if (available.Count == 0) return null;

        int pick = available[Random.Range(0, available.Count)];
        quizUsedIndices.Add(pick);
        return db.quizzes[pick];
    }

    // =========================================================
    // ボタン差し替え / 復元
    // =========================================================

    /// <summary>
    /// 攻撃ボタンを「A」、防御ボタンを「B」に差し替える。
    /// 選択肢の内容は問題文内に含まれる前提。
    /// 他のボタンは無効化したまま。
    /// </summary>
    private void SwapButtonsToQuiz()
    {
        // 攻撃ボタン → A
        if (attackButton != null)
        {
            var label = attackButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                originalAttackLabel = label.text;
                label.text = "A";
            }
            attackButton.interactable = true;

            // リスナーを一時的に差し替え
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OnQuizAnswerA);
        }

        // 防御ボタン → B
        if (defendButton != null)
        {
            var label = defendButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                originalDefendLabel = label.text;
                label.text = "B";
            }
            defendButton.interactable = true;

            // リスナーを一時的に差し替え
            defendButton.onClick.RemoveAllListeners();
            defendButton.onClick.AddListener(OnQuizAnswerB);
        }

        // 他のボタンは無効化のまま
        if (skillButton != null) skillButton.interactable = false;
        if (itemButton != null) itemButton.interactable = false;
        if (magicButton != null) magicButton.interactable = false;
        if (giveUpButton != null) giveUpButton.interactable = false;
    }

    /// <summary>
    /// ボタンを通常の状態に復元する。
    /// </summary>
    private void RestoreButtonsFromQuiz()
    {
        // 攻撃ボタンのテキストとリスナーを復元
        if (attackButton != null)
        {
            if (originalAttackLabel != null)
            {
                var label = attackButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = originalAttackLabel;
                originalAttackLabel = null;
            }
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OnAttackClicked);
        }

        // 防御ボタンのテキストとリスナーを復元
        if (defendButton != null)
        {
            if (originalDefendLabel != null)
            {
                var label = defendButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = originalDefendLabel;
                originalDefendLabel = null;
            }
            defendButton.onClick.RemoveAllListeners();
            defendButton.onClick.AddListener(OnDefendClicked);
        }
    }

    // =========================================================
    // 回答処理
    // =========================================================

    /// <summary>選択肢Aが選ばれた。</summary>
    private void OnQuizAnswerA()
    {
        if (!isQuizAnswering || currentQuizData == null) return;
        ProcessQuizAnswer(QuizAnswer.A);
    }

    /// <summary>選択肢Bが選ばれた。</summary>
    private void OnQuizAnswerB()
    {
        if (!isQuizAnswering || currentQuizData == null) return;
        ProcessQuizAnswer(QuizAnswer.B);
    }

    /// <summary>
    /// クイズの回答を判定し、結果に応じてダメージを与える。
    /// </summary>
    private void ProcessQuizAnswer(QuizAnswer answer)
    {
        isQuizAnswering = false;

        // ボタンを即座に無効化（連打防止）
        if (attackButton != null) attackButton.interactable = false;
        if (defendButton != null) defendButton.interactable = false;

        bool isCorrect = (answer == currentQuizData.correctAnswer);

        if (isCorrect)
        {
            quizCorrectCount++;

            // 敵にダメージ: MaxHp / 10（切り捨て、最低1）
            int quizDamage = enemyMonster.MaxHp / 10;
            if (quizDamage < 1) quizDamage = 1;
            enemyCurrentHp -= quizDamage;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;

            AddLog($"正解！ {enemyMonster.Mname} に {quizDamage} ダメージ！");
            AddLog($"（正解: {quizCorrectCount}/{QuizCorrectToWin}）");

            Debug.Log($"[QuizBoss] 正解 {quizCorrectCount}/{QuizCorrectToWin} damage={quizDamage} enemyHp={enemyCurrentHp}");
        }
        else
        {
            quizWrongCount++;

            string correctLabel = currentQuizData.correctAnswer == QuizAnswer.A
                ? currentQuizData.choiceA
                : currentQuizData.choiceB;

            AddLog($"不正解… 正解は「{correctLabel}」！");
            AddLog($"（不正解: {quizWrongCount}/{QuizMaxWrong}）");

            Debug.Log($"[QuizBoss] 不正解 {quizWrongCount}/{QuizMaxWrong}");

            // 不正解3回で即死
            if (quizWrongCount >= QuizMaxWrong)
            {
                AddLog($"{enemyMonster.Mname} の力が解放された！ You は倒れた…");

                if (GameState.I != null)
                {
                    GameState.I.currentHp = 0;
                }
            }
        }

        currentQuizData = null;

        // ボタンを復元してから AfterEnemyAction へ
        RestoreButtonsFromQuiz();

        // ログ表示後に AfterEnemyAction（勝敗判定含む）
        // AfterEnemyAction 内で enemyCurrentHp <= 0 や playerHp <= 0 が処理される
        FlushLogsAndThen(() =>
        {
            AfterEnemyAction();
        });
    }
}