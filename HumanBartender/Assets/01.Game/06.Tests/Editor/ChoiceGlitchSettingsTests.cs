using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>선택지 글리치 값이 코드 숫자가 아니라 ChoiceGlitchSettings 에셋에서 오는지 확인한다.</summary>
public sealed class ChoiceGlitchSettingsTests
{
    const string ChoicePrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/ChoicePanel.prefab";

    static T Read<T>(UIChoiceGlitch fx, string property) =>
        (T)typeof(UIChoiceGlitch).GetProperty(property, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fx);

    static void AssertDefaults(float duration, float frameRate, int activeFrames, int restEvery, float jitter, Color32 tintA, Color32 tintB)
    {
        Assert.That(duration, Is.EqualTo(.3f).Within(1e-5f));
        Assert.That(frameRate, Is.EqualTo(60f).Within(1e-5f));
        Assert.That(activeFrames, Is.EqualTo(15));
        Assert.That(restEvery, Is.EqualTo(6));
        Assert.That(jitter, Is.EqualTo(.75f).Within(1e-5f));
        Assert.That(tintA, Is.EqualTo(new Color32(101, 247, 255, 255)));
        Assert.That(tintB, Is.EqualTo(new Color32(230, 166, 214, 255)));
    }

    [Test]
    public void AssetKeepsOriginalTiming()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UIChoiceGlitchSettings>(VisualUISetup.ChoiceGlitchSettingsPath);
        Assert.That(settings, Is.Not.Null, "Create › Scriptable Objects › UI › ChoiceGlitchSettings 로 에셋을 만들어야 합니다.");
        AssertDefaults(settings.Duration, settings.FrameRate, settings.ActiveFrames, settings.RestEvery, settings.Jitter, settings.TintA, settings.TintB);
    }

    [Test]
    public void ChoicePanelUsesSharedSettings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath);
        var linked = new SerializedObject(prefab.GetComponent<UIChoiceGlitch>()).FindProperty("settings").objectReferenceValue;
        Assert.That(AssetDatabase.GetAssetPath(linked), Is.EqualTo(VisualUISetup.ChoiceGlitchSettingsPath),
            "ChoicePanel 프리팹의 UIChoiceGlitch Settings 칸에 공용 에셋이 연결되어야 합니다.");
    }

    [Test]
    public void MissingSettingsFallsBackToDefaults()
    {
        var go = new GameObject("Glitch fallback");
        try
        {
            var fx = go.AddComponent<UIChoiceGlitch>();
            AssertDefaults(Read<float>(fx, "Duration"), Read<float>(fx, "FrameRate"), Read<int>(fx, "ActiveFrames"),
                Read<int>(fx, "RestEvery"), Read<float>(fx, "Jitter"), Read<Color32>(fx, "TintA"), Read<Color32>(fx, "TintB"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void GlitchShaderCompiles()
    {
        var shader = Shader.Find("Custom/TMPGlitch");
        Assert.That(shader, Is.Not.Null, "Custom/TMPGlitch 셰이더를 찾을 수 없습니다.");
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "Custom/TMPGlitch 셰이더에 컴파일 에러가 있습니다.");
    }

    [Test]
    public void ShaderGlitchSwapsOnlyTheHeldChoiceWhileHolding()
    {
        var shader = Shader.Find("Custom/TMPGlitch");
        var template = new Material(shader);
        template.SetFloat("_GlitchRGBSplit", 4f);
        var settings = ScriptableObject.CreateInstance<UIChoiceGlitchSettings>();
        var held = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath));
        var other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePrefabPath));
        var fx = held.GetComponent<UIChoiceGlitch>();
        try
        {
            var data = new SerializedObject(settings);
            data.FindProperty("textGlitchMaterial").objectReferenceValue = template;
            data.ApplyModifiedPropertiesWithoutUndo();
            var link = new SerializedObject(fx);
            link.FindProperty("settings").objectReferenceValue = settings;
            link.ApplyModifiedPropertiesWithoutUndo();
            var label = (TMPro.TMP_Text)link.FindProperty("label").objectReferenceValue;
            var otherLabel = (TMPro.TMP_Text)new SerializedObject(other.GetComponent<UIChoiceGlitch>()).FindProperty("label").objectReferenceValue;
            var original = label.fontSharedMaterial;

            Call(fx, "OnEnable");
            Call(fx, "OnHoldingChanged", true);
            // 0.08초 = 60fps 기준 4번째 프레임: 쉬는 프레임(0, 6, 12…)이 아니라 글리치가 보이는 프레임.
            StartedAgo(fx, .08);
            Call(fx, "LateUpdate");
            var glitch = label.fontSharedMaterial;
            Assert.That(glitch.shader, Is.EqualTo(shader), "누르고 있는 동안 글리치 셰이더로 그려야 합니다.");
            Assert.That(glitch, Is.Not.EqualTo(original), "공유 머티리얼을 직접 바꾸면 다른 선택지까지 지지직거립니다.");
            Assert.That(glitch.mainTexture, Is.EqualTo(original.mainTexture), "폰트 아틀라스는 그대로 써야 합니다.");
            Assert.That(glitch.GetFloat("_GlitchRGBSplit"), Is.EqualTo(4f), "템플릿 머티리얼의 _Glitch 값을 가져와야 합니다.");
            Assert.That(glitch.GetFloat("_GlitchStrength"), Is.EqualTo(1f));

            // 쉬는 프레임에서는 글자 흔들기와 같이 셰이더도 쉰다(타이밍은 ChoiceGlitchSettings 한 곳).
            StartedAgo(fx, .105);
            Call(fx, "LateUpdate");
            Assert.That(label.fontSharedMaterial.GetFloat("_GlitchStrength"), Is.EqualTo(0f), "6번째 프레임은 쉬는 프레임입니다.");
            Assert.That(otherLabel.fontSharedMaterial, Is.EqualTo(original), "누르지 않은 선택지는 원래 머티리얼 그대로입니다.");

            Call(fx, "OnHoldingChanged", false);
            Assert.That(label.fontSharedMaterial, Is.EqualTo(original), "손을 떼면 바로 원래 머티리얼로 돌아와야 합니다.");
        }
        finally
        {
            Call(fx, "OnDisable");
            Call(fx, "OnDestroy");
            Object.DestroyImmediate(held); Object.DestroyImmediate(other);
            Object.DestroyImmediate(settings); Object.DestroyImmediate(template);
        }
    }

    static void StartedAgo(UIChoiceGlitch fx, double seconds) =>
        typeof(UIChoiceGlitch).GetField("startedAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(fx, Time.unscaledTimeAsDouble - seconds);

    static void Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args.Length == 0 ? null : args);

    [Test]
    public void LinkedSettingsDriveTheEffect()
    {
        var go = new GameObject("Glitch linked");
        var settings = ScriptableObject.CreateInstance<UIChoiceGlitchSettings>();
        try
        {
            var data = new SerializedObject(settings);
            data.FindProperty("duration").floatValue = .5f;
            data.FindProperty("jitter").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var fx = go.AddComponent<UIChoiceGlitch>();
            var link = new SerializedObject(fx);
            link.FindProperty("settings").objectReferenceValue = settings;
            link.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Read<float>(fx, "Duration"), Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(Read<float>(fx, "Jitter"), Is.EqualTo(3f).Within(1e-5f));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(settings); }
    }
}
