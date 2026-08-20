using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//enum 列挙型　
public enum ItemPickupResult
{
    Get,
    Ignore,
    Exchange
}


public class ItemPickupWindow : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject windowRoot;

    [Header("UI")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image itemImage;
    [SerializeField] private Button getButton;
    [SerializeField] private Button ignoreButton;

    [Header("Input Lock")]
    [SerializeField] private float inputLockSeconds = 0.4f; // ★追加：表示直後にボタンを無効化する時間

    private bool currentIsFull;
    private Coroutine unlockCoroutine; // ★追加

    //ItemPickupResult を受け取る関数を保存する変数
    //ポップアップを表示して後で結果（入手or廃棄）を返すUIに向いている
    private Action<ItemPickupResult> onResult;

    private void Awake()
    {

        //AddListener スクリプト側でボタン処理登録　
        //今回のようにスクリプト内でButtonが定義されている場合に利用
        if (getButton != null)
            getButton.onClick.AddListener(OnClickGet);

        if (ignoreButton != null)
            ignoreButton.onClick.AddListener(OnClickIgnore);

    }

    public void Show(
    string itemName,
    string description,
    Sprite sprite,
    bool canGet,
    bool isFull,
    Action<ItemPickupResult> resultCallback,
    bool cannotIgnore = false,
    bool playSe = true)
    {
        // アイテム発見SE（整理後の再表示など、鳴らしたくない場合は playSe=false）
        if (playSe && AudioManager.I != null)
            AudioManager.I.PlayItemFoundSe();


        onResult = resultCallback;
        this.currentIsFull = isFull;

        if (itemNameText != null)
            itemNameText.text = itemName;

        if (descriptionText != null)
        {
            if (canGet)
                descriptionText.text = description;
            else if (isFull)
                descriptionText.text = $"{description}\n\nアイテムが一杯です。整理してください。";
            else
                descriptionText.text = $"{description}\n\nこれ以上持てないため入手できません。";
        }

        if (itemImage != null)
        {
            itemImage.sprite = sprite;
            itemImage.enabled = sprite != null;
        }

        // ボタン設定（本来の有効/無効状態を決める）
        bool getButtonDesired = false; // ★追加：ロック解除後に復帰させる値を保持
        if (getButton != null)
        {
            if (isFull)
            {
                // 満杯 → 「交換する」ボタンとして有効化
                getButtonDesired = true; // ★変更
                var txt = getButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "整理する";
            }
            else
            {
                getButtonDesired = canGet; // ★変更
                var txt = getButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "入手する";
            }
        }

        // 諦めるボタンの表示制御
        if (ignoreButton != null)
        {
            ignoreButton.gameObject.SetActive(!cannotIgnore);
        }

        if (windowRoot != null)
            windowRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        // ★追加：表示直後の連打貫通を防ぐ入力ロック
        BeginInputLock(getButtonDesired, !cannotIgnore);
    }

    // ★追加：一定時間ボタンを無効化し、その後 desired 値へ復帰させる
    private void BeginInputLock(bool getButtonDesired, bool ignoreButtonActive)
    {
        // 一旦すべて無効化
        if (getButton != null) getButton.interactable = false;
        if (ignoreButton != null) ignoreButton.interactable = false;

        if (unlockCoroutine != null) StopCoroutine(unlockCoroutine);
        unlockCoroutine = StartCoroutine(UnlockAfterDelay(getButtonDesired, ignoreButtonActive));
    }

    // ★追加
    private IEnumerator UnlockAfterDelay(bool getButtonDesired, bool ignoreButtonActive)
    {
        // Time.timeScale の影響を受けない実時間待ち
        yield return new WaitForSecondsRealtime(inputLockSeconds);

        if (getButton != null) getButton.interactable = getButtonDesired;
        if (ignoreButton != null) ignoreButton.interactable = ignoreButtonActive;
        unlockCoroutine = null;
    }


    public void HideImmediate()
    {
        // ★追加：閉じる際にロックコルーチンを停止（次表示への持ち越し防止）
        if (unlockCoroutine != null)
        {
            StopCoroutine(unlockCoroutine);
            unlockCoroutine = null;
        }

        if (windowRoot != null)
            windowRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }



    private void OnClickGet()
    {
        if (currentIsFull)
        {
            // 「整理する」: まだ入手していないので鳴らさない
            Close(ItemPickupResult.Exchange);
        }
        else
        {
            // 「入手する」: 道具袋に入るタイミング
            if (AudioManager.I != null) AudioManager.I.PlayItemGetSe();
            Close(ItemPickupResult.Get);
        }
    }

    private void OnClickIgnore()
    {
        // 諦めるSE
        if (AudioManager.I != null) AudioManager.I.PlayItemDiscardSe();
        Close(ItemPickupResult.Ignore);
    }

    private void Close(ItemPickupResult result)
    {
        HideImmediate();

        //onResultをコピーして初期化
        //?. はif(～!= null)
        //Invokeは関数の実行 Action等ではこれを使う
        var callback = onResult;
        onResult = null;
        callback?.Invoke(result);
    }
}