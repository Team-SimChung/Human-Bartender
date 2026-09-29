using System;
using TMPro;
using UnityEngine;

/// <summary>Only writer of animated TMP vertices for one label. Every frame starts from TMP's unmodified mesh.</summary>
public sealed class DialogueTextAnimator : MonoBehaviour
{
    TMP_Text label;
    DialogueTextCompiler.Line line;
    float[] revealedAt;
    Vector3[][] original;
    int visible;
    bool reducedMotion;
    bool hasMotion;
    bool hasContinuous;

    public void Bind(TMP_Text target, DialogueTextCompiler.Line compiled, bool reduce)
    {
        Clear();
        label = target;
        line = compiled;
        reducedMotion = reduce;
        visible = 0;
        revealedAt = new float[compiled.Characters.Length];
        for (int i = 0; i < revealedAt.Length; i++) revealedAt[i] = -1f;
        foreach (var style in compiled.Characters)
        {
            if (style.Shake || style.Wave || style.Pop) hasMotion = true;
            if (style.Shake || style.Wave) hasContinuous = true;
        }
        label.OnPreRenderText += OnPreRenderText;
        Capture(label.textInfo);
    }

    public void SetVisible(int count, float now, bool instant)
    {
        if (line == null) return;
        int next = Mathf.Clamp(count, 0, revealedAt.Length);
        if (!instant)
            for (int i = visible; i < next; i++) revealedAt[i] = now;
        visible = next;
        if (hasMotion && !reducedMotion) Apply(now);
    }

    void OnPreRenderText(TMP_TextInfo info)
    {
        if (line == null) return;
        Capture(info);
        if (hasMotion && !reducedMotion) Apply(Time.time);
    }

    void Capture(TMP_TextInfo info)
    {
        if (info == null) return;
        original = new Vector3[info.meshInfo.Length][];
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            var vertices = info.meshInfo[m].vertices ?? Array.Empty<Vector3>();
            original[m] = new Vector3[vertices.Length];
            Array.Copy(vertices, original[m], vertices.Length);
        }
    }

    void LateUpdate()
    {
        if (hasMotion && line != null && label != null && !reducedMotion &&
            (hasContinuous || HasActivePop(Time.time))) Apply(Time.time);
    }

    bool HasActivePop(float now)
    {
        for (int i = 0; i < visible && i < line.Characters.Length; i++)
            if (line.Characters[i].Pop && revealedAt[i] >= 0f &&
                now - revealedAt[i] < line.Characters[i].PopDuration) return true;
        return false;
    }

    void Apply(float now)
    {
        if (label == null || original == null) return;
        var info = label.textInfo;
        for (int m = 0; m < original.Length && m < info.meshInfo.Length; m++)
        {
            var vertices = info.meshInfo[m].vertices;
            if (vertices == null) continue;
            Array.Copy(original[m], vertices, Mathf.Min(original[m].Length, vertices.Length));
        }
        if (!reducedMotion)
        {
            int count = Mathf.Min(visible, Mathf.Min(info.characterCount, line.Characters.Length));
            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible || ch.elementType == TMP_TextElementType.Sprite) continue;
                int material = ch.materialReferenceIndex, vertex = ch.vertexIndex;
                if (material < 0 || material >= original.Length || vertex < 0 ||
                    vertex + 3 >= original[material].Length) continue;
                var style = line.Characters[i];
                if (!style.Shake && !style.Wave && !style.Pop) continue;
                Vector3[] target = info.meshInfo[material].vertices;
                if (target == null || vertex + 3 >= target.Length) continue;
                Vector3[] source = original[material];
                Vector3 center = (source[vertex] + source[vertex + 2]) * 0.5f;
                float scale = 1f;
                if (style.Pop && revealedAt[i] >= 0f)
                {
                    float age = now - revealedAt[i];
                    if (age >= 0f && age < style.PopDuration)
                    {
                        float t = age / style.PopDuration;
                        float shape = t < 0.35f ? t / 0.35f : (1f - t) / 0.65f;
                        scale += (style.PopPeak - 1f) * shape;
                    }
                }
                Vector3 offset = Vector3.zero;
                if (style.Wave)
                    offset.y += style.WaveAmp * Mathf.Sin((now * style.WaveHz * Mathf.PI * 2f) + i * style.WavePhase);
                if (style.Shake)
                {
                    // Deterministic per glyph. Never consumes UnityEngine.Random's gameplay state.
                    float phase = now * style.ShakeHz * Mathf.PI * 2f + i * 2.39996f;
                    offset.x += style.ShakeAmp * Mathf.Sin(phase);
                    offset.y += style.ShakeAmp * Mathf.Sin(phase * 1.73f);
                }
                for (int corner = 0; corner < 4; corner++)
                    target[vertex + corner] = center + (source[vertex + corner] - center) * scale + offset;
            }
        }
        label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    public void Clear()
    {
        if (label != null)
        {
            label.OnPreRenderText -= OnPreRenderText;
            if (original != null)
            {
                var info = label.textInfo;
                for (int m = 0; m < original.Length && m < info.meshInfo.Length; m++)
                {
                    var target = info.meshInfo[m].vertices;
                    if (target != null) Array.Copy(original[m], target,
                        Mathf.Min(original[m].Length, target.Length));
                }
                label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }
        label = null;
        line = null;
        original = null;
        revealedAt = null;
        visible = 0;
        hasMotion = false;
        hasContinuous = false;
    }

    void OnDisable() => Clear();
    void OnDestroy() => Clear();
}
