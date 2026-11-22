using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>인물의 파츠와 통짜 초상에 같은 투명도 전환을 적용한다. 원래 RGB 색은 유지한다.</summary>
public sealed class CharacterFade
{
    public const float Duration = .35f;
    readonly SpriteRenderer[] renderers;
    CancellationTokenSource transition;

    public CharacterFade(IEnumerable<SpriteRenderer> sprites)
    {
        var unique = new HashSet<SpriteRenderer>();
        foreach (var sprite in sprites) if (sprite != null) unique.Add(sprite);
        renderers = new SpriteRenderer[unique.Count];
        unique.CopyTo(renderers);
    }

    public static CharacterFade ForRig(SlotCharacterPart rig)
    {
        var sprites = new List<SpriteRenderer>();
        if (rig.parts != null) foreach (var part in rig.parts) if (part != null) sprites.Add(part.Renderer);
        sprites.Add(rig.portaitSpriteRenderer);
        return new CharacterFade(sprites);
    }

    public void SetOpacity(float opacity)
    {
        transition?.Cancel();
        transition = null;
        foreach (var renderer in renderers) SetAlpha(renderer, opacity);
    }

    public async UniTask ToAsync(float opacity, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        transition?.Cancel();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token);
        transition = source;
        var from = new float[renderers.Length];
        bool visible = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            from[i] = renderer.color.a;
            visible |= renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null;
        }
        try
        {
            float began = Time.unscaledTime;
            while (visible)
            {
                source.Token.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01((Time.unscaledTime - began) / Duration);
                for (int i = 0; i < renderers.Length; i++) SetAlpha(renderers[i], Mathf.Lerp(from[i], opacity, progress));
                if (progress >= 1) return;
                await UniTask.NextFrame(PlayerLoopTiming.Update, source.Token);
            }
            foreach (var renderer in renderers) SetAlpha(renderer, opacity);
        }
        finally { if (transition == source) transition = null; }
    }

    static void SetAlpha(SpriteRenderer renderer, float opacity)
    {
        if (renderer == null) return;
        var tint = renderer.color;
        tint.a = opacity;
        renderer.color = tint;
    }
}
