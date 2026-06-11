using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 会話図鑑（ZukanT）からスタッフロールを閲覧するためのボタン。
/// スタッフロール到達済み（endingPhase >= 3）の場合のみ表示する。
/// 閲覧モードで StaffRoll シーンへ遷移し、終了後はこのシーンに戻ってくる。
/// </summary>
[RequireComponent(typeof(Button))]
public class StaffRollZukanButton : MonoBehaviour
{
    private void Start()
    {
        bool viewable = GameState.I != null
                     && GameState.I.endingPhase >= EndingManager.PhaseEpilogue;
        gameObject.SetActive(viewable);

        if (viewable)
            GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (AudioManager.I != null) AudioManager.I.PlayPopupSe();

        // 閲覧モードで遷移（戻り先 = 現在のシーン）
        GameState.I.staffRollReturnScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(EndingManager.StaffRollSceneName);
    }
}