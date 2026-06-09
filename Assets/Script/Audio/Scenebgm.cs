using UnityEngine;

/// <summary>
/// シーンに置くだけで BGM を制御する小さなコンポーネント（一時停止/再開モデル版）。
/// 各シーンの Manager を触らずに、BGM の振る舞いを宣言的に設定できる。
///
/// 【モード】
///   Play  : このシーンの「主BGM」を宣言する。
///           ・useFloorRanges = OFF: bgm（単一クリップ）を主BGMとして再生。
///               タイトル / メイン などに使用。
///           ・useFloorRanges = ON : 現在階（GameState.floor）に応じて
///               floorBgmRanges から該当クリップを選んで再生。
///               塔内部に使用（10階区切り等を Inspector で設定）。
///           入室時の挙動（PlayMain 経由）:
///             ・同じ曲が鳴っていれば継続
///             ・一時停止中なら続きから再開
///             ・違う曲なら新規再生（深度で曲が変わった時は自動的に切替）
///
///   Pause : 入室時に AudioManager.PauseMain() を呼び、主BGMを一時停止する（位置保持）。
///           このシーンを抜けて別シーンへ入ると自動で再開される。
///           → 会話（Talk）に使用。
///
/// 【置かないシーン】
///   ステータス / アイテムボックス / 倉庫 / 図鑑 / 会話図鑑 / オプション。
///   何もしないことで直前のBGM状態を引き継ぐ（自動再開も AudioManager が面倒を見る）。
///
/// 【バトル】
///   BattleSceneController が PlayOverlay / StopOverlay(KeepSilent) で制御。ここには置かない。
/// </summary>
public class SceneBgm : MonoBehaviour
{
    public enum Mode
    {
        Play,   // 主BGMを再生/継続/再開
        Pause,  // 主BGMを一時停止
    }

    /// <summary>
    /// 階層レンジ1件。minFloor〜maxFloor（両端含む）に該当する時 clip を主BGMにする。
    /// </summary>
    [System.Serializable]
    public class FloorBgmRange
    {
        [Tooltip("このレンジの下限階（含む）")]
        public int minFloor = 1;
        [Tooltip("このレンジの上限階（含む）")]
        public int maxFloor = 10;
        [Tooltip("この階層帯で流す BGM")]
        public AudioClip clip;
    }

    [Header("Mode")]
    [Tooltip("Play=このシーンの主BGMを鳴らす（タイトル/メイン/塔）\n"
           + "Pause=主BGMを一時停止する（会話）")]
    [SerializeField] private Mode mode = Mode.Play;

    [Header("Play モード（単一クリップ）")]
    [Tooltip("useFloorRanges=OFF のとき鳴らす主BGM。\n"
           + "拠点系で共通BGMを継続させたい複数シーンには同じクリップをアサインする。")]
    [SerializeField] private AudioClip bgm;

    [Tooltip("BGM をループ再生するか。通常は ON。")]
    [SerializeField] private bool loop = true;

    [Header("Play モード（階層レンジ：塔用）")]
    [Tooltip("ON にすると現在階（GameState.floor）に応じて floorBgmRanges から BGM を選ぶ。\n"
           + "塔内部で深度に応じて曲を変えたい場合に使用。")]
    [SerializeField] private bool useFloorRanges = false;

    [Tooltip("階層帯ごとの BGM 設定。上から順に判定し、最初に該当したレンジを採用する。\n"
           + "例: 1〜10 / 11〜20 / 21〜30 …\n"
           + "どのレンジにも該当しない場合は fallbackBgm（未設定なら何もしない）。")]
    [SerializeField] private FloorBgmRange[] floorBgmRanges;

    [Tooltip("どのレンジにも該当しない階のときに鳴らす BGM（任意）。\n"
           + "未設定の場合、該当なし時は何もしない（直前のBGMを継続）。")]
    [SerializeField] private AudioClip fallbackBgm;

    private void Start()
    {
        if (AudioManager.I == null)
        {
            Debug.LogWarning("[SceneBgm] AudioManager が存在しません。BGM 制御をスキップします。");
            return;
        }

        switch (mode)
        {
            case Mode.Play:
                PlayForCurrentContext();
                break;

            case Mode.Pause:
                AudioManager.I.PauseMain();
                break;
        }
    }

    /// <summary>
    /// 現在の状況（現在階など）に応じた主BGMを今すぐ適用する。
    /// 同一シーン内で階が変わった時など、Start() 以外のタイミングで
    /// BGM を切り替えたい場合に外部（TowerState 等）から呼ぶ。
    /// PlayMain は「同じ曲なら継続／違う曲なら頭から」を判定するため、
    /// 同じレンジ内で連続して呼んでも曲はリスタートしない。
    /// </summary>
    public void ApplyForCurrentFloor()
    {
        if (AudioManager.I == null) return;
        if (mode != Mode.Play) return;
        PlayForCurrentContext();
    }

    private void PlayForCurrentContext()
    {
        if (useFloorRanges)
        {
            AudioClip clip = SelectClipForFloor();
            if (clip != null)
                AudioManager.I.PlayMain(clip, loop);
            // clip が null（該当レンジなし & fallback 未設定）の場合は何もしない＝直前を継続
            return;
        }

        if (bgm != null)
            AudioManager.I.PlayMain(bgm, loop);
        else
            Debug.LogWarning("[SceneBgm] Play モードですが bgm が未設定です。");
    }

    /// <summary>
    /// 現在階（GameState.floor）に該当する BGM を floorBgmRanges から選ぶ。
    /// 上から順に判定し最初に該当したものを返す。該当なしなら fallbackBgm。
    /// </summary>
    private AudioClip SelectClipForFloor()
    {
        int floor = (GameState.I != null) ? GameState.I.floor : 1;

        if (floorBgmRanges != null)
        {
            for (int i = 0; i < floorBgmRanges.Length; i++)
            {
                var r = floorBgmRanges[i];
                if (r == null) continue;
                if (floor >= r.minFloor && floor <= r.maxFloor)
                {
                    if (r.clip != null) return r.clip;
                    break; // 該当レンジはあるが clip 未設定 → fallback へ
                }
            }
        }
        return fallbackBgm; // 該当なし or clip 未設定
    }
}