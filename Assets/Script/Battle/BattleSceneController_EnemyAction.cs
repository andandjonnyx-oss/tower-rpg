using UnityEngine;

/// <summary>
/// BattleSceneController の敵行動パート（partial class）。
/// 敵ターン処理、行動選択（LUC判定）、各種攻撃実行、ターン終了処理を担当する。
///
/// 【ターンスナップショット方式】
///   行動テーブル・行動回数は BeginPlayerTurn() の PreRollEnemyAction() 時点で
///   スナップショット（turnLowHpMode / turnActionCount）として固定される。
///   プレイヤーの攻撃で敵HPが変動しても、そのターン中は行動パターンが変わらない。
///   次のターンの PreRollEnemyAction() で再評価される。
///
/// 【複数回行動】
///   Monster.actionCount（デフォルト1）で1ターンの行動回数を指定。
///   低HP時は Monster.lowHpActionCount（0=通常値使用）で上書き可能。
///   - スタン/麻痺は全行動キャンセル
///   - 強制行動(enemyForcedNextSkill)は1ターン全消費（2回目なし）
///   - 1回目で敵orプレイヤー死亡時は残りスキップ
///   - AfterEnemyAction は全行動完了後に1回だけ実行
///   各行動間は FlushLogsAndThen で待機する。
///
/// 【低HP行動テーブル切り替え】
///   Monster.lowHpThreshold > 0 かつ lowHpActions が空でない場合、
///   現在HP / MaxHP <= threshold で行動テーブルが切り替わる。
///   lowHpBaseActionRange（0=通常値）で actionRange も切り替え可能。
///
/// 【先制攻撃システム】
///   BeginPlayerTurn() で敵の行動が事前抽選される。
///   EnemyTurn() では、pendingEnemyAction が残っている場合はそれを使用する。
///   先制技（Preemptive）の場合は PlayerAction 側で既に実行済みなので、
///   EnemyTurn では通常のターン終了処理のみ行う。
///
/// 【NormalAttack 廃止】
///   旧 NormalAttack は SkillAttack に統一。
///   全ての敵攻撃は ExecuteEnemySkillAttack() で処理する。
///   既存アセットの actionType=1（旧NormalAttack）も SkillAttack と同一扱い。
///
/// 【気絶（スタン）】
///   EnemyTurn() で先制チェックの直後にスタンチェックを行う。
///   スタン中は行動スキップ → ターン終了処理（毒ダメージ等）のみ実行。
///   スタンは1ターン限定で自動解除。
///
/// 【麻痺（Phase2追加）】
///   スタンチェック後に麻痺チェック。20%で行動キャンセル。
///
/// 【暗闘（Phase2追加）】
///   敵が暗闇の場合、baseHitRate を半分にする。
///
/// 【怒り（Phase2追加）】
///   敵が怒り中は actionRange = 1（最初のアクション＝通常攻撃のみ）。
///   攻撃力に RageAttackMultiplier（1.5倍）を乗算。
///   AfterEnemyAction で怒りターンカウントダウン。
///
/// 【食い荒らし（FoodRaid）】
///   プレイヤーの消費アイテム(Consumable)からランダムに1つを食べる。
///   攻撃アイテム(battleDamage > 0)なら「まずい!」敵に最大HP半分のダメージ(即死可)。
///   それ以外なら「うまい!」敵は最大HP半分を回復(MaxHpでクランプ)。
///   所持消費アイテム0の場合は不発メッセージのみ。
///   命中判定・防御・耐性・バフは一切適用しない。
///
/// 【多段攻撃の命中判定ルール】
///   hitCount > 1 の多段攻撃スキルでは、スキル発動自体は100%確定。
///   各ヒットごとに個別に命中/回避判定を行う。
///   （単発スキルでは従来通りスキル発動前に命中判定を行う）
///
/// 【石化（Phase B1追加）】
///   AfterEnemyAction のターン終了処理で TickPlayerPetrifyTurns /
///   TickEnemyPetrifyTurns を呼ぶ。残0到達で以下を発動:
///     - 敵石化完成: enemyCurrentHp = 0 にして OnVictory ルート
///     - プレイヤー石化完成: OnDefeat ルート（Continue可）
///     - 両方同時: 相打ち → 敗北優先（既存の HP0 相打ちルール踏襲）
///   敵を既に倒している場合（enemyCurrentHp <= 0）はティックそのものが
///   呼ばれないため、「敵を倒した場合はカウントが減らない」仕様が自動達成される。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // 防御コマンド定数
    // =========================================================
    //
    // 防御中の防御力倍率とダイス成功率。
    //   DefendDefenseMultiplier: 防御力を何倍にするか（2倍）
    //   DefendDiceRange: 防御ダイスの乱数上限（1.5f → 成功率67%）
    //   通常時の diceRange は DefaultDefenseDiceRange = 2.0f（成功率50%）
    // =========================================================

    /// <summary>防御中の防御力倍率。</summary>
    private const int DefendDefenseMultiplier = 2;

    /// <summary>防御中の防御ダイス乱数上限(成功率67%)。</summary>
    private const float DefendDiceRange = 1.5f;

    // =========================================================
    // LUC判定の閾値切り替え定数
    // =========================================================

    /// <summary>
    /// この値以下の敵LUCでは ±10 の固定差判定を使用する。
    /// これを超えると 1.5倍/0.5倍 の比率判定に切り替わる。
    /// 低LUC域では比率だと閾値が極端に小さくなるための救済措置。
    /// </summary>
    private const int LucFixedThresholdLimit = 20;

    // =========================================================
    // ターンスナップショット
    // =========================================================
    //
    // 行動テーブル・行動回数はターン開始時（PreRollEnemyAction）に
    // 1回だけ評価し、そのターン中は固定する。
    // これにより、プレイヤーの攻撃で敵HPが変動しても
    // 行動パターンの切り替えは「次のターンから」になる。
    // =========================================================

    /// <summary>このターンで低HPモードを使用するか。PreRollEnemyAction で設定。</summary>
    private static bool turnLowHpMode = false;

    /// <summary>このターンの行動回数。PreRollEnemyAction で設定。</summary>
    private static int turnActionCount = 1;

    // =========================================================
    // 低HP判定・スナップショット ヘルパー
    // =========================================================

    /// <summary>
    /// 現在のHP割合で低HPモード条件を満たしているかをリアルタイム判定する。
    /// SnapshotTurnActionMode でスナップショットを作るときに使う。
    /// </summary>
    private bool IsLowHpModeNow()
    {
        if (enemyMonster.lowHpThreshold <= 0f) return false;
        if (enemyMonster.lowHpActions == null || enemyMonster.lowHpActions.Length == 0) return false;
        if (enemyMonster.MaxHp <= 0) return false;
        float hpRatio = (float)enemyCurrentHp / enemyMonster.MaxHp;
        return hpRatio <= enemyMonster.lowHpThreshold;
    }

    /// <summary>
    /// ターンスナップショットを設定する。
    /// PreRollEnemyAction() から呼ばれ、そのターン中の行動テーブルと行動回数を固定する。
    /// </summary>
    private void SnapshotTurnActionMode()
    {
        turnLowHpMode = IsLowHpModeNow();

        int count;
        if (turnLowHpMode && enemyMonster.lowHpActionCount > 0)
            count = enemyMonster.lowHpActionCount;
        else
            count = enemyMonster.actionCount;
        turnActionCount = (count < 1) ? 1 : count;

        if (turnLowHpMode)
        {
            float hpRatio = (float)enemyCurrentHp / enemyMonster.MaxHp;
            Debug.Log($"[Battle] TurnSnapshot: LowHP mode ON (hpRatio={hpRatio:F2} <= {enemyMonster.lowHpThreshold}), actionCount={turnActionCount}");
        }
        else
        {
            Debug.Log($"[Battle] TurnSnapshot: Normal mode, actionCount={turnActionCount}");
        }
    }

    /// <summary>
    /// ターンスナップショットに基づいた行動テーブルを返す。
    /// </summary>
    private EnemyActionEntry[] GetTurnActionTable()
    {
        if (turnLowHpMode) return enemyMonster.lowHpActions;
        return enemyMonster.actions;
    }

    /// <summary>
    /// ターンスナップショットに基づいた baseActionRange を返す。
    /// </summary>
    private int GetTurnBaseActionRange()
    {
        if (turnLowHpMode && enemyMonster.lowHpBaseActionRange > 0)
            return enemyMonster.lowHpBaseActionRange;
        return enemyMonster.baseActionRange;
    }

    // =========================================================
    // 戦闘終了判定ヘルパー
    // =========================================================

    /// <summary>
    /// 戦闘が終了済み、または敵/プレイヤーが倒れているかを判定する。
    /// 複数回行動ループの中断判定に使用。
    /// </summary>
    private bool IsBattleEndedOrDead()
    {
        if (battleEnded) return true;
        if (enemyCurrentHp <= 0) return true;
        if (GameState.I != null && GameState.I.currentHp <= 0) return true;
        return false;
    }

    // =========================================================
    // 敵ターン
    // =========================================================

    /// <summary>
    /// 敵の行動処理。
    ///
    /// 【先制攻撃システム対応】
    ///   pendingEnemyAction が残っている場合:
    ///     - 先制技だった場合: PlayerAction 側で既に実行済みなので、ここでは
    ///       ターン終了処理（毒ダメージ等）のみ行う。
    ///     - 通常技だった場合: 事前抽選済みの行動をそのまま実行する。
    ///   pendingEnemyAction が null の場合:
    ///     - actions 配列があれば新規に抽選して実行する。
    ///     - なければ Legacy 通常攻撃。
    ///
    /// 【気絶（スタン）チェック】
    ///   先制チェック後にスタンチェックを行い、スタン中は行動スキップ。
    ///
    /// 【麻痺チェック（Phase2追加）】
    ///   スタンチェック後に麻痺チェック。20%で行動キャンセル。
    ///
    /// 【複数回行動】
    ///   turnActionCount（PreRollEnemyAction でスナップショット済み）で
    ///   行動回数を決定し、ループで実行。
    ///   AfterEnemyAction は全行動完了後に1回だけ呼ぶ。
    ///   各行動間は FlushLogsAndThen で待機する。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;

        // =========================================================
        // 先制済みチェック（最優先）
        // =========================================================
        if (isEnemyPreemptive)
        {
            isEnemyPreemptive = false; // フラグをリセット
            pendingEnemyAction = null; // 念のためクリア
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 気絶（スタン）チェック — 全行動キャンセル
        // =========================================================
        if (enemyIsStunned)
        {
            enemyIsStunned = false; // 1ターンで解除
            enemyForcedNextSkill = null; // 予定行動をキャンセル
            AddLog($"{enemyMonster.Mname} は気絶して動けない！");
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 麻痺チェック（Phase2追加） — 全行動キャンセル
        // =========================================================
        if (enemyIsParalyzed)
        {
            if (StatusEffectSystem.CheckParalyzeCancel())
            {
                enemyForcedNextSkill = null; // 予定行動をキャンセル
                AddLog($"{enemyMonster.Mname} は麻痺して動けない！");
                AfterEnemyAction();
                return;
            }
        }

        // =========================================================
        // クイズボス分岐
        // クイズボスの場合は通常行動をスキップしてクイズを出題する。
        // =========================================================
        if (IsQuizBoss())
        {
            StartQuizTurn();
            return;
        }




        // =========================================================
        // 次ターン強制行動チェック（力をためる等）
        // 強制行動は1ターン全消費（複数回行動しない）
        // =========================================================
        if (enemyForcedNextSkill != null)
        {
            SkillData forced = enemyForcedNextSkill;
            enemyForcedNextSkill = forced.enemyNextForceSkill; // 次の予約をセット（nullなら解除）
            ExecuteEnemySkillAttack(forced);
            return;
        }


        // =========================================================
        // 怒り中の敵は通常攻撃のみ（Phase2追加）
        // ターンスナップショットのテーブルから最初のアクションを強制選択。
        // 複数回行動しない。
        // =========================================================
        if (enemyRageTurn > 0)
        {
            EnemyActionEntry[] rageTable = GetTurnActionTable();
            if (rageTable != null && rageTable.Length > 0)
            {
                // 最初のアクションを強制使用（通常攻撃想定）
                ExecuteEnemyAction(rageTable[0]);
                return;
            }
        }

        // =========================================================
        // 事前抽選済みの行動がある場合（通常技）
        // =========================================================
        if (pendingEnemyAction != null)
        {
            EnemyActionEntry pending = pendingEnemyAction;
            pendingEnemyAction = null; // 消費

            if (turnActionCount <= 1)
            {
                // 1回行動: 従来通り
                ExecuteEnemyAction(pending);
                return;
            }

            // 複数回行動: 1回目は pending、2回目以降は通常抽選
            ExecuteEnemySingleAction(pending);
            if (IsBattleEndedOrDead()) { AfterEnemyAction(); return; }
            ExecuteRemainingActions(turnActionCount, 1);
            return;
        }

        // =========================================================
        // 通常の行動抽選（actions 未設定の場合は Legacy 攻撃）
        // =========================================================
        EnemyActionEntry[] currentTable = GetTurnActionTable();
        if (currentTable == null || currentTable.Length == 0)
        {
            ExecuteLegacyAttack();
            return;
        }

        if (turnActionCount <= 1)
        {
            // 1回行動: 従来通り
            EnemyActionEntry selectedAction = SelectEnemyAction();
            ExecuteEnemyAction(selectedAction);
            return;
        }

        // =========================================================
        // 複数回行動ループ
        // =========================================================
        Debug.Log($"[Battle] Multi-action: {turnActionCount} actions this turn");
        EnemyActionEntry firstAction = SelectEnemyAction();
        ExecuteEnemySingleAction(firstAction);
        if (IsBattleEndedOrDead()) { AfterEnemyAction(); return; }
        ExecuteRemainingActions(turnActionCount, 1);
    }

    // =========================================================
    // 複数回行動用メソッド
    // =========================================================

    /// <summary>
    /// 単一の敵行動を実行する（AfterEnemyAction を呼ばない版）。
    /// 複数回行動のとき、各個別行動の実行に使用。
    /// </summary>
    private void ExecuteEnemySingleAction(EnemyActionEntry action)
    {
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] EnemyActionEntry.skill が null です。通常攻撃で代替します。");
            ExecuteLegacyAttackCore();
            return;
        }

        // 沈黙判定: 敵が沈黙中で魔法系スキルなら70%で失敗
        if (enemyIsSilenced && action.skill.skillSource == SkillSource.Magic)
        {
            if (StatusEffectSystem.CheckSilenceFail())
            {
                string silenceName = !string.IsNullOrEmpty(action.skill.skillName)
                    ? action.skill.skillName : "魔法";
                AddLog($"{enemyMonster.Mname} は{silenceName}を唱えようとした…しかし沈黙で失敗した！");
                return;
            }
        }

        switch (action.skill.actionType)
        {
#pragma warning disable 0618 // Obsolete 警告を抑制（既存アセット互換のため）
            case MonsterActionType.NormalAttack: ExecuteEnemySkillAttackCore(action.skill); break;
#pragma warning restore 0618
            case MonsterActionType.SkillAttack: ExecuteEnemySkillAttackCore(action.skill); break;
            case MonsterActionType.Preemptive: ExecuteEnemySkillAttackCore(action.skill); break;
            case MonsterActionType.FoodRaid: ExecuteEnemyFoodRaidCore(action.skill); break;
            case MonsterActionType.Idle: ExecuteEnemyIdleCore(action.skill); break;
            default: ExecuteEnemyIdleCore(action.skill); break;
        }

        if (action.skill.enemyNextForceSkill != null)
        {
            enemyForcedNextSkill = action.skill.enemyNextForceSkill;
        }
    }

    /// <summary>
    /// 残りの行動を FlushLogsAndThen 経由で順次実行する。
    /// completedCount は既に実行済みの行動数。
    /// 全行動完了後に AfterEnemyAction を呼ぶ。
    /// </summary>
    private void ExecuteRemainingActions(int totalActions, int completedCount)
    {
        if (completedCount >= totalActions)
        {
            // 全行動完了 → ターン終了処理
            AfterEnemyAction();
            return;
        }

        // ログを表示してから次の行動を実行
        int nextIndex = completedCount; // キャプチャ用
        FlushLogsAndThen(() =>
        {
            // 戦闘終了チェック
            if (IsBattleEndedOrDead())
            {
                AfterEnemyAction();
                return;
            }

            // 強制行動が設定された場合（直前の行動で enemyNextForceSkill が入った）
            // → 強制行動は1ターン全消費なので、残り行動をスキップして終了
            if (enemyForcedNextSkill != null)
            {
                Debug.Log($"[Battle] Multi-action: forced skill set during action {nextIndex}, ending turn");
                AfterEnemyAction();
                return;
            }

            EnemyActionEntry nextAction = SelectEnemyAction();
            ExecuteEnemySingleAction(nextAction);

            if (IsBattleEndedOrDead())
            {
                AfterEnemyAction();
                return;
            }

            // 再帰的に次の行動へ
            ExecuteRemainingActions(totalActions, nextIndex + 1);
        });
    }

    // =========================================================
    // Legacy 攻撃
    // =========================================================

    /// <summary>
    /// 従来の敵攻撃処理（actions 未設定時のフォールバック）。
    /// Monster.Attack をそのままダメージとして使用する。
    /// 防御ダイスによる軽減を適用する。
    /// プレイヤーが防御中の場合、防御力2倍・ダイス優遇を適用する。
    ///
    /// ※ actions 配列が未設定のモンスター用の安全ネット。
    ///   新規モンスターは必ず actions を設定すること。
    ///
    /// 命中判定:
    ///   Monster.BaseHitRate × (1 - プレイヤー回避率/100)、最低10%。
    ///   敵が暗闇の場合、baseHitRate を半分にする。
    ///
    /// AfterEnemyAction 付き。
    /// </summary>
    private void ExecuteLegacyAttack()
    {
        ExecuteLegacyAttackCore();
        AfterEnemyAction();
    }

    /// <summary>
    /// Legacy攻撃のコア処理。AfterEnemyAction は呼ばない。
    /// </summary>
    private void ExecuteLegacyAttackCore()
    {
        // Phase2: 暗闇補正
        int hitRate = enemyMonster.BaseHitRate;
        if (enemyIsBlind) hitRate = hitRate / 2;

        if (!CheckEnemyHit(hitRate))
        {
            AddLog($"{enemyMonster.Mname} の攻撃！ …しかし外れた！");
            return;
        }

        // Phase2: 怒り中は攻撃力1.5倍
        int enemyDamage = ApplyEnemyAttackBuffDebuff(enemyMonster.Attack);
        if (enemyRageTurn > 0)
        {
            enemyDamage = Mathf.FloorToInt(enemyDamage * StatusEffectSystem.RageAttackMultiplier + 0.5f);
        }
        if (enemyDamage < 1) enemyDamage = 1;

        int defense = GetPlayerDefense(DamageCategory.Physical);
        int blocked;
        if (isDefending)
        {
            defense *= DefendDefenseMultiplier;
            blocked = RollDefenseDice(defense, DefendDiceRange);
        }
        else
        {
            blocked = RollDefenseDice(defense);
        }
        int finalDamage = enemyDamage - blocked;
        if (finalDamage < 0) finalDamage = 0;

        ApplyDamageToPlayer(finalDamage);

        if (blocked > 0)
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！（{blocked}軽減）");
        else
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！");
    }

    // =========================================================
    // 行動抽選
    // =========================================================

    /// <summary>
    /// LUC 差に応じた乱数の上限値（actionRange）を計算し、
    /// 行動テーブルから行動を選択する。
    ///
    /// ターンスナップショット（turnLowHpMode）に基づいて
    /// 行動テーブルと actionRange 基準値を決定する。
    ///
    /// 比較対象:
    ///   プレイヤー側 = GameState.I.baseLUC（生のステータス値）+ LUCバフ/デバフ適用
    ///   敵側         = Monster.Luck + LUCバフ/デバフ適用
    ///
    /// actionRange の決定ルール（baseActionRange=100 の場合）:
    ///
    /// 【判定方式の切り替え】
    ///   敵LUC ≤ 20 の場合（低LUC域）:
    ///     ±10 の固定差で判定する。比率だと閾値が極端に小さくなるため。
    ///     有利閾値 = enemyLuc + 10
    ///     不利閾値 = max(enemyLuc - 10, 1)
    ///
    ///   敵LUC > 20 の場合（通常域）:
    ///     1.5倍/0.5倍 の比率で判定する。
    ///     有利閾値 = enemyLuc × 1.5（四捨五入）
    ///     不利閾値 = enemyLuc × 0.5（四捨五入）
    ///
    ///   プレイヤー有利: playerLuc >= 有利閾値 → actionRange = 80（敵が弱体化）
    ///   敵有利:         playerLuc <  不利閾値 → actionRange = 120（敵が強化）
    ///   互角:           上記どちらでもない   → actionRange = baseActionRange
    ///
    /// 乱数 0 ～ actionRange-1 を振り、
    /// actions[i].threshold を昇順に走査して、乱数値 < threshold の最初の行動を返す。
    /// </summary>
    private EnemyActionEntry SelectEnemyAction()
    {
        // ターンスナップショットに基づいた行動テーブルと基準値
        EnemyActionEntry[] actionTable = GetTurnActionTable();
        int baseRange = GetTurnBaseActionRange();

        if (turnLowHpMode)
        {
            float hpRatio = (float)enemyCurrentHp / enemyMonster.MaxHp;
            Debug.Log($"[Battle] LowHP mode: hpRatio={hpRatio:F2} <= {enemyMonster.lowHpThreshold} -> using lowHpActions");
        }

        int playerLuc = (GameState.I != null) ? GameState.I.baseLUC : 1;
        playerLuc = ApplyPlayerLucBuffDebuff(playerLuc);

        int enemyLuc = enemyMonster.Luck;
        enemyLuc = ApplyEnemyLucBuffDebuff(enemyLuc);

        int actionRange = CalcActionRange(playerLuc, enemyLuc, baseRange);
        int roll = Random.Range(0, actionRange);

        Debug.Log($"[Battle] EnemyAction: playerLuc={playerLuc} enemyLuc={enemyLuc} " +
                  $"baseRange={baseRange} actionRange={actionRange} roll={roll} lowHpMode={turnLowHpMode}");

        EnemyActionEntry selected = PickFromTable(actionTable, roll);

        // =========================================================
        // 再抽選ループ（スマート抽選）
        // 選ばれたスキルの rerollCondition を確認し、条件を満たしていたら再抽選。
        // 最大5回まで。超えたら元の結果をそのまま使用する。
        // =========================================================
        const int maxReroll = 10;
        for (int r = 0; r < maxReroll; r++)
        {
            if (selected.skill == null) break;
            if (selected.skill.rerollCondition == RerollCondition.None) break;
            if (!ShouldReroll(selected.skill)) break;

            int reroll = Random.Range(0, actionRange);
            EnemyActionEntry next = PickFromTable(actionTable, reroll);
            Debug.Log($"[Battle] Reroll {r + 1}/{maxReroll}: {selected.skill.skillName} -> {(next.skill != null ? next.skill.skillName : "null")} (roll={reroll})");
            selected = next;
        }

        return selected;
    }

    /// <summary>
    /// 行動テーブルからロール値に対応するエントリを返す。
    /// </summary>
    private EnemyActionEntry PickFromTable(EnemyActionEntry[] table, int roll)
    {
        for (int i = 0; i < table.Length; i++)
        {
            if (roll < table[i].threshold)
                return table[i];
        }
        return table[table.Length - 1];
    }

    /// <summary>
    /// 再抽選条件を評価する。true = 再抽選すべき（条件を満たしている）。
    /// </summary>
    private bool ShouldReroll(SkillData skill)
    {
        switch (skill.rerollCondition)
        {
            case RerollCondition.TargetAlreadyAilment:
                return IsTargetAlreadyAffected(skill);

            case RerollCondition.UserNotAilment:
                return IsUserNotAffected(skill);

            case RerollCondition.UserHpAboveHalf:
                return GetEnemyHpRatio() >= 0.5f;

            case RerollCondition.UserHpAboveThreshold:
                return GetEnemyHpRatio() >= skill.rerollHpThreshold;

            default:
                return false;
        }
    }

    /// <summary>
    /// 敵の現在HP割合を返す。
    /// </summary>
    private float GetEnemyHpRatio()
    {
        if (enemyMonster == null || enemyMonster.MaxHp <= 0) return 1f;
        return (float)enemyCurrentHp / enemyMonster.MaxHp;
    }

    /// <summary>
    /// TargetAlreadyAilment: プレイヤーが既にこのスキルの状態異常にかかっているか判定。
    /// additionalEffects から StatusAilmentEffectData (Inflict) を探し、
    /// そのtargetStatusEffect でプレイヤーの状態を確認する。
    /// バフ/デバフ系は BattleBuffState のプレイヤー側を参照する。
    /// </summary>
    private bool IsTargetAlreadyAffected(SkillData skill)
    {
        if (skill.additionalEffects == null) return false;

        for (int i = 0; i < skill.additionalEffects.Count; i++)
        {
            var entry = skill.additionalEffects[i];
            if (entry == null || entry.effectData == null) continue;
            if (!(entry.effectData is StatusAilmentEffectData)) continue;
            if (entry.ailmentMode != AilmentMode.Inflict) continue;

            StatusEffect effect = entry.targetStatusEffect;

            // バフ/デバフ系: BattleBuffState で判定
            if (StatusEffectSystem.IsBuffDebuff(effect))
            {
                if (StatusEffectSystem.IsDebuff(effect))
                {
                    // デバフ → プレイヤーに付与するもの
                    ref BuffDebuffPair pair = ref buffState.player.GetPairRef(effect);
                    if (pair.IsDebuffed) return true;
                }
                else
                {
                    // バフ → 自分（敵）に付与するもの → TargetAlreadyAilment の対象外
                    // （自己バフは UserNotAilment で扱う）
                    continue;
                }
            }
            // 怒り → 自己付与なので TargetAlreadyAilment 対象外
            else if (effect == StatusEffect.Rage)
            {
                continue;
            }
            // 持続型状態異常: GameState で判定
            else if (StatusEffectSystem.IsPlayerAffected(effect))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// UserNotAilment: 敵自身がこのスキルの状態異常にかかっていない場合 true（→ 再抽選する）。
    /// 自己付与型スキル（怒り、自己バフ等）で使用。
    /// 「既にかかっている状態でのみ使いたい」ケースではなく、
    /// 「まだかかっていないなら使う意味がない」自己回復/維持スキル用。
    /// </summary>
    private bool IsUserNotAffected(SkillData skill)
    {
        if (skill.additionalEffects == null) return false;

        for (int i = 0; i < skill.additionalEffects.Count; i++)
        {
            var entry = skill.additionalEffects[i];
            if (entry == null || entry.effectData == null) continue;
            if (!(entry.effectData is StatusAilmentEffectData)) continue;
            if (entry.ailmentMode != AilmentMode.Inflict) continue;

            StatusEffect effect = entry.targetStatusEffect;

            // 怒り: enemyRageTurn で判定
            if (effect == StatusEffect.Rage)
            {
                if (enemyRageTurn <= 0) return true; // かかっていない → 再抽選
                continue;
            }

            // 自己バフ: BattleBuffState 敵側で判定
            if (StatusEffectSystem.IsBuffDebuff(effect) && !StatusEffectSystem.IsDebuff(effect))
            {
                ref BuffDebuffPair pair = ref buffState.enemy.GetPairRef(effect);
                if (!pair.IsBuffed) return true; // かかっていない → 再抽選
                continue;
            }

            // 敵の持続型状態異常（毒の自己回復等）
            // 敵側フラグで判定
            switch (effect)
            {
                case StatusEffect.Poison: if (!enemyIsPoisoned) return true; break;
                case StatusEffect.Paralyze: if (!enemyIsParalyzed) return true; break;
                case StatusEffect.Blind: if (!enemyIsBlind) return true; break;
                case StatusEffect.Silence: if (!enemyIsSilenced) return true; break;
                default: break;
            }
        }

        return false;
    }

    /// <summary>
    /// プレイヤーと敵の Luck を比較し、行動判定の乱数上限値を返す。
    ///
    /// 敵LUCが低い場合（LucFixedThresholdLimit以下）は ±10 の固定差で判定し、
    /// それ以上の場合は 1.5倍/0.5倍 の比率で判定する。
    /// </summary>
    private int CalcActionRange(int playerLuc, int enemyLuc, int baseRange)
    {
        int advThreshold;
        int disadvThreshold;

        if (enemyLuc <= LucFixedThresholdLimit)
        {
            // 低LUC域: ±10 の固定差で判定
            advThreshold = enemyLuc + 10;
            disadvThreshold = (enemyLuc - 10 > 0) ? (enemyLuc - 10) : 1;
        }
        else
        {
            // 通常域: 1.5倍/0.5倍 の比率で判定
            advThreshold = Mathf.FloorToInt(enemyLuc * 1.5f + 0.5f);
            disadvThreshold = Mathf.FloorToInt(enemyLuc * 0.5f + 0.5f);
        }

        if (playerLuc >= advThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 0.8f + 0.5f);
            Debug.Log($"[Battle] LUC判定: プレイヤー有利 " +
                      $"playerLuc={playerLuc} >= {advThreshold} " +
                      $"actionRange={range}");
            return range;
        }

        if (playerLuc < disadvThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 1.2f + 0.5f);
            Debug.Log($"[Battle] LUC判定: 敵有利 " +
                      $"playerLuc={playerLuc} < {disadvThreshold} " +
                      $"actionRange={range}");
            return range;
        }

        Debug.Log($"[Battle] LUC判定: 互角 playerLuc={playerLuc} " +
                  $"advThreshold={advThreshold} disadvThreshold={disadvThreshold} " +
                  $"actionRange={baseRange}");
        return baseRange;
    }

    // =========================================================
    // 行動実行（1回行動用、AfterEnemyAction 付き）
    // =========================================================

    /// <summary>
    /// 選択された敵行動を実行する。
    /// </summary>
    private void ExecuteEnemyAction(EnemyActionEntry action)
    {
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] EnemyActionEntry.skill が null です。通常攻撃で代替します。");
            ExecuteLegacyAttack();
            return;
        }

        // 沈黙判定: 敵が沈黙中で魔法系スキルなら70%で失敗
        if (enemyIsSilenced && action.skill.skillSource == SkillSource.Magic)
        {
            if (StatusEffectSystem.CheckSilenceFail())
            {
                string silenceName = !string.IsNullOrEmpty(action.skill.skillName)
                    ? action.skill.skillName : "魔法";
                AddLog($"{enemyMonster.Mname} は{silenceName}を唱えようとした…しかし沈黙で失敗した！");
                AfterEnemyAction();
                return;
            }
        }


        switch (action.skill.actionType)
        {
#pragma warning disable 0618 // Obsolete 警告を抑制（既存アセット互換のため）
            case MonsterActionType.NormalAttack: ExecuteEnemySkillAttack(action.skill); break;
#pragma warning restore 0618
            case MonsterActionType.SkillAttack: ExecuteEnemySkillAttack(action.skill); break;
            case MonsterActionType.Preemptive: ExecuteEnemySkillAttack(action.skill); break;
            case MonsterActionType.FoodRaid: ExecuteEnemyFoodRaid(action.skill); break;
            case MonsterActionType.Idle: ExecuteEnemyIdle(action.skill); break;
            default: ExecuteEnemyIdle(action.skill); break;
        }

        if (action.skill.enemyNextForceSkill != null)
        {
            enemyForcedNextSkill = action.skill.enemyNextForceSkill;
        }
    }

    // =========================================================
    // スキル攻撃
    // =========================================================

    /// <summary>
    /// 敵のスキル攻撃（AfterEnemyAction 付き、1回行動時用）。
    /// </summary>
    private void ExecuteEnemySkillAttack(SkillData skill)
    {
        ExecuteEnemySkillAttackCore(skill);
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵のスキル攻撃コア処理。SkillData のパラメータでダメージ計算する。
    /// AfterEnemyAction は呼ばない。呼び出し元が責任を持つ。
    /// Phase2: 敵が暗闇の場合、baseHitRate を半分にする。
    /// Phase2: 敵が怒り中の場合、攻撃力に RageAttackMultiplier を乗算する。
    ///
    /// 多段攻撃（hitCount > 1）の場合:
    ///   スキル発動自体は100%確定し、各ヒットごとに命中/回避判定を行う。
    /// </summary>
    private void ExecuteEnemySkillAttackCore(SkillData skill)
    {
        // Phase2: 暗闘補正 — 敵が暗闇なら命中率半分
        int effectiveHitRate = skill.baseHitRate;
        if (enemyIsBlind) effectiveHitRate = effectiveHitRate / 2;

        // =========================================================
        // 非ダメージスキル: 命中判定をスキップし、追加効果のみ実行
        // （ヒール等の自己対象スキルは命中判定不要）
        // =========================================================
        if (skill.IsNonDamage)
        {
            string effectSkillName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "スキル";

            // 敵対象の非ダメージスキル（毒付与等）は回避判定を行う
            if (skill.IsHostileNonDamage)
            {
                if (!CheckEnemyHit(effectiveHitRate))
                {
                    AddLog($"{enemyMonster.Mname} の{effectSkillName}！ …しかし外れた！");
                    return;
                }
            }

            AddLog($"{enemyMonster.Mname} の{effectSkillName}！");
            ProcessEnemySkillEffects(skill);
            return;
        }

        // --- ここから先はダメージスキルのみ ---

        // =========================================================
        // 多段攻撃対応: hitCount > 1 の場合はスキル発動確定、各ヒットで個別判定
        // =========================================================
        int hits = skill.EffectiveHitCount;

        // 単発スキル: 従来通りスキル発動前に命中判定
        if (hits <= 1)
        {
            if (!CheckEnemyHit(effectiveHitRate))
            {
                string missName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName
                    : $"{skill.skillAttribute.ToJapanese()}攻撃";
                AddLog($"{enemyMonster.Mname} の{missName}！ …しかし外れた！");
                return;
            }
        }

        // =========================================================
        // 乱数ダメージスキル
        // randomDamageMax > 0 の場合、1〜maxの乱数がベースダメージ。
        // クリティカル無効、防御ダイス有効。属性は None を想定。
        // =========================================================
        if (skill.randomDamageMax > 0)
        {
            int rndBase = Random.Range(1, skill.randomDamageMax + 1);
            string rndName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "攻撃";

            // 防御ダイス
            int defense = GetPlayerDefense(skill.damageCategory);

            if (skill.defenseIgnoreRate > 0f)
            {
                defense = Mathf.FloorToInt(defense * (1f - skill.defenseIgnoreRate) + 0.5f);
            }

            int blocked;
            if (isDefending)
            {
                defense *= DefendDefenseMultiplier;
                blocked = RollDefenseDice(defense, DefendDiceRange);
            }
            else
            {
                blocked = RollDefenseDice(defense);
            }
            int finalDmg = rndBase - blocked;
            if (finalDmg < 0) finalDmg = 0;

            ApplyDamageToPlayer(finalDmg);

            string blockLog = blocked > 0 ? $"（防御{blocked}軽減）" : "";
            AddLog($"{enemyMonster.Mname} の{rndName}！ {finalDmg}ダメージ！（乱数{rndBase}）{blockLog}");

            Debug.Log($"[Battle] RandomDamage: roll={rndBase} max={skill.randomDamageMax} " +
                      $"defense={defense} blocked={blocked} final={finalDmg}");

            ProcessEnemySkillEffects(skill);
            return;
        }

        // =========================================================
        // HP依存ダメージスキル
        // hpDependentType != None の場合、対象のHP割合でダメージを決定。
        // 防御/属性耐性/バフ/クリティカルは全てスキップ。
        // =========================================================
        if (skill.IsHpDependent)
        {

            // =========================================================
            // CurrentHpDamage: 使用者のHP依存ダメージ（自爆系）
            // 防御ダイス・属性耐性を適用する独立処理
            // =========================================================
            if (skill.hpDependentType == HpDependentType.CurrentHpDamage)
            {
                string cdName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName : "攻撃";

                if (!CheckEnemyHit(effectiveHitRate))
                {
                    AddLog($"{enemyMonster.Mname} の{cdName}！ …しかし外れた！");
                    // 自爆系スキルは外しても自滅する（追加効果の SelfDestruct を実行）
                    ProcessEnemySkillEffects(skill);
                    return;
                }

                // ダメージ = 使用者（敵）の現在HP
                int baseDamage = enemyCurrentHp;
                if (baseDamage < 1) baseDamage = 1;

                // 属性耐性を適用
                int resistance = PassiveCalculator.CalcTotalAttributeResistance(skill.skillAttribute);
                float reductionRate = resistance / 100f;
                int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
                if (afterResist < 0) afterResist = 0;

                // 防御ダイスを適用（defenseIgnoreRate 対応）
                int defense = GetPlayerDefense(skill.damageCategory);
                if (skill.defenseIgnoreRate > 0f)
                {
                    defense = Mathf.FloorToInt(defense * (1f - skill.defenseIgnoreRate) + 0.5f);
                }
                int blocked;
                if (isDefending)
                {
                    defense *= DefendDefenseMultiplier;
                    blocked = RollDefenseDice(defense, DefendDiceRange);
                }
                else
                {
                    blocked = RollDefenseDice(defense);
                }
                int finalDamage = afterResist - blocked;
                if (finalDamage < 0) finalDamage = 0;

                ApplyDamageToPlayer(finalDamage);

                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";

                AddLog($"{enemyMonster.Mname} の{cdName}！ {finalDamage}ダメージ！（残りHP{baseDamage}）{logSuffix}");

                Debug.Log($"[Battle] CurrentHpDamage(Enemy→Player): userHp={baseDamage} " +
                          $"resistance={resistance} afterResist={afterResist} " +
                          $"defense={defense} blocked={blocked} final={finalDamage}");

                ProcessEnemySkillEffects(skill);
                return;
            }


            string hpDepName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "攻撃";

            // 単発前提（多段併用不可）: 命中判定
            if (!CheckEnemyHit(effectiveHitRate))
            {
                AddLog($"{enemyMonster.Mname} の{hpDepName}！ …しかし外れた！");
                return;
            }

            int playerHp = (GameState.I != null) ? GameState.I.currentHp : 0;
            int hpDamage = CalcHpDependentDamage(skill.hpDependentType, playerHp,
                         (GameState.I != null) ? GameState.I.maxHp : 0, skill.hpDependentPercent);

            ApplyDamageToPlayer(hpDamage);

            string hpDepLog;
            switch (skill.hpDependentType)
            {
                case HpDependentType.HalfCurrentHp: hpDepLog = "（HP半減）"; break;
                case HpDependentType.ReduceToOne: hpDepLog = "（HP→1）"; break;
                case HpDependentType.MaxHpPercent: hpDepLog = $"（最大HPの{skill.hpDependentPercent}%）"; break;
                default: hpDepLog = ""; break;
            }
            AddLog($"{enemyMonster.Mname} の{hpDepName}！ {hpDamage}ダメージ！{hpDepLog}");

            Debug.Log($"[Battle] HpDependent(Enemy→Player): type={skill.hpDependentType} " +
                      $"beforeHp={playerHp} damage={hpDamage}");

            ProcessEnemySkillEffects(skill);
            return;
        }


        // --- ダメージ計算（多段攻撃対応） ---
        int totalDamage = 0;
        int hitSuccess = 0;

        if (hits > 1)
        {
            string multiName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "攻撃";
            AddLog($"{enemyMonster.Mname} の{multiName}！（{hits}回攻撃）");
        }

        for (int h = 0; h < hits; h++)
        {
            // ★多段攻撃: 全ヒット（1発目含む）で個別に命中判定
            // ★単発攻撃: ループ前で判定済みなのでここはスキップ
            if (hits > 1 && !CheckEnemyHit(effectiveHitRate))
            {
                AddLog($"  {h + 1}撃目 …外れた！");
                continue;
            }

            float hitMul = skill.GetHitDamageMultiplier(h);
            int hitBonus = skill.GetHitBonusDamage(h);
            int baseDamage;
            if (hitMul > 0f)
            {
                int attackPower = ApplyEnemyAttackBuffDebuff(enemyMonster.Attack);
                // Phase2: 怒り中は攻撃力1.5倍
                if (enemyRageTurn > 0)
                {
                    attackPower = Mathf.FloorToInt(attackPower * StatusEffectSystem.RageAttackMultiplier + 0.5f);
                }
                baseDamage = Mathf.FloorToInt(attackPower * hitMul + 0.5f);
            }
            else
                baseDamage = 0;
            baseDamage += hitBonus;
            if (baseDamage < 1) baseDamage = 1;

            WeaponAttribute hitAttr = skill.GetHitAttribute(h);
            int resistance = PassiveCalculator.CalcTotalAttributeResistance(hitAttr);

            float reductionRate = resistance / 100f;
            int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
            if (afterResist < 0) afterResist = 0;

            int defense = GetPlayerDefense(skill.damageCategory);

            if (skill.defenseIgnoreRate > 0f)
            {
                defense = Mathf.FloorToInt(defense * (1f - skill.defenseIgnoreRate) + 0.5f);
            }

            int blocked;
            if (isDefending)
            {
                defense *= DefendDefenseMultiplier;
                blocked = RollDefenseDice(defense, DefendDiceRange);
            }
            else
            {
                blocked = RollDefenseDice(defense);
            }
            int finalDamage = afterResist - blocked;
            if (finalDamage < 0) finalDamage = 0;

            ApplyDamageToPlayer(finalDamage);
            totalDamage += finalDamage;
            hitSuccess++;

            if (hits > 1)
            {
                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";
                string attrTag = skill.HasMultiHitEntries ? $"（{hitAttr.ToJapanese()}）" : "";
                AddLog($"  {h + 1}撃目{attrTag} {finalDamage}ダメージ！{logSuffix}");
            }
            else
            {
                // 単発攻撃: 従来ログ
                string actionName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName
                    : $"{skill.skillAttribute.ToJapanese()}攻撃";

                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性で軽減+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";

                AddLog($"{enemyMonster.Mname} の{actionName}！（{hitAttr.ToJapanese()}属性） " +
                       $"{finalDamage}ダメージ！{logSuffix}");
            }

            Debug.Log($"[Battle] SkillAttack hit {h + 1}/{hits}: base={baseDamage} resistance={resistance} " +
                      $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage} defending={isDefending}");

            // 途中でプレイヤーが倒れたら残りをスキップ
            if (GameState.I != null && GameState.I.currentHp <= 0) break;
        }

        // 多段攻撃の合計ログ
        if (hits > 1)
        {
            AddLog($"  → 合計 {totalDamage}ダメージ！（{hitSuccess}/{hits}命中）");
        }

        // 追加効果の実行（全ヒット完了後に1回だけ）
        ProcessEnemySkillEffects(skill);
    }

    // =========================================================
    // Idle
    // =========================================================

    /// <summary>
    /// 敵が何もしない（AfterEnemyAction 付き、1回行動時用）。
    /// </summary>
    private void ExecuteEnemyIdle(SkillData skill)
    {
        ExecuteEnemyIdleCore(skill);
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵が何もしない（コア処理、AfterEnemyAction なし）。
    /// </summary>
    private void ExecuteEnemyIdleCore(SkillData skill)
    {
        string actionName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "様子を見ている";
        AddLog($"{enemyMonster.Mname} は{actionName}…");
    }

    // =========================================================
    // 食い荒らし (FoodRaid)
    // =========================================================

    /// <summary>
    /// 敵の食い荒らし行動（AfterEnemyAction 付き、1回行動時用）。
    /// </summary>
    private void ExecuteEnemyFoodRaid(SkillData skill)
    {
        ExecuteEnemyFoodRaidCore(skill);
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵の食い荒らし行動コア処理。AfterEnemyAction は呼ばない。
    ///
    /// プレイヤーの所持消費アイテム(Consumable)からランダムに1つを食べる。
    ///
    /// 処理フロー:
    ///   1. ItemBoxManager から Consumable カテゴリの所持品を列挙
    ///   2. 0個 → 不発ログ「道具袋を漁ったが、不満そうに戻って行った……」
    ///   3. 1個以上 → ランダムに1つ選択
    ///   4. 選ばれたアイテムの IsBattleAttackItem(battleDamage>0) で分岐:
    ///      - true  → 「まずい！」敵は MaxHp/2 のダメージ(クランプなし・即死可)
    ///      - false → 「うまい！」敵は MaxHp/2 を回復(MaxHp でクランプ)
    ///   5. アイテムを RemoveItem で削除(transformInto は参照しない)
    ///
    /// 仕様:
    ///   - 命中判定なし(必中)
    ///   - 防御ダイス・属性耐性・バフ/デバフ一切なし
    ///   - 追加効果(additionalEffects)は実行しない
    ///   - 多段攻撃とは非対応(hitCount無視)
    ///   - SkillData.skillName は不発・成功ログのどちらにも使用しない
    ///     (行動名は「食い荒らし」「道具袋を漁った」等でハードコード)
    /// </summary>
    private void ExecuteEnemyFoodRaidCore(SkillData skill)
    {
        // プレイヤーの消費アイテムを列挙
        var pool = new System.Collections.Generic.List<InventoryItem>();
        if (ItemBoxManager.Instance != null)
        {
            var items = ItemBoxManager.Instance.GetItems();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var inv = items[i];
                    if (inv == null || inv.data == null) continue;
                    if (inv.data.category == ItemCategory.Consumable)
                    {
                        pool.Add(inv);
                    }
                }
            }
        }

        // 不発: 消費アイテムを持っていない
        if (pool.Count == 0)
        {
            AddLog($"{enemyMonster.Mname} は突然道具袋を漁ったが、不満そうに戻って行った……");
            Debug.Log($"[Battle] FoodRaid: {enemyMonster.Mname} - no consumable items (miss)");
            return;
        }

        // ランダムに1つ選択
        int pick = Random.Range(0, pool.Count);
        InventoryItem target = pool[pick];
        string itemName = target.data.itemName;

        // 共通ログ: 「〇〇は△△を食べてしまった！」
        AddLog($"{enemyMonster.Mname}は{itemName}を食べてしまった！");

        // アイテム効果による分岐
        int half = enemyMonster.MaxHp / 2;
        if (half < 1) half = 1; // 念のため最低値保証(MaxHpが1の特殊モンスター対策)

        if (target.data.IsBattleAttackItem)
        {
            // 攻撃アイテム → まずい！ ダメージ(クランプなし、即死可)
            enemyCurrentHp -= half;
            // 下限クランプは行わない(enemyCurrentHp が負になっても AfterEnemyAction で勝利判定される)
            AddLog($"まずい！　{half}のダメージ！");
            Debug.Log($"[Battle] FoodRaid: {enemyMonster.Mname} ate {itemName} (attack) -> {half} damage, HP now {enemyCurrentHp}");
        }
        else
        {
            // 回復系アイテム → うまい！ 回復(MaxHpでクランプ)
            int healed = half;
            if (enemyCurrentHp + healed > enemyMonster.MaxHp)
            {
                healed = enemyMonster.MaxHp - enemyCurrentHp;
                if (healed < 0) healed = 0;
            }
            enemyCurrentHp += healed;
            AddLog($"うまい！　HPを{healed}回復！");
            Debug.Log($"[Battle] FoodRaid: {enemyMonster.Mname} ate {itemName} (non-attack) -> {healed} heal, HP now {enemyCurrentHp}");
        }

        // アイテムを消費(transformInto は参照しない: 純粋に消えるだけ)
        if (ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.RemoveItem(target);
        }

        // HP変化を UI に反映
        RefreshBattleStatusEffectUI();
    }

    /// <summary>
    /// 敵スキルの追加効果を実行する共通メソッド。
    /// </summary>
    private void ProcessEnemySkillEffects(SkillData skill)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: false,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyIsStunned,
            ref enemyCurrentHp,
            0,
            ref enemyIsParalyzed,
            ref enemyIsBlind,
            ref enemyIsSilenced,
            ref enemyRageTurn,
            ref playerRageTurn,
            ref buffState);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }

        RefreshBattleStatusEffectUI();
    }
    /// <summary>
    /// 敵の行動後の共通処理。
    /// ターン終了時の毒ダメージを適用し、
    /// 怒りターンカウントダウンを行い、
    /// プレイヤー敗北判定を行い、生存していればプレイヤーターンに戻す。
    ///
    /// 【Phase B1: 石化ターン処理】
    /// TickBuffDebuffTurns の直後に石化ティックを追加。
    /// 敵石化完成は enemyCurrentHp=0 で OnVictory に乗せ、
    /// プレイヤー石化完成は PlayerPetrifyReachedZero を見て OnDefeat へ。
    /// 両方完成した場合は敗北優先（既存の相打ちルール踏襲）。
    ///
    /// 敵HP0チェックが関数冒頭にあるため、敵を既に倒した場合は
    /// このメソッド自体がティック処理の手前でリターンし、
    /// 「敵を倒した場合はカウントが減らない」仕様が自動達成される。
    /// </summary>
    private void AfterEnemyAction()
    {
        if (enemyCurrentHp <= 0)
        {
            // プレイヤーも倒れている場合（敵の自爆で相打ち）は敗北扱い
            if (GameState.I != null && GameState.I.currentHp <= 0)
            {
                FlushLogsAndThen(() => OnDefeat());
            }
            else
            {
                FlushLogsAndThen(() => OnVictory());
            }
            return;
        }

        // --- プレイヤーの毒ダメージ ---
        int playerPoisonDmg = StatusEffectSystem.ApplyBattlePoisonToPlayer();
        if (playerPoisonDmg > 0)
        {
            AddLog($"You は毒のダメージで {playerPoisonDmg} 受けた！");
        }

        // --- 敵の毒ダメージ ---
        if (enemyIsPoisoned && enemyMonster != null)
        {
            int enemyPoisonDmg = StatusEffectSystem.CalcBattlePoisonDamage(enemyMonster.MaxHp);
            enemyCurrentHp -= enemyPoisonDmg;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;
            AddLog($"{enemyMonster.Mname} は毒のダメージで {enemyPoisonDmg} 受けた！");

            if (enemyCurrentHp <= 0)
            {
                // 毒で敵が倒れた → ログを表示してから勝利処理
                FlushLogsAndThen(() => OnVictory());
                return;
            }
        }

        // =========================================================
        // 敵の自動回復（ターン終了時）
        // 毒ダメージで倒れなかった場合のみ回復する。
        // 最大HPを超えない。
        // =========================================================
        if (enemyMonster.autoRegenEnabled && enemyCurrentHp > 0)
        {
            int regenAmount = Mathf.Min(enemyMonster.autoRegenAmount, enemyMonster.MaxHp - enemyCurrentHp);
            if (regenAmount > 0)
            {
                enemyCurrentHp += regenAmount;
                AddLog($"{enemyMonster.Mname} は {regenAmount} HP回復した！");
            }
        }

        // =========================================================
        // 怒りターンカウントダウン（Phase2追加）
        // =========================================================
        if (enemyRageTurn > 0)
        {
            enemyRageTurn--;
            if (enemyRageTurn <= 0)
            {
                AddLog($"{enemyMonster.Mname} の怒りが収まった。");
            }
        }
        if (playerRageTurn > 0)
        {
            playerRageTurn--;
            if (playerRageTurn <= 0)
            {
                AddLog("You の怒りが収まった。");
            }
        }

        // =========================================================
        // バフ/デバフ ターンカウントダウン（Phase4: 全5種一括）
        // =========================================================
        {
            var buffLogs = TickBuffDebuffTurns();
            for (int bl = 0; bl < buffLogs.Count; bl++)
            {
                AddLog(buffLogs[bl]);
            }
        }

        // =========================================================
        // 石化ターンカウントダウン（Phase B1追加）
        // プレイヤー → 敵 の順で処理。
        // 敵が石化完成した場合は enemyCurrentHp=0 にして OnVictory へ。
        // プレイヤー石化完成の敗北判定は後続の FlushLogsAndThen で処理。
        // =========================================================
        {
            string plog = TickPlayerPetrifyTurns();
            if (!string.IsNullOrEmpty(plog)) AddLog(plog);

            string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";
            string elog = TickEnemyPetrifyTurns(eName);
            if (!string.IsNullOrEmpty(elog)) AddLog(elog);

            // 敵の石化完成 → 勝利ルート（ただしプレイヤーも倒れていたら敗北優先）
            if (EnemyPetrifyJustReachedZero)
            {
                enemyCurrentHp = 0; // 既存の勝利ルートに乗せる

                // プレイヤーが HP0 or 石化完成なら相打ち → 敗北優先
                bool playerDead = GameState.I != null && GameState.I.currentHp <= 0;
                if (playerDead || PlayerPetrifyReachedZero)
                {
                    FlushLogsAndThen(() => OnDefeat());
                }
                else
                {
                    FlushLogsAndThen(() => OnVictory());
                }
                return;
            }
        }


        // ログを全部表示してから勝敗判定・ターン移行
        FlushLogsAndThen(() =>
        {
            // プレイヤー敗北判定（HP0 or 石化完成）
            if (GameState.I != null
                && (GameState.I.currentHp <= 0 || PlayerPetrifyReachedZero))
            {
                OnDefeat();
                return;
            }

            // プレイヤーターンに戻す
            SetButtonsInteractable(true);
            RefreshSkillButton();
            RefreshMagicSelector();
            RefreshBattleStatusEffectUI();
        });
    }



}