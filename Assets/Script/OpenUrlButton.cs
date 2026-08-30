using UnityEngine;

public class OpenUrlButton : MonoBehaviour
{
    [Tooltip("ボタンを押したときに開くURL")]
    [SerializeField] private string url = "";

    private void Awake()
    {
#if UNITY_SWITCH
        // Switch: 外部ブラウザへの遷移が無いため、URLボタン（公式X/Discord等）は
        // ボタンごと非表示にする。このコンポーネントが付いた全ボタンに自動適用
        // されるので、シーン側の改修は不要。
        gameObject.SetActive(false);
#endif
    }

    // ボタンの OnClick から呼び出す
    public void OpenUrl()
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[OpenUrlButton] URLが設定されていません");
            return;
        }
        Application.OpenURL(url);
    }
}