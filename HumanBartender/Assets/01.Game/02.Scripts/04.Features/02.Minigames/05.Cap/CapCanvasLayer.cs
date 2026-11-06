using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2D 캔버스 변환 행렬. HTML 캔버스의 setTransform(a, b, c, d, e, f)과 같은 뜻이다 —
/// x' = a·x + c·y + e, y' = b·x + d·y + f. 좌표는 웹 캔버스(1280×720, y가 아래로 증가) 기준이다.
///
/// 웹 연출을 수식 그대로 옮기려고 둔다. 뚜껑이 빗나갈 때의 기울임(skew)은 Transform으로 만들 수 없어서,
/// 정점을 직접 이 행렬로 옮긴다.
/// </summary>
public struct CapAffine2D
{
    public float a, b, c, d, e, f;

    public static CapAffine2D Identity => new CapAffine2D { a = 1f, d = 1f };

    public CapAffine2D Multiply(float na, float nb, float nc, float nd, float ne, float nf) => new CapAffine2D
    {
        a = a * na + c * nb,
        b = b * na + d * nb,
        c = a * nc + c * nd,
        d = b * nc + d * nd,
        e = a * ne + c * nf + e,
        f = b * ne + d * nf + f,
    };

    public CapAffine2D Translate(float x, float y) => Multiply(1f, 0f, 0f, 1f, x, y);
    public CapAffine2D Scale(float x, float y) => Multiply(x, 0f, 0f, y, 0f, 0f);

    /// <summary>캔버스 rotate와 같다. 화면(y 아래)에서 양수가 시계 방향이다.</summary>
    public CapAffine2D Rotate(float radians)
    {
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        return Multiply(cos, sin, -sin, cos, 0f, 0f);
    }

    public Vector2 Apply(float x, float y) => new Vector2(a * x + c * y + e, b * x + d * y + f);
}

