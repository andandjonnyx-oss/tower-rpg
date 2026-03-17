using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenItemBoxButton : MonoBehaviour
{
    [SerializeField] private string itemBoxSceneName = "Itembox";

    public void OnClickOpenItemBox()
    {
        // アイテム取得ポップアップ中なら開かない
        if (TowerItemTrigger.Instance != null && TowerItemTrigger.Instance.IsBusy)
            return;

        // 必要なら他の busy もここで確認
        SceneManager.LoadScene(itemBoxSceneName);
    }
}