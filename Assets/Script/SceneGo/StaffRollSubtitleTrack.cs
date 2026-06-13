using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// スタッフロール用の字幕（歌詞）トラック。
/// シーン開始（= BGM開始）からの経過秒数に合わせて字幕を切り替える。
/// StaffRollController のスライドショーとは独立して動く。
///
/// 使い方:
///   1. StaffRoll シーンの字幕用 TMP_Text（画面下部）にこのコンポーネントをアタッチ
///      （または空オブジェクトにアタッチして subtitleText をアサイン）
///   2. entries に「開始秒数 + テキスト」を曲のタイムスタンプ通りに登録
///   3. 行間を空けたい（前奏・間奏など）場合は、空テキストのエントリを挟む
///
/// タイミングは「前の字幕からの相対秒」ではなく「曲頭からの絶対秒」で指定する。
/// これにより、1行のタイミングを調整しても以降の行に影響しない（歌詞ズレ防止）。
/// </summary>
public class StaffRollSubtitleTrack : MonoBehaviour
{
    [System.Serializable]
    public class SubtitleEntry
    {
        [Tooltip("この字幕を表示し始める秒数（シーン開始＝曲の頭からの絶対秒）")]
        public float startTime;

        [Tooltip("字幕テキスト。空にすると字幕を消す（前奏・間奏など）")]
        [TextArea(1, 3)]
        public string text;
    }

    [Header("Subtitles")]
    [Tooltip("字幕リスト。startTime の昇順に登録する。\n"
           + "次のエントリの startTime に達すると自動的に切り替わる。\n"
           + "最後のエントリはシーン終了まで表示され続ける\n"
           + "（途中で消したい場合は空テキストのエントリを最後に追加する）。")]
    [SerializeField] private SubtitleEntry[] entries;

    [Tooltip("全体のタイミング微調整（秒）。\n"
           + "正の値で字幕が遅く、負の値で早く出る。曲とのズレ補正用。")]
    [SerializeField] private float timeOffset = 0f;

    [Header("UI")]
    [Tooltip("字幕を表示する TMP_Text。未設定の場合は自分自身から取得を試みる。")]
    [SerializeField] private TMP_Text subtitleText;

    private void Start()
    {
        if (subtitleText == null)
            subtitleText = GetComponent<TMP_Text>();

        if (subtitleText == null)
        {
            Debug.LogWarning("[StaffRollSubtitle] subtitleText が未設定です。字幕を表示できません。");
            return;
        }

        subtitleText.text = "";

        if (entries == null || entries.Length == 0) return;

        StartCoroutine(PlaySubtitles());
    }

    /// <summary>
    /// シーン開始時刻を基準（= 曲の頭）として、各エントリの startTime に達したら
    /// 字幕を切り替える。待機は絶対時刻ベースで判定するため、
    /// 行数が増えても累積ズレが発生しない。
    /// </summary>
    private IEnumerator PlaySubtitles()
    {
        float baseTime = Time.time;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            float showAt = baseTime + e.startTime + timeOffset;
            while (Time.time < showAt)
                yield return null;

            subtitleText.text = e.text ?? "";
        }
    }
}