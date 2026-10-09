using UnityEngine;
using UnityEngine.UI;

/// <summary>Rounded panels, seat dots and rings without a separate bitmap resource.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StoryTutorialShape : MaskableGraphic
{
    [SerializeField] float radius = 8f;
    [SerializeField] float border;

    public void Style(Color tint, float cornerRadius, float borderWidth = 0f)
    {
        color = tint;
        radius = cornerRadius;
        border = borderWidth;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        StoryUiMesh.Rounded(mesh, rectTransform.rect, radius, border, color);
    }
}

internal static class StoryUiMesh
{
    public static void Quad(VertexHelper mesh, Rect rect, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    public static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
    {
        if ((to - from).sqrMagnitude < .001f) return;
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(from - normal, tint, Vector2.zero);
        mesh.AddVert(from + normal, tint, Vector2.zero);
        mesh.AddVert(to + normal, tint, Vector2.zero);
        mesh.AddVert(to - normal, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    public static void Rounded(VertexHelper mesh, Rect rect, float radius, float border, Color tint)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        radius = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
        const int segments = 8;
        const int count = (segments + 1) * 4;
        int start = mesh.currentVertCount;
        if (border <= 0) mesh.AddVert(rect.center, tint, Vector2.zero);
        var inner = new Rect(rect.x + border, rect.y + border, rect.width - border * 2, rect.height - border * 2);
        for (int corner = 0; corner < 4; corner++)
        {
            for (int step = 0; step <= segments; step++)
            {
                float angle = (corner * 90 + step * 90f / segments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(Corner(rect, radius, corner) + direction * radius, tint, Vector2.zero);
                if (border > 0)
                {
                    float innerRadius = Mathf.Max(0, radius - border);
                    mesh.AddVert(Corner(inner, innerRadius, corner) + direction * innerRadius, tint, Vector2.zero);
                }
            }
        }
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            if (border <= 0) mesh.AddTriangle(start, start + 1 + index, start + 1 + next);
            else
            {
                mesh.AddTriangle(start + index * 2, start + next * 2, start + index * 2 + 1);
                mesh.AddTriangle(start + next * 2, start + next * 2 + 1, start + index * 2 + 1);
            }
        }
    }

    static Vector2 Corner(Rect rect, float radius, int corner) => corner switch
    {
        0 => new Vector2(rect.xMax - radius, rect.yMax - radius),
        1 => new Vector2(rect.xMin + radius, rect.yMax - radius),
        2 => new Vector2(rect.xMin + radius, rect.yMin + radius),
        _ => new Vector2(rect.xMax - radius, rect.yMin + radius)
    };
}
