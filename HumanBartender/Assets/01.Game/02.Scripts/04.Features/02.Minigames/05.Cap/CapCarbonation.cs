using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 병을 딴 뒤 병 안에서 올라오는 탄산 기포. 웹 본편 open-carbonation.js를 그대로 옮겼다.
///
/// 흰 거품이 아니라 속이 빈 호박색 기포 한 무리가 한 번 올라온다. 좌표는 병 원본 픽셀(뚜껑 중심이
/// 원점, y가 아래로 증가)이고, 화면에서는 4.2배로 커진다. 15fps로 끊어 움직이는 것도 웹과 같다 —
/// 원본 픽셀 그림과 같은 거친 움직임을 내려는 의도라서 부드럽게 바꾸지 않는다.
/// </summary>
public static class CapCarbonation
{
    public const float Duration = 2.05f;
    public const int ParticleCount = 44;

    /// <summary>기포가 들어갈 수 있는 병 안쪽 윤곽(병 원본 픽셀).</summary>
    public static readonly Vector2[] Outline =
    {
        new(-8, 5), new(9, 5), new(9, 53), new(17, 69), new(20, 83), new(20, 151),
        new(-19, 151), new(-19, 83), new(-16, 70), new(-8, 53),
    };

    /// <summary>병 몸통의 라벨. 불투명한 종이라 이 안에는 기포를 그리지 않는다.</summary>
    public static readonly Vector2[] Label = { new(-21, 89), new(21, 85), new(21, 160), new(-21, 160) };

    /// <summary>기포 고리의 팔각형 꼭짓점(반지름 1 기준). 원본 픽셀 그림처럼 살짝 찌그러져 있다.</summary>
    public static readonly Vector2[] Ring =
    {
        new(-.55f, -.72f), new(.5f, -.72f), new(1f, -.2f), new(.86f, .46f),
        new(.36f, .8f), new(-.48f, .72f), new(-1f, .22f), new(-.9f, -.35f),
    };

    public readonly struct Particle
    {
        public readonly float X, Y, Radius, Alpha;
        public readonly int Index;
        public Particle(float x, float y, float radius, float alpha, int index)
        {
            X = x; Y = y; Radius = radius; Alpha = alpha; Index = index;
        }
    }

    /// <summary>웹의 의사난수. 같은 n이면 언제나 같은 값이라 매번 같은 기포 무리가 나온다.</summary>
    public static float Random(float n)
    {
        // 웹은 double로 계산한다. float로 하면 큰 값의 소수부가 달라져 기포 배치가 바뀐다.
        double v = System.Math.Sin(n * 127.1 + 63.7) * 43758.5453;
        return (float)(v - System.Math.Floor(v));
    }

    static float Width(float y) => y < 53f ? 8f : y < 83f ? 8f + (y - 53f) * .36f : 19f;

    /// <summary>이 나이(초)에 보이는 기포들. 재생 시간 밖이면 비어 있다.</summary>
    public static void Sample(float age, List<Particle> particles)
    {
        particles.Clear();
        if (age < 0f || age >= Duration) return;
        float t = Mathf.Floor(age * 15f) / 15f;
        for (int i = 0; i < ParticleCount; i++)
        {
            float birth = .06f + Random(i + 2) * .52f, life = .91f + Random(i + 38) * .28f, u = (t - birth) / life;
            if (u <= 0f || u >= 1f) continue;
            float y = 109f - u * 76f;
            float lane = (Random(i + 71) * 2f - 1f) * .85f;
            float x = lane * (Width(y) - 2f) + Mathf.Sin(u * 5f + i) * .55f;
            float alpha = Mathf.Clamp01(u * 7f) * Mathf.Clamp01((1f - u) * 5f) * (.24f + Random(i + 13) * .22f);
            particles.Add(new Particle(x, y, .7f + Random(i + 107) * 1.05f, alpha, i));
        }
    }

    /// <summary>점이 기포를 그려도 되는 자리인지. 병 윤곽 안이고 라벨 밖이어야 한다.</summary>
    public static bool InsideClip(float x, float y) => Contains(Outline, x, y) && !Contains(Label, x, y);

