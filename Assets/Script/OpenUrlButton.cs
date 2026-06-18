using UnityEngine;

public class OpenUrlButton : MonoBehaviour
{
    [Tooltip("ボタンを押したときに開くURL")]
    [SerializeField] private string url = "";

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