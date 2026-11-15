using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>미리 배치한 Image를 재사용해 파츠 또는 단일 초상을 표시한다.</summary>
public sealed class ProfilePortraitView : MonoBehaviour
{
    public enum Mode
    {
        [InspectorName("파츠형")] Parts = 0,
        [InspectorName("단일 이미지")] SingleImage = 1,
    }

    [Serializable]
    public struct Layer
    {
        public Sprite sprite;
        [Tooltip("투명 여백을 포함한 원본 프레임의 정규화 위치와 크기입니다.")]
        public Rect frame;
    }

    [Header("미리 배치한 초상 파츠")]
    [SerializeField] Image[] layers;
    [SerializeField] GameObject missing;
    [SerializeField] AspectRatioFitter framing;

    public void Show(Mode mode, Sprite singlePortrait, Layer[] portrait)
    {
        bool single = mode == Mode.SingleImage;
        Layer first = default;
        bool available = single ? TrySingleLayer(singlePortrait, out first) : portrait != null && portrait.Length > 0;
        if (!single && available) first = portrait[0];
        if (available && framing != null && first.sprite != null && first.frame.width > 0 && first.frame.height > 0)
        {
            framing.aspectRatio = first.sprite.rect.width / first.sprite.rect.height * first.frame.height / first.frame.width;
        }
        for (int i = 0; i < layers.Length; i++)
        {
            var image = layers[i];
            bool show = available && (single ? i == 0 : i < portrait.Length && portrait[i].sprite != null);
            image.gameObject.SetActive(show);
            if (!show) continue;
            var layer = single ? first : portrait[i];
            image.sprite = layer.sprite;
            var frame = layer.frame;
            image.rectTransform.anchorMin = frame.min;
            image.rectTransform.anchorMax = frame.max;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }
        if (missing != null) missing.SetActive(!available);
    }

    // PNG의 투명 여백은 제외하되 원본 파일과 스프라이트는 그대로 둔다.
    // 기존 파츠 프레임과 같은 좌표를 사용하므로 카드·상세창 모두 같은 구도로 표시된다.
    static bool TrySingleLayer(Sprite sprite, out Layer layer)
    {
        layer = default;
        if (sprite == null) return false;
        var vertices = sprite.vertices;
        if (vertices.Length == 0) return false;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var vertex in vertices)
        {
            Vector2 pixel = vertex * sprite.pixelsPerUnit + sprite.pivot;
            min = Vector2.Min(min, pixel);
            max = Vector2.Max(max, pixel);
        }
        var size = max - min;
        if (size.x <= 0 || size.y <= 0) return false;
        layer = new Layer { sprite = sprite, frame = new Rect(-min.x / size.x, -min.y / size.y,
            sprite.rect.width / size.x, sprite.rect.height / size.y) };
        return true;
    }
}
