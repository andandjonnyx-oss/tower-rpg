using System;
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

    private bool currentIsFull;

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
    bool cannotIgnore = false)
    {
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

        // ボタン設定
        if (getButton != null)
        {
            if (isFull)
            {
                // 満杯 → 「交換する」ボタンとして有効化
                getButton.interactable = true;
                var txt = getButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "整理する";
            }
            else
            {
                getButton.interactable = canGet;
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
    }


    public void HideImmediate()
    {
        if (windowRoot != null)
            windowRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }



    private void OnClickGet()
    {
        if (currentIsFull)
            Close(ItemPickupResult.Exchange);
        else
            Close(ItemPickupResult.Get);
    }

    private void OnClickIgnore()
    {
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