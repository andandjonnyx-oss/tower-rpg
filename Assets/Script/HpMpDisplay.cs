using TMPro;
using UnityEngine;

/// <summary>
/// HP/MP をリアルタイム表示する汎用コンポーネント。
/// Battle、Tower、Status、Main など、どのシーンにも置ける。
/// GameState の値を毎フレーム監視して自動更新する。
/// recoverOnStart を ON にすると、シーン開始時に HP/MP を全回復する。
/// </summary>
public class HpMpDisplay : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text mpText;

    [Header("Recovery")]
    [Tooltip("ONにすると、このシーンに入った時にHP/MPを全回復する（Mainシーン用）")]
    [SerializeField] private bool recoverOnStart = false;

    // 前フレームの値（変化があった時だけテキストを更新する）
    private int lastHp = -1;
    private int lastMaxHp = -1;
    private int lastMp = -1;
    private int lastMaxMp = -1;

    private void Start()
    {
        if (recoverOnStart && GameState.I != null)
        {
            GameState.I.currentHp = GameState.I.maxHp;
            GameState.I.currentMp = GameState.I.maxMp;
            Debug.Log($"[HpMpDisplay] 全回復: HP={GameState.I.currentHp}/{GameState.I.maxHp}");
        }
    }

    private void Update()
    {
        if (GameState.I == null) return;

        var gs = GameState.I;

        // HP が変化した時だけ更新
        if (gs.currentHp != lastHp || gs.maxHp != lastMaxHp)
        {
            lastHp = gs.currentHp;
            lastMaxHp = gs.maxHp;
            if (hpText != null)
                hpText.text = $"HP：{gs.currentHp}/{gs.maxHp}";
        }

        // MP が変化した時だけ更新
        if (gs.currentMp != lastMp || gs.maxMp != lastMaxMp)
        {
            lastMp = gs.currentMp;
            lastMaxMp = gs.maxMp;
            if (mpText != null)
                mpText.text = $"MP：{gs.currentMp}/{gs.maxMp}";
        }
    }
}