    static bool Contains(Vector2[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}

/// <summary>
/// 탄산 기포를 화면 해상도 텍스처에 직접 그린다. 웹 캔버스가 4.2배 확대한 상태에서 선을 긋는 것과
/// 같은 굵기·위치가 나오도록, 텍스처 한 칸을 화면 한 픽셀로 잡는다.
/// </summary>
public sealed class CapCarbonationCanvas
{
    /// <summary>화면에서 병 원본 픽셀 하나가 차지하는 크기. open-view.js의 scale과 같다.</summary>
    public const float Scale = 4.2f;
    /// <summary>텍스처가 덮는 병 원본 좌표 범위.</summary>
    public const float Left = -22f, Top = 0f, Right = 22f, Bottom = 152f;

    static readonly Color32 Stroke = new(0xE2, 0xBB, 0x75, 0xFF);
    static readonly Color32 Highlight = new(0xF6, 0xDD, 0xA1, 0xFF);
    const float LineWidth = .32f;

    readonly int width, height;
    readonly Color[] pixels;
    readonly bool[] clip;
    readonly List<CapCarbonation.Particle> particles = new();
    int drawnFrame = int.MinValue;

    public Texture2D Texture { get; }

    public CapCarbonationCanvas()
    {
        width = Mathf.CeilToInt((Right - Left) * Scale);
        height = Mathf.CeilToInt((Bottom - Top) * Scale);
        pixels = new Color[width * height];
        clip = new bool[width * height];
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                Vector2 p = ToBottle(col + .5f, row + .5f);
                clip[row * width + col] = CapCarbonation.InsideClip(p.x, p.y);
            }

        Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "CapCarbonation",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Clear();
        Texture.SetPixels(pixels);
        Texture.Apply(false);
    }

    /// <summary>화면에서 텍스처가 차지하는 사각형(병 원본 좌표, 왼쪽 위 기준).</summary>
    public static Rect BottleRect => new Rect(Left, Top, Right - Left, Bottom - Top);

    /// <summary>텍스처 행(row 0 = 맨 위)과 열의 중심을 병 원본 좌표로 바꾼다.</summary>
    static Vector2 ToBottle(float col, float row) => new Vector2(Left + col / Scale, Top + row / Scale);

    /// <summary>이 나이에 맞게 다시 그린다. 15fps로 끊기 때문에 칸이 바뀔 때만 실제로 그린다. 그린 기포 수를 돌려준다.</summary>
    public int Draw(float age)
    {
        int frame = age < 0f || age >= CapCarbonation.Duration ? -1 : Mathf.FloorToInt(age * 15f);
        if (frame == drawnFrame) return particles.Count;
        drawnFrame = frame;

        CapCarbonation.Sample(age, particles);
        Clear();
        foreach (var particle in particles) DrawParticle(particle);
        Texture.SetPixels(pixels);
        Texture.Apply(false);
        return particles.Count;
    }

    void Clear()
    {
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
    }

    void DrawParticle(CapCarbonation.Particle p)
    {
        // 웹과 같이 반 픽셀 단위로 맞춘다.
        float x = Mathf.Round(p.X * 2f) / 2f, y = Mathf.Round(p.Y * 2f) / 2f, r = p.Radius;
        var ring = CapCarbonation.Ring;

        float half = LineWidth * .5f * Scale;
        float minX = x - r - 1f, maxX = x + r + 1f, minY = y - r - 1f, maxY = y + r + 1f;
        ForEachPixel(minX, minY, maxX, maxY, (index, bx, by) =>
        {
            float distance = float.MaxValue;
            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 a = new(x + ring[i].x * r, y + ring[i].y * r);
                Vector2 b = new(x + ring[(i + 1) % ring.Length].x * r, y + ring[(i + 1) % ring.Length].y * r);
                distance = Mathf.Min(distance, DistanceToSegment(new Vector2(bx, by), a, b));
            }
            // 화면 픽셀 기준 안티에일리어싱: 선 중심에서 반 굵기 + 반 픽셀까지 덮는다.
            float coverage = Mathf.Clamp01(half + .5f - distance * Scale);
            if (coverage > 0f) Blend(index, Stroke, p.Alpha * coverage);
        });

        // 기포 왼쪽 위의 밝은 반사. 웹: fillRect(x − .45r, y − .72r, .7r, .25)
        float hx = x - r * .45f, hy = y - r * .72f, hw = r * .7f, hh = .25f;
        ForEachPixel(hx, hy, hx + hw, hy + hh, (index, bx, by) =>
        {
            float cover = Overlap(bx, hx, hx + hw) * Overlap(by, hy, hy + hh);
            if (cover > 0f) Blend(index, Highlight, p.Alpha * .6f * cover);
        });
    }

    /// <summary>화면 픽셀 하나(병 좌표로 1/Scale 너비)가 구간과 겹치는 비율.</summary>
    static float Overlap(float center, float from, float to)
    {
        float halfPixel = .5f / Scale;
        float overlap = Mathf.Min(center + halfPixel, to) - Mathf.Max(center - halfPixel, from);
        return Mathf.Clamp01(overlap * Scale);
    }

    void ForEachPixel(float minX, float minY, float maxX, float maxY, System.Action<int, float, float> action)
    {
        int c0 = Mathf.Max(0, Mathf.FloorToInt((minX - Left) * Scale)), c1 = Mathf.Min(width - 1, Mathf.CeilToInt((maxX - Left) * Scale));
        int r0 = Mathf.Max(0, Mathf.FloorToInt((minY - Top) * Scale)), r1 = Mathf.Min(height - 1, Mathf.CeilToInt((maxY - Top) * Scale));
        for (int row = r0; row <= r1; row++)
            for (int col = c0; col <= c1; col++)
            {
                int textureRow = height - 1 - row;
                int index = textureRow * width + col;
                if (!clip[row * width + col]) continue;
                Vector2 p = ToBottle(col + .5f, row + .5f);
                action(index, p.x, p.y);
            }
    }

    void Blend(int index, Color32 color, float alpha)
    {
        if (alpha <= 0f) return;
        Color dst = pixels[index];
        float outA = alpha + dst.a * (1f - alpha);
        if (outA <= 0f) return;
        Color src = color;
        pixels[index] = new Color(
            (src.r * alpha + dst.r * dst.a * (1f - alpha)) / outA,
            (src.g * alpha + dst.g * dst.a * (1f - alpha)) / outA,
            (src.b * alpha + dst.b * dst.a * (1f - alpha)) / outA,
            outA);
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    public void Dispose()
    {
        if (Texture != null) Object.Destroy(Texture);
    }
}
