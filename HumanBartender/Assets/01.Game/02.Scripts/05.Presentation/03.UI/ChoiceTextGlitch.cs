using System;
using TMPro;
using UnityEngine;

/// <summary>선택지의 입력·배경·문구와 분리된 TMP 메시 및 셰이더 글리치다.</summary>
internal sealed class ChoiceTextGlitch : IDisposable
{
    private readonly TMPGlitchMaterial materialGlitch = new TMPGlitchMaterial();
    private TMP_Text label;
    private UIChoiceGlitchSettings settings;
    private double startedAt;
    private int frame;
    private bool activeFrame;

    public bool IsPlaying { get; private set; }

    /// <summary>한 번 재생한다. 같은 문구에 다시 호출하면 원본으로 되돌린 뒤 처음부터 재생한다.</summary>
    public void Play(TMP_Text target, UIChoiceGlitchSettings configuration)
    {
        Stop();
        startedAt = Time.unscaledTimeAsDouble;
        Sample(target, configuration, 0);
    }

    public void Tick()
    {
        if (!IsPlaying) return;
        if (label == null || !label.isActiveAndEnabled)
        {
            Stop();
            return;
        }
        double elapsed = Time.unscaledTimeAsDouble - startedAt;
        if (elapsed >= GetDuration(settings))
        {
            Stop();
            return;
        }
        Sample(label, settings, elapsed);
    }

    /// <summary>기존 홀드·포커스 제어가 소유한 경과 시간으로 같은 효과를 재생한다.</summary>
    public void Sample(TMP_Text target, UIChoiceGlitchSettings configuration, double elapsed)
    {
        if (target == null)
        {
            Stop();
            return;
        }
        if (label != target)
        {
            Stop();
            label = target;
            label.OnPreRenderText += Distort;
        }
        settings = configuration;
        IsPlaying = true;
        float frameRate = settings != null ? settings.FrameRate : UIChoiceGlitchSettings.DefaultFrameRate;
        int activeFrames = settings != null ? settings.ActiveFrames : UIChoiceGlitchSettings.DefaultActiveFrames;
        int restEvery = settings != null ? settings.RestEvery : UIChoiceGlitchSettings.DefaultRestEvery;
        frame = Mathf.FloorToInt((float)(elapsed % GetDuration(settings)) * frameRate);
        int pattern = Mathf.FloorToInt((float)elapsed * frameRate);
        activeFrame = !((restEvery > 0 && frame % restEvery == 0) || frame > activeFrames);
        Material template = settings != null ? settings.TextGlitchMaterial : null;
        if (template != null) materialGlitch.Apply(label, template, activeFrame, pattern);
        else materialGlitch.Release(label);
        // TMP가 원본 정점을 만든 뒤 변형하므로 이전 프레임의 이동량이 누적되지 않는다.
        label.ForceMeshUpdate();
    }

    public void Stop()
    {
        IsPlaying = false;
        activeFrame = false;
        if (label != null)
        {
            label.OnPreRenderText -= Distort;
            materialGlitch.Release(label);
            label.ForceMeshUpdate(true);
        }
        label = null;
        settings = null;
    }

    public void Dispose()
    {
        Stop();
        materialGlitch.Dispose();
    }

    internal static float GetDuration(UIChoiceGlitchSettings configuration)
    {
        return Mathf.Max(configuration != null ? configuration.Duration : UIChoiceGlitchSettings.DefaultDuration, .01f);
    }

    private void Distort(TMP_TextInfo info)
    {
        if (!IsPlaying || !activeFrame) return;
        float jitter = settings != null ? settings.Jitter : UIChoiceGlitchSettings.DefaultJitter;
        Color32 tintA = settings != null ? settings.TintA : UIChoiceGlitchSettings.DefaultTintA;
        Color32 tintB = settings != null ? settings.TintB : UIChoiceGlitchSettings.DefaultTintB;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;
            TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
            float offset = ((i * 7 + frame * 3) % 5 - 2) * jitter;
            Color32 tint = (i + frame) % 3 == 0 ? tintA : tintB;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int index = character.vertexIndex + vertex;
                mesh.vertices[index].x += offset;
                if ((i + frame) % 3 == 1) continue;
                tint.a = mesh.colors32[index].a;
                mesh.colors32[index] = tint;
            }
        }
    }
}

/// <summary>원래 폰트 재질의 사본에 글리치 셰이더를 적용해 다른 선택지의 공유 재질을 보존한다.</summary>
internal sealed class TMPGlitchMaterial : IDisposable
{
    private const string Prefix = "_Glitch";
    private static readonly int StrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int FrameId = Shader.PropertyToID("_GlitchFrame");
    private Material copy;
    private Material source;
    private Material sourceTemplate;

    public void Apply(TMP_Text label, Material template, bool active, int pattern)
    {
        Material current = label.fontSharedMaterial;
        if (current == null) return;
        // 폰트나 설정이 바뀌면 새 원본에서 사본을 만든다. 공유 폰트 재질은 직접 변경하지 않는다.
        if (copy == null || (current != copy && source != current) || sourceTemplate != template)
        {
            Material original = current == copy ? source : current;
            Release(label);
            Create(original, template);
        }
        if (label.fontSharedMaterial != copy) label.fontSharedMaterial = copy;
        copy.SetFloat(StrengthId, active ? 1f : 0f);
        copy.SetFloat(FrameId, pattern);
    }

    public void Release(TMP_Text label)
    {
        if (label != null && copy != null && label.fontSharedMaterial == copy) label.fontSharedMaterial = source;
    }

    public void Dispose()
    {
        if (copy != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
            else UnityEngine.Object.DestroyImmediate(copy);
        }
        copy = null;
        source = null;
        sourceTemplate = null;
    }

    private void Create(Material current, Material template)
    {
        Dispose();
        copy = new Material(current) { name = current.name + " (Glitch)", hideFlags = HideFlags.HideAndDontSave };
        copy.shader = template.shader;
        Shader shader = template.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string property = shader.GetPropertyName(i);
            if (!property.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    copy.SetFloat(property, template.GetFloat(property));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    copy.SetColor(property, template.GetColor(property));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    copy.SetVector(property, template.GetVector(property));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Int:
                    copy.SetInteger(property, template.GetInteger(property));
                    break;
            }
        }
        source = current;
        sourceTemplate = template;
    }
}
