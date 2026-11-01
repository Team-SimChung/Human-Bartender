using UnityEngine;

/// <summary>선반 물건마다 공유 머티리얼을 바꾸지 않고 아웃라인 상태를 표시한다.</summary>
internal sealed class CraftPrepItemOutline
{
    private const float TransitionDuration = 0.1f;
    private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int IntensityId = Shader.PropertyToID("_OutlineIntensity");
    private static readonly int GlowId = Shader.PropertyToID("_SelectionGlow");
    private static readonly int BoundsId = Shader.PropertyToID("_SpriteBounds");
    private static readonly int UvRectId = Shader.PropertyToID("_SpriteUVRect");
    private MaterialPropertyBlock properties;
    private SpriteRenderer target;
    private Sprite sourceSprite;
    private Sprite displaySprite;
    private Color color;
    private float intensity;
    private float targetIntensity;
    private float glow;
    private float targetGlow;

    public Sprite SourceSprite(SpriteRenderer renderer) => renderer != null && renderer.sprite == displaySprite
        ? sourceSprite : renderer != null ? renderer.sprite : null;

    public void Set(SpriteRenderer renderer, bool selected, bool hovered, Color selectedColor, Color hoverColor)
    {
        if (renderer == null) return;
        PrepareGeometry(renderer);
        // Selection takes precedence, including while the pointer is over the item.
        targetIntensity = selected || hovered ? 1f : 0f;
        if (selected || hovered)
        {
            color = selected ? selectedColor : hoverColor;
            targetGlow = selected ? 1f : 0f;
        }
        Apply();
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (target == null || Mathf.Approximately(intensity, targetIntensity) && Mathf.Approximately(glow, targetGlow)) return;
        float step = unscaledDeltaTime / TransitionDuration;
        intensity = Mathf.MoveTowards(intensity, targetIntensity, step);
        glow = Mathf.MoveTowards(glow, targetGlow, step);
        Apply();
    }

    private void PrepareGeometry(SpriteRenderer renderer)
    {
        if (target == renderer && renderer.sprite == displaySprite && displaySprite != null) return;
        ReleaseSprite();
        target = renderer;
        sourceSprite = renderer.sprite;
        if (sourceSprite == null) return;

        // A private rectangular mesh gives the shader room for an outer glow without editing imported sprites.
        Rect rect = sourceSprite.rect;
        Vector2 pivot = sourceSprite.pivot / rect.size;
        displaySprite = Sprite.Create(sourceSprite.texture, rect, pivot, sourceSprite.pixelsPerUnit,
            0, SpriteMeshType.FullRect, sourceSprite.border, false);
        displaySprite.name = sourceSprite.name + " (Selection)";
        displaySprite.hideFlags = HideFlags.HideAndDontSave;
        Bounds bounds = displaySprite.bounds;
        renderer.sprite = displaySprite;

        properties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetVector(BoundsId, new Vector4(bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y));
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector2 uv in displaySprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
        properties.SetVector(UvRectId, new Vector4(min.x, min.y, max.x, max.y));
        renderer.SetPropertyBlock(properties);
    }

    private void Apply()
    {
        if (target == null) return;
        properties ??= new MaterialPropertyBlock();
        target.GetPropertyBlock(properties);
        properties.SetColor(ColorId, QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
        properties.SetFloat(IntensityId, intensity);
        properties.SetFloat(GlowId, glow);
        target.SetPropertyBlock(properties);
    }

    public void Clear()
    {
        intensity = targetIntensity = glow = targetGlow = 0f;
        Apply();
        ReleaseSprite();
        target = null;
    }

    private void ReleaseSprite()
    {
        if (displaySprite == null) return;
        if (target != null && target.sprite == displaySprite) target.sprite = sourceSprite;
        if (Application.isPlaying) Object.Destroy(displaySprite);
        else Object.DestroyImmediate(displaySprite);
        displaySprite = null;
        sourceSprite = null;
    }
}
