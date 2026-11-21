using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 삼호 취함 표정은 부위 애니메이션이다. 취한 전신 4프레임(몸통)과 입 모양을 쓰고, 몸통 그림에 얼굴이 있어
/// 기본 눈·눈썹·윗얼굴은 끈다. 프레임은 기본 자세와 같은 칸 기준점을 써서 위치가 기본 자세와 맞는다.
/// 표정을 못 불러오면 삼호가 대화 중간에 사라지던 문제(2일차 바)의 회귀 방지다.
/// </summary>
public sealed class SamhoDrunkExpressionTests
{
    const string Root = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/01.Bar/02.Samho/";
    const string Clips = "Assets/01.Game/03.Content/07.Animations/01.Characters/03.Samho/01.Clips/05.Drunk/";
    const float CanvasWidth = 551f;

    static Vector2 CanvasPivot(Sprite sprite) =>
        new Vector2(sprite.rect.x % CanvasWidth + sprite.pivot.x, sprite.rect.y + sprite.pivot.y);

    static Sprite[] Sprites(string path, string prefix) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .Where(s => s.name.StartsWith(prefix)).OrderBy(s => s.name).ToArray();

    [Test]
    public void DrunkFramesShareTheDefaultPoseAnchor()
    {
        var reference = Sprites(Root + "02.Idle/01.Default/body.png", "Game_body_");
        Assert.That(reference, Is.Not.Empty);
        Vector2 anchor = CanvasPivot(reference[0]);
        foreach (var (path, prefix) in new[] { ("01.Emotion/01.Drunk/body.png", "Game_drunk_body_"), ("01.Emotion/01.Drunk/face_bottom.png", "Game_drunk_face_bottom_") })
        {
            var frames = Sprites(Root + path, prefix);
            Assert.That(frames.Length, Is.EqualTo(4), path);
            foreach (var frame in frames)
            {
                Vector2 pivot = CanvasPivot(frame);
                Assert.That(pivot.x, Is.EqualTo(anchor.x).Within(.01f), frame.name);
                Assert.That(pivot.y, Is.EqualTo(anchor.y).Within(.01f), frame.name);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
        }
    }

    [TestCase("samho_body_drunk_Loop", 4)]
    [TestCase("samho_lower_face_drunk_Loop", 1)]
    [TestCase("samho_lower_face_drunk_Dialogue", 4)]
    public void DrunkClipsAreAddressableAndUseTheDrunkSheets(string name, int distinctFrames)
    {
        string path = Clips + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Assert.That(clip, Is.Not.Null, path);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(keys.Select(k => k.value).Distinct().Count(), Is.EqualTo(distinctFrames));
        Assert.That(keys.All(k => k.value is Sprite s && s.name.StartsWith("Game_drunk_")), Is.True);
        string group = File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset");
        Assert.That(group.Contains("m_GUID: " + AssetDatabase.AssetPathToGUID(path) + "\n    m_Address: " + name) ||
                    group.Contains("m_GUID: " + AssetDatabase.AssetPathToGUID(path) + "\r\n    m_Address: " + name), Is.True,
            name + " 주소로 Addressables에 등록되어야 합니다.");
    }

    [Test]
    public void CsvMakesDrunkAPartsExpressionThatHidesTheDefaultFace()
    {
        string csv = Path.Combine(Application.streamingAssetsPath, "csv", "characters");
        var expressions = File.ReadAllLines(Path.Combine(csv, "expressions.csv")).Where(l => l.StartsWith("\"samho\",\"drunk\"")).ToArray();
        Assert.That(expressions, Is.Not.Empty);
        Assert.That(expressions.All(l => l.Contains("\"parts_anim\"")), Is.True);
        var parts = File.ReadAllLines(Path.Combine(csv, "expression_parts.csv")).Where(l => l.StartsWith("\"samho/drunk\"")).ToArray();
        foreach (string part in new[] { "eyes", "eyebrows", "upper_face" })
            Assert.That(parts.Where(l => l.Contains($"\"{part}\"")).All(l => l.Contains("\"none\"")) && parts.Any(l => l.Contains($"\"{part}\"")), Is.True, part);
        Assert.That(parts.Any(l => l.Contains("\"body\",\"samho_body_drunk\"")), Is.True);
        Assert.That(parts.Any(l => l.Contains("\"lower_face\",\"samho_lower_face_drunk\"")), Is.True);
    }

    [Test]
    public void NoneLoopTurnsThePartOff()
    {
        var clip = JsonConvert.DeserializeObject<NewExpressionPartClip>("{\"clip\":\"samho_eyes_default\",\"loop\":\"none\"}");
        Assert.That(clip.Loop, Is.EqualTo(ENewAnimLoopMode.None));
        var data = ScriptableObject.CreateInstance<NewExpressionDataSO>();
        try
        {
            data.expressionData = new Dictionary<string, Dictionary<string, NewExpressionEntry>>
            {
                ["samho"] = new()
                {
                    ["drunk"] = new NewExpressionEntry
                    {
                        Mode = ENewExpressionMode.PartsAnim,
                        Parts = new Dictionary<string, NewExpressionPartClip> { ["eyes"] = clip }
                    }
                }
            };
            Assert.That(data.GetPartData("samho", "drunk", EAnimationPart.Eyes).Loop, Is.EqualTo(EAnimLoopMode.None),
                "CharacterLoader는 None이면 그 부위를 끄고 기본 표정으로 물러서지 않는다.");
        }
        finally { Object.DestroyImmediate(data); }
    }
}
