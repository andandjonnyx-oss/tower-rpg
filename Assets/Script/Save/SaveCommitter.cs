using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SaveManager / SettingsStore の遅延書き込みを「安全地点」で実際にディスクへ確定させる常駐コンポーネント。
/// GameStateAutoCreate と同じパターンでどのシーンから起動しても必ず存在する。
///
/// 【コミット地点（＝クラッシュ時に巻き戻りうる境界）】
///   1. シーン遷移時（activeSceneChanged）
///      … 戦闘終了・街到着・アイテム画面の出入りなど、このゲームの区切りは
///        ほぼすべてシーン遷移なので、これが主経路。
///   2. シーン到着の1フレーム後（sceneLoaded + 1frame）
///      … MainSceneAutoSave など「到着時 Start() でセーブする」既存コードの
///        書き込みを到着直後に確定させるため。
///   3. アプリの一時停止／フォーカス喪失／終了
///      … モバイルのホームボタン・タスクキル前、PC のウィンドウ切り替え・終了。
///
/// ⚠️ 1シーンに長居している間（塔で連続移動中など）はディスク未反映の期間がある。
///    クラッシュ耐性が特に必要な箇所を新設する場合は SaveManager.CommitIfDirty()
///    を明示的に呼ぶこと（多重呼び出しは無害。dirty でなければ即 return する）。
/// </summary>
public class SaveCommitter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateIfNeeded()
    {
        if (FindAnyObjectByType<SaveCommitter>() != null) return;
        var go = new GameObject("SaveCommitter");
        go.AddComponent<SaveCommitter>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void CommitAll()
    {
        SaveManager.CommitIfDirty();
        SettingsStore.CommitIfDirty();
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        CommitAll();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーン内オブジェクトの Start()（MainSceneAutoSave 等）が走り終わるのを
        // 1フレーム待ってからコミットする。
        StartCoroutine(CommitNextFrame());
    }

    private IEnumerator CommitNextFrame()
    {
        yield return null;
        CommitAll();
    }

    private void OnApplicationPause(bool paused)
    {
        // モバイル: ホームボタン／他アプリ切り替え。この後プロセスが
        // 予告なく kill されうるので、ここでの確定が生命線。
        if (paused) CommitAll();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CommitAll();
    }

    private void OnApplicationQuit()
    {
        CommitAll();
    }
}
