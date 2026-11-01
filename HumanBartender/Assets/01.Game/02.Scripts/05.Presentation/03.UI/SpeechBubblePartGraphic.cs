using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>스프라이트 한 파트를 그린다. 원본의 투명 여백을 제외하고 꼬리 연결 구간은 겹치지 않게 비운다.</summary>
[AddComponentMenu("UI/Speech Bubble Part Graphic")]
public sealed class SpeechBubblePartGraphic : MaskableGraphic
{
    [SerializeField] Sprite sprite;
    [SerializeField, Min(0)] float sourceInsetPixels = 1;
    readonly Rect[] cutouts = new Rect[3];
    readonly List<Rect> regions = new List<Rect>(16);
    readonly List<Rect> scratch = new List<Rect>(16);

    public Sprite Sprite => sprite;
    public override Texture mainTexture => sprite != null ? sprite.texture : base.mainTexture;

    public void SetCutout(int index, Rect value)
    {
        if (cutouts[index] == value) return;
        cutouts[index] = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect area = rectTransform.rect;
        if (sprite == null || area.width <= 0 || area.height <= 0) return;

        regions.Clear();
        regions.Add(area);
        foreach (Rect cutout in cutouts)
        {
            if (cutout.width <= 0 || cutout.height <= 0) continue;
            scratch.Clear();
            foreach (Rect region in regions) Subtract(region, cutout, scratch);
            regions.Clear();
            regions.AddRange(scratch);
        }

        Vector4 outer = DataUtility.GetOuterUV(sprite);
        Vector4 padding = DataUtility.GetPadding(sprite);
        Rect pixels = sprite.rect;
        float width = Mathf.Max(1, pixels.width - padding.x - padding.z);
        float height = Mathf.Max(1, pixels.height - padding.y - padding.w);
        // Keep bilinear samples inside the gutter, with a guard for stretched subpixel edges.
        // Untrimmed tails use a half texel so their original silhouette stays intact.
        float inset = Mathf.Min(sourceInsetPixels + (sourceInsetPixels > 0 ? 1 : .5f), Mathf.Min(pixels.width, pixels.height) * .5f);
        Vector4 uv = new Vector4(
            Mathf.Lerp(outer.x, outer.z, Mathf.Max(0, inset - padding.x) / width),
            Mathf.Lerp(outer.y, outer.w, Mathf.Max(0, inset - padding.y) / height),
            Mathf.Lerp(outer.z, outer.x, Mathf.Max(0, inset - padding.z) / width),
            Mathf.Lerp(outer.w, outer.y, Mathf.Max(0, inset - padding.w) / height));

        foreach (Rect region in regions) AddQuad(mesh, area, region, uv);
    }

    static void Subtract(Rect area, Rect cutout, List<Rect> output)
    {
        float left = Mathf.Max(area.xMin, cutout.xMin), right = Mathf.Min(area.xMax, cutout.xMax);
        float bottom = Mathf.Max(area.yMin, cutout.yMin), top = Mathf.Min(area.yMax, cutout.yMax);
        if (right <= left || top <= bottom) { output.Add(area); return; }
        if (left > area.xMin) output.Add(Rect.MinMaxRect(area.xMin, area.yMin, left, area.yMax));
        if (right < area.xMax) output.Add(Rect.MinMaxRect(right, area.yMin, area.xMax, area.yMax));
        if (bottom > area.yMin) output.Add(Rect.MinMaxRect(left, area.yMin, right, bottom));
        if (top < area.yMax) output.Add(Rect.MinMaxRect(left, top, right, area.yMax));
    }

    void AddQuad(VertexHelper mesh, Rect area, Rect region, Vector4 uv)
    {
        float u0 = Mathf.Lerp(uv.x, uv.z, (region.xMin - area.xMin) / area.width);
        float u1 = Mathf.Lerp(uv.x, uv.z, (region.xMax - area.xMin) / area.width);
        float v0 = Mathf.Lerp(uv.y, uv.w, (region.yMin - area.yMin) / area.height);
        float v1 = Mathf.Lerp(uv.y, uv.w, (region.yMax - area.yMin) / area.height);
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(region.xMin, region.yMin), color, new Vector2(u0, v0));
        mesh.AddVert(new Vector3(region.xMin, region.yMax), color, new Vector2(u0, v1));
        mesh.AddVert(new Vector3(region.xMax, region.yMax), color, new Vector2(u1, v1));
        mesh.AddVert(new Vector3(region.xMax, region.yMin), color, new Vector2(u1, v0));
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
