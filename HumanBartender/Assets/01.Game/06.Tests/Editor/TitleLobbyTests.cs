using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>QA-34 메인 타이틀: 새 배경 전체 사용, 로고 크기·웹 가로 찢김 글리치, 빗방울 밀도를 검사한다.</summary>
public sealed class TitleLobbyTests
{
    const string MainPath = "Assets/01.Game/01.Scenes/Main.unity";
    const string BackgroundPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/02.Title/01.Sprites/title_street_bg.png";
    const string TearTablePath = "Assets/01.Game/03.Content/03.Vfx/01.Sprites/TX_TitleLogoTear_LUT.png";

    static void WithMain(System.Action<GameObject[]> body)
    {
        var scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
        try { body(scene.GetRootGameObjects()); }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void BackdropShowsTheWholeNewStreetImage()
    {
        WithMain(roots =>
        {
            var backdrop = roots.SelectMany(r => r.GetComponentsInChildren<RawImage>(true)).Single(r => r.name == "City backdrop");
            Assert.That(AssetDatabase.GetAssetPath(backdrop.texture), Is.EqualTo(BackgroundPath));
            Assert.That(backdrop.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), "새 배경은 일부를 잘라 늘리지 않고 전체를 쓴다.");
            Assert.That(backdrop.texture.width, Is.EqualTo(1920));
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        });
    }

    [Test]
    public void LogoUsesTheReferenceSizeAndTheWebSignalTear()
    {
        WithMain(roots =>
        {
            var logo = roots.SelectMany(r => r.GetComponentsInChildren<Image>(true)).Single(i => i.name == "Project LUNA");
            Assert.That(logo.rectTransform.sizeDelta, Is.EqualTo(new Vector2(473, 190)), "QA-34 2쪽 원본 크기 + 좌우 찢김 여백");
            Assert.That(logo.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(6, -56)));
            Assert.That(logo.preserveAspect, Is.False, "여백을 넣은 사각형이라 비율 고정을 끈다.");
            var material = logo.material;
            Assert.That(material.GetFloat("_UseTear"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_HorizontalPad"), Is.EqualTo(.12f).Within(.0001f));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_TearTex")), Is.EqualTo(TearTablePath));
            Assert.That(material.GetFloat("_UseRGBGlitch"), Is.Zero, "참고 영상에는 RGB 분리가 없다.");
            Assert.That(material.GetFloat("_GlitchAmount"), Is.Zero);
            Assert.That(material.GetFloat("_UseBandShift"), Is.Zero);
            Assert.That(material.GetFloat("_TearCycle"), Is.EqualTo(2.86f).Within(.0001f));
        });
    }

    [TestCase(0, 3, 0)]       // 0ms: 찢김 없음
    [TestCase(101, 3, 10)]    // 1010ms 프레임: 3번 줄 +10
    [TestCase(101, 8, -7)]
    [TestCase(168, 44, -12)]  // 1680ms 프레임: 44~71번 줄 -12
    [TestCase(285, 50, 0)]    // 2850ms: 2350ms 이후 정지
    public void TearTableMatchesTheWebFrames(int column, int row, int shift)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TearTablePath);
        Assert.That(importer.sRGBTexture, Is.False, "값 표이므로 선형으로 읽는다.");
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        var table = new Texture2D(2, 2);
        try
        {
            table.LoadImage(File.ReadAllBytes(TearTablePath));
            Assert.That(new Vector2Int(table.width, table.height), Is.EqualTo(new Vector2Int(286, 75)));
            // PNG 위쪽 줄이 0번 줄이다(LoadImage는 아래부터 읽는다).
            int value = Mathf.RoundToInt(table.GetPixel(column, table.height - 1 - row).r * 255);
            Assert.That((value - 128) / 4, Is.EqualTo(shift));
        }
        finally { Object.DestroyImmediate(table); }
    }

    [Test]
    public void TitleRainHasFewerSmallDrops()
    {
        WithMain(roots =>
        {
            var layer = roots.SelectMany(r => r.GetComponentsInChildren<WindowRainLayer>(true)).Single();
            var settings = new SerializedObject(layer).FindProperty("settings");
            // QA-34: 작은 알갱이가 너무 많아 환공포증을 일으킨다 → 생성량·최대 개수·꼬리 방울을 줄이고 가장 작은 방울을 키운다.
            Assert.That(settings.FindPropertyRelative("spawnPerSecond").floatValue, Is.EqualTo(15));
            Assert.That(settings.FindPropertyRelative("maxDrops").intValue, Is.EqualTo(180));
            Assert.That(settings.FindPropertyRelative("spawnRadius").vector2Value.x, Is.GreaterThanOrEqualTo(.8f));
            Assert.That(settings.FindPropertyRelative("trailChance").floatValue, Is.LessThanOrEqualTo(.35f));
        });
    }
}
