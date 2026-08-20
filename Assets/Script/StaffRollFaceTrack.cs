using System.Collections;
using UnityEngine;

/// <summary>
/// スタッフロール用の表情トラック。
/// StaffRollSubtitleTrack（歌詞）と同じ仕組みで、シーン開始（= BGM開始）からの
/// 経過秒数に合わせて FaceComposer の各パーツ番号を切り替える。
///
/// 使い方:
///   1. 空オブジェクト（EdFacePlayer 等）にこのコンポーネントをアタッチ
///   2. composer に Faceroot の FaceComposer をアサイン
///   3. entries に「開始秒数 + 各パーツ番号」を歌詞と同じタイムスタンプで登録
///
/// タイミングは歌詞側と同じく「曲頭からの絶対秒」で指定するため、
/// 1行調整しても以降に影響せず、歌詞と表情がズレない。
/// </summary>
public class StaffRollFaceTrack : MonoBehaviour
{
    [System.Serializable]
    public class FaceEntry
    {
        [Tooltip("この表情に切り替える秒数（シーン開始＝曲の頭からの絶対秒）")]
        public float startTime;
        [Tooltip("身体(karada)の番号")] public int body = 0;
        [Tooltip("髪(kami)の番号")] public int hair = 0;
        [Tooltip("眉(mayu)の番号")] public int brow = 0;
        [Tooltip("目(me)の番号")] public int eye = 0;
        [Tooltip("口(kuti)の番号")] public int mouth = 0;
    }

    [Header("Face")]
    [Tooltip("表情を切り替える対象の FaceComposer。")]
    [SerializeField] private FaceComposer composer;

    [Tooltip("表情リスト。startTime の昇順に登録する。\n"
           + "次のエントリの startTime に達すると自動的に切り替わる。")]
    [SerializeField] private FaceEntry[] entries;

    [Tooltip("全体のタイミング微調整（秒）。\n"
           + "正の値で表情が遅く、負の値で早く切り替わる。曲とのズレ補正用。")]
    [SerializeField] private float timeOffset = 0f;

    private void Start()
    {
        if (composer == null)
        {
            Debug.LogWarning("[StaffRollFace] composer が未設定です。表情を切り替えできません。");
            return;
        }
        if (entries == null || entries.Length == 0) return;
        StartCoroutine(PlayFaces());
    }

    /// <summary>
    /// シーン開始時刻を基準（= 曲の頭）として、各エントリの startTime に達したら
    /// 表情を切り替える。歌詞側と同じ絶対時刻ベースのため累積ズレが発生しない。
    /// </summary>
    private IEnumerator PlayFaces()
    {
        float baseTime = Time.time;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            float showAt = baseTime + e.startTime + timeOffset;
            while (Time.time < showAt)
                yield return null;

            composer.Compose(e.body, e.hair, e.brow, e.eye, e.mouth);
        }
    }
}