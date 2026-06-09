using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// オプション画面を開くボタン。
/// 「戻り先シーン名」を GameState.optionReturnScene に記憶してから Option へ遷移する。
/// タイトル / メイン / 塔内部 など複数シーンに設置し、それぞれ returnSceneName を設定する。
///
/// Inspector 設定:
///   optionSceneName : 開くオプションシーン名（既定 "Option"）
///   returnSceneName : 戻る先シーン名（このボタンを置いたシーン名を入れる。
///                     例: タイトルなら "Title"、メインなら "Main"、塔内部なら塔シーン名）
///
/// ボタンの OnClick に OnClickOpenOption() を登録する。
/// </summary>
public class OpenOptionButton : MonoBehaviour
{
    [SerializeField] private string optionSceneName = "Option";
    [Tooltip("オプションから戻る先のシーン名（このボタンを置いたシーンの名前）")]
    [SerializeField] private string returnSceneName = "Title";

    public void OnClickOpenOption()
    {
        // 戻り先を記憶（GameState は DontDestroyOnLoad で常駐）
        if (GameState.I != null)
            GameState.I.optionReturnScene = returnSceneName;

        SceneManager.LoadScene(optionSceneName);
    }
}