using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Web-style dimming with live control cutouts, outlines and drag/click arrows.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StoryTutorialGuideGraphic : MaskableGraphic
{
    Rect? source, destination;
    // 딤을 뚫는 자리. 테두리는 화면 안으로 당겨 그려도 구멍은 대상 전체를 비운다. 비우면 테두리 사각형과 같다.
    Rect? sourceHole, destinationHole;
    Vector2? arrowFrom, arrowTo;
    bool drag;
    // 끌기 화살표 곡선의 두 조절점과 점선 모양. 값은 StoryTutorialView 인스펙터가 갖는다.
    Vector2 routeFromBend, routeToBend;
    Vector2 routeDash = new Vector2(12, 9);
    float routeDashSpeed = 30;

    public bool HasSource => source.HasValue;
    public bool HasDestination => destination.HasValue;
    public bool HasDragRoute => drag && arrowFrom.HasValue && arrowTo.HasValue;
    public Vector2? RouteFrom => arrowFrom;
    public Vector2? RouteTo => arrowTo;

    /// <summary>끌기 화살표(3차 곡선)의 조절점과 점선 길이·빈칸·흐르는 속도(초당 px).</summary>
    public void SetRoute(Vector2 fromBend, Vector2 toBend, Vector2 dash, float dashSpeed)
    {
        routeFromBend = fromBend;
        routeToBend = toBend;
        routeDash = dash;
        routeDashSpeed = dashSpeed;
    }

    public void SetArrow(Vector2 from, Vector2 to) => SetGuide(null, null, from, to, false);

    /// <param name="focusHole">focus 자리의 딤 구멍. 비우면 focus와 같다.</param>
    /// <param name="targetHole">target 자리의 딤 구멍. 비우면 target과 같다.</param>
    public void SetGuide(Rect? focus, Rect? target, Vector2? from, Vector2? to, bool dragRoute,
        Rect? focusHole = null, Rect? targetHole = null)
    {
        source = focus;
        destination = target;
        sourceHole = focus.HasValue ? focusHole ?? focus : null;
        destinationHole = target.HasValue ? targetHole ?? target : null;
        arrowFrom = from;
        arrowTo = to;
        drag = dragRoute;
        raycastTarget = false; // Guidance never intercepts the controls underneath.
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (sourceHole.HasValue || destinationHole.HasValue) DrawShade(mesh);
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
        // 웹 .day0-drag-route: 3차 곡선 점선(기본 12 그림·9 빈칸)이 놓을 자리 쪽으로 흐르고, 끝에 화살촉. 밑에 옅은 후광을 깐다.
        var glow = new Color(.27f, .85f, .91f, .18f * pulse);
        float period = Mathf.Max(1, routeDash.x + routeDash.y);
        float offset = Mathf.Repeat(Time.unscaledTime * routeDashSpeed, period);
        for (int pass = 0; pass < 2; pass++)
        {
            float width = pass == 0 ? 7 : 3;
            Color tint = pass == 0 ? glow : mint;
            Vector2 previous = from;
            float travelled = 0;
            const int steps = 240;
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Bezier(from, routeFromBend, routeToBend, to, i / (float)steps);
                float length = Vector2.Distance(previous, point);
                // 선분 가운데가 그림 칸에 들면 그린다. 시간에 따라 offset만큼 빼서 무늬가 끝점 쪽으로 움직인다.
                if (Mathf.Repeat(travelled + length * .5f - offset, period) < routeDash.x)
                    StoryUiMesh.Line(mesh, previous, point, width, tint);
                travelled += length;
                previous = point;
            }
            ArrowHead(mesh, to, to - routeToBend, tint, width);
        }
    }

    void DrawShade(VertexHelper mesh)
    {
        Rect area = rectTransform.rect;
        var xs = new[] { area.xMin, area.xMax, sourceHole?.xMin ?? area.xMin, sourceHole?.xMax ?? area.xMax,
            destinationHole?.xMin ?? area.xMin, destinationHole?.xMax ?? area.xMax };
        var ys = new[] { area.yMin, area.yMax, sourceHole?.yMin ?? area.yMin, sourceHole?.yMax ?? area.yMax,
            destinationHole?.yMin ?? area.yMin, destinationHole?.yMax ?? area.yMax };
        Array.Sort(xs);
        Array.Sort(ys);
        var shade = new Color(.008f, .024f, .055f, .57f);
        for (int x = 0; x < xs.Length - 1; x++)
        for (int y = 0; y < ys.Length - 1; y++)
        {
            Rect cell = Rect.MinMaxRect(Mathf.Max(area.xMin, xs[x]), Mathf.Max(area.yMin, ys[y]),
                Mathf.Min(area.xMax, xs[x + 1]), Mathf.Min(area.yMax, ys[y + 1]));
            if (cell.width <= 0 || cell.height <= 0 || sourceHole?.Contains(cell.center) == true ||
                destinationHole?.Contains(cell.center) == true) continue;
            StoryUiMesh.Quad(mesh, cell, shade);
        }
    }

    internal static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }

    static void ArrowHead(VertexHelper mesh, Vector2 point, Vector2 direction, Color tint, float width = 3)
    {
        if (direction.sqrMagnitude < .001f) return;
        direction.Normalize();
        Vector2 normal = new Vector2(-direction.y, direction.x);
        StoryUiMesh.Line(mesh, point - direction * 12 + normal * 8, point, width, tint);
        StoryUiMesh.Line(mesh, point - direction * 12 - normal * 8, point, width, tint);
    }
}
