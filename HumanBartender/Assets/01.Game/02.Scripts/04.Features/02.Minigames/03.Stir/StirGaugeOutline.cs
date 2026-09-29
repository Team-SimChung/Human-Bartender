using UnityEngine;
using UnityEngine.UI;

/// <summary>칸 내부는 비워두고 바깥에 푸른 테두리와 부드러운 빛번짐을 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StirGaugeOutline : MaskableGraphic
{
    [SerializeField, Min(0f)] float thickness = 1.5f;
    [SerializeField, Min(0f)] float glowWidth = 2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect inner = GetPixelAdjustedRect();
        if (inner.width <= 0f || inner.height <= 0f) return;

        Rect border = Expand(inner, Mathf.Max(0f, thickness));
        if (thickness > 0f) AddRing(vh, inner, border, color, color);
        if (glowWidth <= 0f) return;

        Color glow = color;
        glow.a *= 0.5f;
        Color transparent = glow;
        transparent.a = 0f;
        AddRing(vh, border, Expand(border, glowWidth), glow, transparent);
    }

    static Rect Expand(Rect rect, float amount)
    {
        return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount,
            rect.xMax + amount, rect.yMax + amount);
    }

    static void AddRing(VertexHelper vh, Rect inner, Rect outer, Color innerColor, Color outerColor)
    {
        int start = vh.currentVertCount;
        AddCorners(vh, inner, innerColor);
        AddCorners(vh, outer, outerColor);
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            vh.AddTriangle(start + i, start + 4 + i, start + 4 + next);
            vh.AddTriangle(start + i, start + 4 + next, start + next);
        }
    }

    static void AddCorners(VertexHelper vh, Rect rect, Color tint)
    {
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.zero);
    }
}
