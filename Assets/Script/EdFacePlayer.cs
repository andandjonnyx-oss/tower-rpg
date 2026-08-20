using System;
using UnityEngine;

[Serializable]
public struct FaceKeyframe
{
    public float time;                       // 何秒の時点で
    public int body, hair, brow, eye, mouth; // どの表情にするか
}

public class EdFacePlayer : MonoBehaviour
{
    public FaceComposer composer;
    public FaceKeyframe[] keyframes;   // time昇順で並べておく
    public AudioSource songSource;     // 曲(歌)。これの再生位置を基準にする

    int _next;

    void OnEnable() => _next = 0;

    void Update()
    {
        if (composer == null || keyframes == null || keyframes.Length == 0) return;

        // 基準時刻: 曲があれば曲の再生位置、なければ起動からの経過
        float t = songSource != null ? songSource.time : Time.timeSinceLevelLoad;

        // 現在時刻を過ぎたキーフレームを順に適用
        while (_next < keyframes.Length && t >= keyframes[_next].time)
        {
            var k = keyframes[_next];
            composer.Compose(k.body, k.hair, k.brow, k.eye, k.mouth);
            _next++;
        }
    }

    // 頭出し・ループ用
    public void ResetTimeline() => _next = 0;
}