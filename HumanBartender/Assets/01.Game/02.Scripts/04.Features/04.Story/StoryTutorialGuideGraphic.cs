using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Web-style dimming with live control cutouts, outlines and drag/click arrows.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StoryTutorialGuideGraphic : MaskableGraphic
{
    Rect? source, destination;
    Vector2? arrowFrom, arrowTo;
    bool drag;

    public bool HasSource => source.HasValue;
    public bool HasDestination => destination.HasValue;
    public bool HasDragRoute => drag && arrowFrom.HasValue && arrowTo.HasValue;

    public void SetGuide(Rect? focus, Rect? target, Vector2? from, Vector2? to, bool dragRoute)
    {
        source = focus;
        destination = target;
        arrowFrom = from;
        arrowTo = to;
        drag = dragRoute;
        raycastTarget = false; // Guidance never intercepts the controls underneath.
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect area = rectTransform.rect;
        var xs = new[] { area.xMin, area.xMax, source?.xMin ?? area.xMin, source?.xMax ?? area.xMax,
            destination?.xMin ?? area.xMin, destination?.xMax ?? area.xMax };
        var ys = new[] { area.yMin, area.yMax, source?.yMin ?? area.yMin, source?.yMax ?? area.yMax,
            destination?.yMin ?? area.yMin, destination?.yMax ?? area.yMax };
        Array.Sort(xs);
        Array.Sort(ys);
        var shade = new Color(.008f, .024f, .055f, .57f);
        for (int x = 0; x < xs.Length - 1; x++)
        for (int y = 0; y < ys.Length - 1; y++)
        {
            Rect cell = Rect.MinMaxRect(Mathf.Max(area.xMin, xs[x]), Mathf.Max(area.yMin, ys[y]),
                Mathf.Min(area.xMax, xs[x + 1]), Mathf.Min(area.yMax, ys[y + 1]));
            if (cell.width <= 0 || cell.height <= 0 || source?.Contains(cell.center) == true ||
                destination?.Contains(cell.center) == true) continue;
            StoryUiMesh.Quad(mesh, cell, shade);
        }
        float pulse = .8f + .2f * Mathf.Sin(Time.unscaledTime * 4.5f);
        var mint = new Color(.64f, .98f, 1f, pulse);
        foreach (var focus in new[] { source, destination })
        {
            if (!focus.HasValue) continue;
            Rect box = focus.Value;
            var halo = new Rect(box.x - 4, box.y - 4, box.width + 8, box.height + 8);
            StoryUiMesh.Rounded(mesh, halo, 13, 5, new Color(.3f, .85f, .92f, .12f * pulse));
            StoryUiMesh.Rounded(mesh, box, 10, 2, mint);
        }
        if (!arrowFrom.HasValue || !arrowTo.HasValue) return;
        Vector2 from = arrowFrom.Value, to = arrowTo.Value;
        if (!drag)
        {
            StoryUiMesh.Line(mesh, from, to, 3, mint);
            ArrowHead(mesh, to, to - from, mint);
            return;
        }
        Vector2 bend = new Vector2(Mathf.Min(from.x, to.x) - 65, Mathf.Max(from.y, to.y) + 70);
        Vector2 previous = from;
        for (int i = 1; i <= 64; i++)
        {
            float t = i / 64f;
            Vector2 point = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * bend + t * t * to;
            if ((i + (int)(Time.unscaledTime * 12)) % 7 < 4) StoryUiMesh.Line(mesh, previous, point, 3, mint);
            previous = point;
        }
        ArrowHead(mesh, to, to - bend, mint);
    }

    static void ArrowHead(VertexHelper mesh, Vector2 point, Vector2 direction, Color tint)
    {
        direction.Normalize();
        Vector2 normal = new Vector2(-direction.y, direction.x);
        StoryUiMesh.Line(mesh, point - direction * 12 + normal * 8, point, 3, tint);
        StoryUiMesh.Line(mesh, point - direction * 12 - normal * 8, point, 3, tint);
    }
}
