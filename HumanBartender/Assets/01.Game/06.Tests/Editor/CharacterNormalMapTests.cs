using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 루나·삼호 외부 스프라이트 시트에 노말맵이 보조 텍스처로 연결되고, 캐릭터 셰이더가 그 노말맵으로 음영을 넣는지 검사한다.
/// </summary>
public sealed class CharacterNormalMapTests
{
    const string Art = CharacterNormalMapSetup.CharacterFolder + "/";
    const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    const string ShaderPath = "Assets/01.Game/03.Content/03.Vfx/03.Shaders/SH_CharacterSpriteLit.shader";

    static readonly string[] Sheets =
    {
        "01.Luna/01.Idle/luna_idle", "01.Luna/02.Run/luna_run", "01.Luna/03.Walk/luna_walk",
        "03.Samho/01.Idle/samho_idle", "03.Samho/02.Run/samho_run",
    };

    [TestCaseSource(nameof(Sheets))]
    public void EverySpriteInTheSheetCarriesItsNormalMap(string sheet)
    {
        string normalPath = Art + sheet + CharacterNormalMapSetup.NormalSuffix + ".png";
        Assert.That(CharacterNormalMapSetup.SheetPathFor(normalPath), Is.EqualTo(Art + sheet + ".png"));
        var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        Assert.That(importer, Is.Not.Null, normalPath);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), "픽셀아트 규칙");
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + sheet + ".png").OfType<Sprite>().ToArray();
        Assert.That(sprites, Is.Not.Empty);
        foreach (var sprite in sprites)
        {
            var secondary = new SecondarySpriteTexture[sprite.GetSecondaryTextureCount()];
            sprite.GetSecondaryTextures(secondary);
            Assert.That(secondary.Any(t => t.name == CharacterNormalMapSetup.NormalMapSlot && t.texture == normal),
                Is.True, sprite.name);
            Assert.That(secondary.Any(t => t.name == "_MaskTex"), Is.False,
                "외곽선만 칠한 마스크는 거리 라이트(Multiply with Mask R)를 몸 안쪽에서 지운다.");
        }
    }

    [Test]
    public void CharacterShaderShadesWithTheNormalMap()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        Assert.That(shader.FindPropertyIndex("_NormalMap"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_NormalShade"), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void LunaAndSamhoUseTheChosenNormalShading()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Additive);
        try
        {
            var lighting = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterSpriteLighting>(true))
                .Where(l => l.name == "Luna" || l.name == "Samho").ToArray();
            Assert.That(lighting.Select(l => l.name).Distinct(), Is.EquivalentTo(new[] { "Luna", "Samho" }));
            foreach (var item in lighting)
            {
                var data = new SerializedObject(item);
                Assert.That(data.FindProperty("normalShading").floatValue, Is.EqualTo(0.6f).Within(1e-4f), item.name);
                Assert.That(data.FindProperty("normalHighlight").floatValue, Is.GreaterThanOrEqualTo(1f), item.name);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
