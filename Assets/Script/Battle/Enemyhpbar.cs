using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敵のHPバーを表示するコンポーネント。
/// マジックアイテム「ルーペ」を所持している場合のみ表示する。
/// ルーペを複数所持している場合はバーの横幅にボーナスを付ける（お遊び要素）。
///   倍率 = 1 + 0.1 × (所持数 - 1)
///   例: 1個=1.0倍, 2個=1.1倍, 5個=1.4倍, 10個=1.9倍
///
/// 使い方:
///   1. 敵画像の上に Slider（HPバー）を配置
///   2. このスクリプトを Slider にアタッチ
///   3. hpSlider に自身の Slider をアサイン
///   4. fillImage に Slider の Fill Area > Fill の Image をアサイン
///   5. backgroundImage に Slider の Background の Image をアサイン
///   6. loupeItemId にルーペの itemId を設定（例: "M001_Loupe"）
///
/// HPバーは毎フレーム BattleSceneController の敵HP を監視して更新する。
/// （HpMpDisplay.cs と同じパターン）
/// </summary>
public class EnemyHpBar : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("HPバー用の Slider コンポーネント")]
    [SerializeField] private Slider hpSlider;

    [Tooltip("Slider の Fill Image（HP=0 で非表示にするため）")]
    [SerializeField] private Image fillImage;

    [Tooltip("Slider の Background Image（HP=満タンで非表示にするため）")]
    [SerializeField] private Image backgroundImage;

    [Header("Loupe Settings")]
    [Tooltip("ルーペの itemId。この itemId のアイテムを所持していればHPバーを表示する")]
    [SerializeField] private string loupeItemId = "M001_Loupe";

    [Tooltip("ルーペ2個目以降1個あたりの追加倍率（デフォルト 0.1）")]
    [SerializeField] private float bonusPerExtra = 0.1f;

    // HPバーのルート GameObject（表示/非表示切替用）
    private GameObject barRoot;

    // 前フレームの値（変化時のみ更新）
    private int lastHp = -1;
    private int lastMaxHp = -1;

    private void Start()
    {
        barRoot = (hpSlider != null) ? hpSlider.gameObject : gameObject;

        // ルーペ所持判定
        int loupeCount = CountLoupeItems();

        if (loupeCount <= 0)
        {
            // ルーペ未所持 → HPバー非表示
            barRoot.SetActive(false);
            Debug.Log("[EnemyHpBar] ルーペ未所持 → HPバー非表示");
            return;
        }

        // ルーペ所持 → HPバー表示
        barRoot.SetActive(true);
        Debug.Log($"[EnemyHpBar] ルーペ所持数={loupeCount} → HPバー表示");

        // ルーペ2個以上 → 横幅ボーナス: 1 + 0.1 × (count - 1)
        if (loupeCount >= 2)
        {
            float multiplier = 1f + bonusPerExtra * (loupeCount - 1);
            RectTransform rt = barRoot.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 size = rt.sizeDelta;
                size.x *= multiplier;
                rt.sizeDelta = size;
                Debug.Log($"[EnemyHpBar] ルーペ{loupeCount}個所持 → バー横幅{multiplier:F1}倍");
            }
        }

        // Slider の初期設定
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.value = 1f;
            // インタラクション無効化（ユーザーが操作できないようにする）
            hpSlider.interactable = false;
        }

        // 初期状態: HP満タン → Background を隠す
        UpdateFillBackgroundVisibility(1f);
    }

    private void Update()
    {
        if (hpSlider == null) return;
        if (!barRoot.activeSelf) return;

        int currentHp = BattleSceneController.EnemyCurrentHp;
        int maxHp = BattleSceneController.EnemyMaxHp;

        // 値が変化した時だけ更新
        if (currentHp != lastHp || maxHp != lastMaxHp)
        {
            lastHp = currentHp;
            lastMaxHp = maxHp;

            float ratio = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
            hpSlider.value = ratio;
            UpdateFillBackgroundVisibility(ratio);
        }
    }

    /// <summary>
    /// HP比率に応じて Fill と Background の表示/非表示を切り替える。
    /// HP=0   → Fill を非表示（赤のBackgroundだけ見える）
    /// HP=満タン → Background を非表示（緑のFillだけ見える）
    /// それ以外  → 両方表示
    /// </summary>
    private void UpdateFillBackgroundVisibility(float ratio)
    {
        if (fillImage != null)
        {
            fillImage.enabled = (ratio > 0f);
        }
        if (backgroundImage != null)
        {
            backgroundImage.enabled = (ratio < 1f);
        }
    }

    /// <summary>
    /// ItemBoxManager からルーペの所持数をカウントする。
    /// </summary>
    private int CountLoupeItems()
    {
        if (ItemBoxManager.Instance == null) return 0;
        if (string.IsNullOrEmpty(loupeItemId)) return 0;

        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return 0;

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].data != null &&
                items[i].data.itemId == loupeItemId)
            {
                count++;
            }
        }
        return count;
    }
}