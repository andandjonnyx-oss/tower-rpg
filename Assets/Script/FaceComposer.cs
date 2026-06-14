using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class FaceComposer : MonoBehaviour
{
    [System.Serializable]
    public class FacePart
    {
        public string label;      // "口" "目" など表示用
        public Image image;       // 重ねる対象
        public Sprite[] sprites;  // 連番(00,01,02... or a/b含む全部)
        public int index;         // 現在の番号

        public void Apply()
        {
            if (image == null || sprites == null || sprites.Length == 0) return;
            index = Mathf.Clamp(index, 0, sprites.Length - 1);
            image.sprite = sprites[index];
        }

        public void Step(int delta)
        {
            if (sprites == null || sprites.Length == 0) return;
            index = Mathf.Clamp(index + delta, 0, sprites.Length - 1);
            Apply();
        }

        public void SetIndex(int i)
        {
            index = i;
            Apply();
        }
    }

    [Header("奥→手前の順")]
    public FacePart body;   // karada
    public FacePart hair;   // kami
    public FacePart brow;   // mayu
    public FacePart eye;    // me
    public FacePart mouth;  // kuti

    FacePart[] _all;
    public FacePart[] All => _all ??= new[] { body, hair, brow, eye, mouth };

    void OnValidate() => ApplyAll();

    public void ApplyAll()
    {
        foreach (var p in All) p?.Apply();
    }

    // 図鑑などからの一括指定
    public void Compose(int bodyIdx, int hairIdx, int browIdx, int eyeIdx, int mouthIdx)
    {
        body.SetIndex(bodyIdx);
        hair.SetIndex(hairIdx);
        brow.SetIndex(browIdx);
        eye.SetIndex(eyeIdx);
        mouth.SetIndex(mouthIdx);
    }
}