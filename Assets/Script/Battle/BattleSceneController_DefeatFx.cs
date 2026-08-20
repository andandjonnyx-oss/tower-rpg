using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleSceneController の撃破演出パート。
///
/// 勝利確定（OnVictory 入口）から、敵画像（enemyImage）の退場演出を再生し、
/// 完了後に既存の勝利本体処理（OnVictoryCore）へ合流させる。
///
/// 演出の出し分け（判定は OnVictory 入口で確定済みの引数で受け取る）:
///   通常モンスター … 回転しながらランダムな360度方向へ飛んでいく
///   ボスモンスター … 数回点滅 → 震えながら下へ沈むように消える
///   餌付け勝利     … 演出なし（画像不変）→ OnVictory 側で即 Core 呼び出し
///   第二形態連戦   … 演出なし（OnVictoryCore 内の既存ロジックで画像差し替え）
/// </summary>
public partial class BattleSceneController : MonoBehaviour
{
    // =========================================================
    // 撃破演出パラメータ（調整用）
    // =========================================================

    /// <summary>通常モンスター: 飛散演出の所要時間（秒）。</summary>
    private const float NormalDefeatDuration = 0.7f;
    /// <summary>通常モンスター: 飛んでいく距離（px）。画面外まで飛ばす想定。</summary>
    private const float NormalDefeatFlyDistance = 1400f;
    /// <summary>通常モンスター: 回転の総回転量（度）。</summary>
    private const float NormalDefeatSpin = 720f;

    /// <summary>ボス: 1回の点滅（消灯→点灯）にかける時間（秒）。</summary>
    private const float BossDefeatBlinkDuration = 0.12f;
    /// <summary>ボス: 1回目の点滅後の待機（秒）。</summary>
    private const float BossDefeatWait1 = 1.0f;
    /// <summary>ボス: 2回目の点滅後の待機（秒）。</summary>
    private const float BossDefeatWait2 = 0.5f;
    /// <summary>ボス: 3回目の点滅後の待機（秒）。</summary>
    private const float BossDefeatWait3 = 0.5f;
    /// <summary>ボス: 震えながら沈むフェードアウトの所要時間（秒）。</summary>
    private const float BossDefeatSinkDuration = 1.2f;
    /// <summary>ボス: 震えの横揺れ幅（px）。位置の基準は固定。</summary>
    private const float BossDefeatShakeAmplitude = 14f;
    /// <summary>ボス: 震えの速さ（大きいほど細かく振動）。</summary>
    private const float BossDefeatShakeSpeed = 50f;
    /// <summary>ボス: 画像高さが取得できない場合の沈降距離フォールバック（px）。</summary>
    private const float BossDefeatSinkFallback = 600f;

    /// <summary>連戦: 第一形態が消える（フェードアウト）時間（秒）。</summary>
    private const float Phase1VanishDuration = 0.4f;

    /// <summary>
    /// 撃破演出を再生してから OnVictoryCore() を呼ぶ。
    /// </summary>
    /// <param name="isBoss">true ならボス演出、false なら通常モンスター演出。</param>
    private IEnumerator PlayDefeatThenVictory(bool isBoss)
    {
        if (enemyImage != null)
        {
            if (isBoss) yield return StartCoroutine(BossDefeatRoutine());
            else yield return StartCoroutine(NormalDefeatRoutine());

            // 演出後は画像を隠す（飛散・沈降の最終状態を固定）
            enemyImage.enabled = false;
        }

        OnVictoryCore();
    }

    /// <summary>
    /// 連戦（第二形態へ移行）時の演出。
    /// 第一形態の画像をフェードアウトして消した後、OnVictoryCore() を呼ぶ。
    /// OnVictoryCore 内でシーンが再読込され、第二形態が Start() で表示される。
    /// </summary>
    private IEnumerator Phase1VanishThenContinue()
    {
        if (enemyImage != null)
        {
            Color baseColor = enemyImage.color;

            float t = 0f;
            while (t < Phase1VanishDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Phase1VanishDuration);

                Color c = baseColor;
                c.a = Mathf.Lerp(1f, 0f, k);
                enemyImage.color = c;

                yield return null;
            }

            // 第一形態を非表示にし、色を戻しておく（次形態は Start() で再設定される）
            enemyImage.enabled = false;
            enemyImage.color = baseColor;
        }