/// <summary>연출 메시를 편집 가능한 RectTransform 안에 그린다. 위치와 크기는 프리팹 값을 유지한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CapCanvasLayer : MaskableGraphic
{
    [SerializeField] Texture texture;
    [SerializeField] Vector2 drawingOrigin;
    [SerializeField] Vector2 referenceSize = new Vector2(1280f, 720f);
    public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

    readonly List<Vector3> vertices = new();
    readonly List<Vector2> uvs = new();
    readonly List<Color32> colors = new();
    readonly List<int> triangles = new();

    public void SetTexture(Texture source)
    {
        if (texture == source) return;
        texture = source;
        SetMaterialDirty();
    }

    public void Clear()
    {
        vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
    }

    public void Apply() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 point = vertices[i];
            point.x = rect.xMin + (point.x - drawingOrigin.x) * rect.width / Mathf.Max(.001f, referenceSize.x);
            point.y = rect.yMax - (point.y - drawingOrigin.y) * rect.height / Mathf.Max(.001f, referenceSize.y);
            helper.AddVert(point, colors[i] * color, uvs[i]);
        }
        for (int i = 0; i < triangles.Count; i += 3)
            helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    /// <summary>
    /// 캔버스 drawImage(image, sx, sy, sw, sh, dx, dy, dw, dh). 원본 사각형은 이미지 왼쪽 위 기준 픽셀이다.
    /// </summary>
    public void Image(CapAffine2D m, float sx, float sy, float sw, float sh,
                      float dx, float dy, float dw, float dh, float alpha = 1f)
    {
        float tw = mainTexture.width, th = mainTexture.height;
        // 텍스처는 아래에서 위로 저장되므로 원본의 y를 뒤집어 읽는다.
        float u0 = sx / tw, u1 = (sx + sw) / tw;
        float v0 = 1f - sy / th, v1 = 1f - (sy + sh) / th;
        Quad(m, dx, dy, dw, dh, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1),
             new Color(1f, 1f, 1f, alpha));
    }

    /// <summary>텍스처 전체를 사각형에 그린다. 탄산·할로처럼 미리 만든 그림에 쓴다.</summary>
    public void DrawTexture(CapAffine2D m, float dx, float dy, float dw, float dh, Color tint)
    {
        Quad(m, dx, dy, dw, dh, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), tint);
    }

    public void FillRect(CapAffine2D m, float x, float y, float w, float h, Color color)
    {
        Vector2 uv = new Vector2(.5f, .5f);
        Quad(m, x, y, w, h, uv, uv, uv, uv, color);
    }

    /// <summary>열린 꺾은선을 굵기 width로 긋는다. 이음매는 선분을 겹쳐 메운다.</summary>
    public void StrokePolyline(CapAffine2D m, IReadOnlyList<Vector2> points, float width, Color color, bool closed = false)
    {
        int count = points.Count;
        int segments = closed ? count : count - 1;
        for (int i = 0; i < segments; i++)
            Segment(m, points[i], points[(i + 1) % count], width, color);
    }

    readonly List<Vector2> circle = new();

    /// <summary>캔버스 arc(cx, cy, r, 0, 2π) + stroke.</summary>
    public void StrokeCircle(CapAffine2D m, float cx, float cy, float radius, float width, Color color, int segments = 96)
    {
        if (radius <= 0f) return;
        circle.Clear();
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            circle.Add(new Vector2(cx + Mathf.Cos(t) * radius, cy + Mathf.Sin(t) * radius));
        }
        StrokePolyline(m, circle, width, color, true);
    }

    /// <summary>캔버스 ellipse + fill. 회전 없는 타원이다.</summary>
    public void FillEllipse(CapAffine2D m, float cx, float cy, float rx, float ry, Color color, int segments = 48)
    {
        int center = vertices.Count;
        AddVertex(m.Apply(cx, cy), new Vector2(.5f, .5f), color);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            AddVertex(m.Apply(cx + Mathf.Cos(t) * rx, cy + Mathf.Sin(t) * ry), new Vector2(.5f, .5f), color);
            if (i > 0) { triangles.Add(center); triangles.Add(center + i); triangles.Add(center + i + 1); }
        }
    }

    readonly List<Vector2> ellipse = new();

    public void StrokeEllipse(CapAffine2D m, float cx, float cy, float rx, float ry, float width, Color color, int segments = 48)
    {
        ellipse.Clear();
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            ellipse.Add(new Vector2(cx + Mathf.Cos(t) * rx, cy + Mathf.Sin(t) * ry));
        }
        StrokePolyline(m, ellipse, width, color, true);
    }

    void Segment(CapAffine2D m, Vector2 from, Vector2 to, float width, Color color)
    {
        // 굵기는 변환 전 좌표에서 잡는다. 캔버스 lineWidth도 현재 변환을 따라 늘어난다.
        Vector2 dir = to - from;
        float length = dir.magnitude;
        if (length < 1e-5f) return;
        dir /= length;
        // 이음매가 벌어지지 않도록 양 끝을 굵기의 절반만큼 늘린다(사각 끝 처리).
        Vector2 extend = dir * (width * .5f);
        Vector2 normal = new Vector2(-dir.y, dir.x) * (width * .5f);
        Vector2 a = from - extend, b = to + extend;
        Vector2 uv = new Vector2(.5f, .5f);
        int start = vertices.Count;
        AddVertex(m.Apply(a.x + normal.x, a.y + normal.y), uv, color);
        AddVertex(m.Apply(b.x + normal.x, b.y + normal.y), uv, color);
        AddVertex(m.Apply(b.x - normal.x, b.y - normal.y), uv, color);
        AddVertex(m.Apply(a.x - normal.x, a.y - normal.y), uv, color);
        AddQuadTriangles(start);
    }

    void Quad(CapAffine2D m, float x, float y, float w, float h,
              Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight, Vector2 uvBottomLeft, Color color)
    {
        int start = vertices.Count;
        AddVertex(m.Apply(x, y), uvTopLeft, color);
        AddVertex(m.Apply(x + w, y), uvTopRight, color);
        AddVertex(m.Apply(x + w, y + h), uvBottomRight, color);
        AddVertex(m.Apply(x, y + h), uvBottomLeft, color);
        AddQuadTriangles(start);
    }

    void AddVertex(Vector2 position, Vector2 uv, Color color)
    {
        vertices.Add(new Vector3(position.x, position.y, 0f));
        uvs.Add(uv);
        colors.Add(color);
    }

    void AddQuadTriangles(int start)
    {
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

}
