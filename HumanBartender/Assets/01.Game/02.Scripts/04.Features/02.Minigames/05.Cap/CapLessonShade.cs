using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>편집한 대상 영역을 따라 구멍만 갱신하는 안내 가림막. 대상의 배치를 변경하지 않는다.</summary>
[ExecuteAlways]
public sealed class CapLessonShade : MaskableGraphic
{
    [SerializeField] RectTransform focus;
    [SerializeField] RectTransform[] buttons;
    [SerializeField] Vector2 padding = new Vector2(12f, 12f);
    readonly List<Rect> holes = new();
    readonly List<float> xs = new(), ys = new();
    readonly Vector3[] corners = new Vector3[4];

    public void SetFocus(RectTransform target)
    {
        if (focus == target) return;
        focus = target;
        SetVerticesDirty();
    }

    // 안내가 켜진 동안에만, 옮긴 대상과 해상도에 맞춰 구멍을 다시 그린다.
    void LateUpdate() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        holes.Clear(); xs.Clear(); ys.Clear();
        Rect area = rectTransform.rect;
        xs.Add(area.xMin); xs.Add(area.xMax);
        ys.Add(area.yMin); ys.Add(area.yMax);
        AddHole(focus, area);
        if (buttons != null) foreach (var button in buttons) AddHole(button, area);
        xs.Sort(); ys.Sort();
        for (int x = 0; x + 1 < xs.Count; x++)
            for (int y = 0; y + 1 < ys.Count; y++)
            {
                var rect = Rect.MinMaxRect(xs[x], ys[y], xs[x + 1], ys[y + 1]);
                if (rect.width <= 0f || rect.height <= 0f || holes.Exists(hole => hole.Contains(rect.center))) continue;
                int i = helper.currentVertCount;
                helper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
                helper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
                helper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
                helper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
                helper.AddTriangle(i, i + 1, i + 2);
                helper.AddTriangle(i, i + 2, i + 3);
            }
    }

    void AddHole(RectTransform target, Rect area)
    {
        if (target == null) return;
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 local = rectTransform.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        var hole = Rect.MinMaxRect(Mathf.Clamp(min.x - padding.x, area.xMin, area.xMax),
            Mathf.Clamp(min.y - padding.y, area.yMin, area.yMax),
            Mathf.Clamp(max.x + padding.x, area.xMin, area.xMax),
            Mathf.Clamp(max.y + padding.y, area.yMin, area.yMax));
        holes.Add(hole);
        xs.Add(hole.xMin); xs.Add(hole.xMax);
        ys.Add(hole.yMin); ys.Add(hole.yMax);
    }
}
