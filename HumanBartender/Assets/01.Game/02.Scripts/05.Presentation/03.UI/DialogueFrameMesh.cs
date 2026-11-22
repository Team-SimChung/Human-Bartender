using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>원본 말풍선의 모서리와 꼬리는 같은 배율로 유지하고 직선·본문 부분만 늘린다.</summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class DialogueFrameMesh : BaseMeshEffect
{
    [Tooltip("스프라이트 안에서 본문 바깥 여백. 왼쪽·아래·오른쪽·위 순서, 원본 픽셀 단위.")]
    public Vector4 bodyInsets = new Vector4(0, 5, 0, 71);
    [Min(.01f)] public float unitsPerPixel = .22f;
    [Min(1)] public float cornerPixels = 64;
    [Min(1)] public float tailHalfWidthPixels = 36;

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        var image = (Image)graphic;
        var sprite = image.overrideSprite;
        if (sprite == null) return;
        Rect source = sprite.rect, area = graphic.rectTransform.rect;
        float bottom = bodyInsets.y, top = source.height - bodyInsets.w;
        float left = bodyInsets.x, right = source.width - bodyInsets.z;
        float scale = Mathf.Min(unitsPerPixel, area.height / (2 * cornerPixels),
            area.width / (2 * cornerPixels + 2 * tailHalfWidthPixels));
        if (scale <= 0 || top <= bottom || right <= left) return;
        float middle = (left + right) * .5f;
        Vector4 padding = DataUtility.GetPadding(sprite);
        float visibleWidth = source.width - padding.x - padding.z;
        float visibleHeight = source.height - padding.y - padding.w;
        var sx = new[] { padding.x, left + cornerPixels, middle - tailHalfWidthPixels,
            middle + tailHalfWidthPixels, right - cornerPixels, source.width - padding.z };
        var sy = new[] { padding.y, bottom, bottom + cornerPixels, top - cornerPixels, top, source.height - padding.w };
        var dx = new[] { area.xMin + (padding.x - left) * scale, area.xMin + cornerPixels * scale,
            area.center.x - tailHalfWidthPixels * scale, area.center.x + tailHalfWidthPixels * scale,
            area.xMax - cornerPixels * scale, area.xMax + (bodyInsets.z - padding.z) * scale };
        var dy = new[] { area.yMin + (padding.y - bottom) * scale, area.yMin, area.yMin + cornerPixels * scale,
            area.yMax - cornerPixels * scale, area.yMax, area.yMax + (bodyInsets.w - padding.w) * scale };
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        mesh.Clear();
        for (int y = 0; y < sy.Length; y++)
        for (int x = 0; x < sx.Length; x++)
            mesh.AddVert(new Vector3(dx[x], dy[y]), image.color,
                new Vector2(Mathf.Lerp(uv.x, uv.z, (sx[x] - padding.x) / visibleWidth), Mathf.Lerp(uv.y, uv.w, (sy[y] - padding.y) / visibleHeight)));
        for (int y = 0; y < sy.Length - 1; y++)
        for (int x = 0; x < sx.Length - 1; x++)
        {
            int i = y * sx.Length + x;
            mesh.AddTriangle(i, i + sx.Length, i + 1);
            mesh.AddTriangle(i + 1, i + sx.Length, i + sx.Length + 1);
        }
    }
}