        OnVictoryCore();
    }


    /// <summary>
    /// 通常モンスター撃破演出。
    /// 画像が回転しながら、ランダムな360度方向へ飛んでいく（イーズアウト）。
    /// </summary>
    private IEnumerator NormalDefeatRoutine()
    {
        RectTransform rt = enemyImage.rectTransform;

        Vector2 startPos = rt.anchoredPosition;
        Quaternion startRot = rt.localRotation;

        // ランダムな360度方向の単位ベクトル
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 endPos = startPos + dir * NormalDefeatFlyDistance;

        // 回転方向もランダム（時計回り / 反時計回り）
        float spin = NormalDefeatSpin * (UnityEngine.Random.value < 0.5f ? 1f : -1f);

        float t = 0f;
        while (t < NormalDefeatDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / NormalDefeatDuration);
            // イーズアウト（最初速く、徐々に減速）
            float ease = 1f - (1f - k) * (1f - k);

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            rt.localRotation = startRot * Quaternion.Euler(0f, 0f, spin * ease);

            yield return null;
        }

        rt.anchoredPosition = endPos;
    }

    /// <summary>
    /// ボスモンスター撃破演出。
    /// 点滅→1秒→点滅→0.5秒→点滅→0.5秒→
    /// 左右に震えながら下へ沈むようにフェードアウト（位置の基準は固定）。
    /// </summary>
    private IEnumerator BossDefeatRoutine()
    {
        // --- ① 点滅 → 待機 を3回（待機時間は 1.0s / 0.5s / 0.5s） ---
        yield return StartCoroutine(BossBlinkOnce());
        yield return new WaitForSeconds(BossDefeatWait1);

        yield return StartCoroutine(BossBlinkOnce());
        yield return new WaitForSeconds(BossDefeatWait2);

        yield return StartCoroutine(BossBlinkOnce());
        yield return new WaitForSeconds(BossDefeatWait3);

        // --- ② 震えながら下へ移動してフェードアウト。元画像の底より下はマスクで隠す ---
        // 親が通常 Canvas でマスクが無いため、実行時に RectMask2D を動的生成して
        // enemyImage をその子に入れ、マスク矩形（= 元画像と同じ領域）の外（底より下）を
        // クリップする。これにより「地面に潜っていく」表現になる。

        RectTransform imgRt = enemyImage.rectTransform;

        // 元の親子情報を保存（演出後に完全復元する）
        Transform origParent = imgRt.parent;
        int origSiblingIndex = imgRt.GetSiblingIndex();
        Vector3 origWorldPos = imgRt.position;
        Vector2 origAnchoredPos = imgRt.anchoredPosition;
        Vector2 origAnchorMin = imgRt.anchorMin;
        Vector2 origAnchorMax = imgRt.anchorMax;
        Vector2 origPivot = imgRt.pivot;
        Vector2 origSizeDelta = imgRt.sizeDelta;
        Color baseColor2 = enemyImage.color;

        // マスク用オブジェクトを生成し、元画像と同じ位置・サイズ・親に配置
        GameObject maskGo = new GameObject("BossDefeatMask", typeof(RectTransform));
        RectTransform maskRt = maskGo.GetComponent<RectTransform>();
        maskRt.SetParent(origParent, false);
        maskRt.anchorMin = origAnchorMin;
        maskRt.anchorMax = origAnchorMax;
        maskRt.pivot = origPivot;
        maskRt.sizeDelta = origSizeDelta;
        maskRt.position = origWorldPos;
        maskRt.SetSiblingIndex(origSiblingIndex);
        maskGo.AddComponent<RectMask2D>();

        // enemyImage をマスクの子に移動（ワールド位置維持）
        imgRt.SetParent(maskRt, true);

        // マスク基準でのローカル開始位置を記録
        Vector2 startLocal = imgRt.anchoredPosition;

        // 沈む距離（元画像の高さ分だけ下げれば、底ラインから完全に潜って見えなくなる）
        float imgHeight = origSizeDelta.y != 0f ? Mathf.Abs(origSizeDelta.y) : imgRt.rect.height;
        float sinkDistance = imgHeight > 0f ? imgHeight : BossDefeatSinkFallback;

        float t = 0f;
        while (t < BossDefeatSinkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / BossDefeatSinkDuration);

            // 下へ移動（底より下はマスクで隠れる）
            float sinkY = -sinkDistance * k;
            // 横方向の震え（消えるにつれて弱める）
            float shakeX = Mathf.Sin(t * BossDefeatShakeSpeed) * BossDefeatShakeAmplitude * (1f - k);

            imgRt.anchoredPosition = startLocal + new Vector2(shakeX, sinkY);

            // 全体を通して徐々にフェードアウト
            Color c = baseColor2;
            c.a = Mathf.Lerp(1f, 0f, k);
            enemyImage.color = c;

            yield return null;
        }

        // --- 復元: enemyImage を元の親・位置・色に戻し、マスクを破棄 ---
        imgRt.SetParent(origParent, false);
        imgRt.SetSiblingIndex(origSiblingIndex);
        imgRt.anchorMin = origAnchorMin;
        imgRt.anchorMax = origAnchorMax;
        imgRt.pivot = origPivot;
        imgRt.sizeDelta = origSizeDelta;
        imgRt.anchoredPosition = origAnchoredPos;
        enemyImage.color = baseColor2;

        Destroy(maskGo);
    }

    /// <summary>
    /// 1回点滅する（消灯→点灯）。
    /// </summary>
    private IEnumerator BossBlinkOnce()
    {
        enemyImage.enabled = false;
        yield return new WaitForSeconds(BossDefeatBlinkDuration);
        enemyImage.enabled = true;
        yield return new WaitForSeconds(BossDefeatBlinkDuration);
    }
}