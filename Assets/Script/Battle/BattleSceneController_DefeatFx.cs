using System.Collections;
using UnityEngine;

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

    /// <summary>ボス: 点滅の回数。</summary>
    private const int BossDefeatBlinkCount = 4;
    /// <summary>ボス: 点滅1回（消灯→点灯）の時間（秒）。</summary>
    private const float BossDefeatBlinkInterval = 0.12f;
    /// <summary>ボス: 震えながら沈む演出の所要時間（秒）。</summary>
    private const float BossDefeatSinkDuration = 1.0f;
    /// <summary>ボス: 沈む距離（px）。</summary>
    private const float BossDefeatSinkDistance = 400f;
    /// <summary>ボス: 震えの横揺れ幅（px）。</summary>
    private const float BossDefeatShakeAmplitude = 14f;

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
    /// 数回点滅 → 震えながら下へ沈むように消えていく。
    /// </summary>
    private IEnumerator BossDefeatRoutine()
    {
        RectTransform rt = enemyImage.rectTransform;
        Vector2 basePos = rt.anchoredPosition;

        // --- ① 点滅 ---
        for (int i = 0; i < BossDefeatBlinkCount; i++)
        {
            enemyImage.enabled = false;
            yield return new WaitForSeconds(BossDefeatBlinkInterval);
            enemyImage.enabled = true;
            yield return new WaitForSeconds(BossDefeatBlinkInterval);
        }

        // --- ② 震えながら下へ沈む ---
        Color baseColor = enemyImage.color;
        float t = 0f;
        while (t < BossDefeatSinkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / BossDefeatSinkDuration);

            // 下方向へ沈む
            float sinkY = -BossDefeatSinkDistance * k;
            // 横方向の震え（沈むほど弱める）
            float shakeX = Mathf.Sin(t * 50f) * BossDefeatShakeAmplitude * (1f - k);

            rt.anchoredPosition = basePos + new Vector2(shakeX, sinkY);

            // 後半でフェードアウト
            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01((k - 0.4f) / 0.6f));
            enemyImage.color = c;

            yield return null;
        }

        // 元の色に戻しておく（次戦闘での再利用に備える。直後 enabled=false される）
        enemyImage.color = baseColor;
    }
}