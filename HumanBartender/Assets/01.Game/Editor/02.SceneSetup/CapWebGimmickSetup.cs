// 메뉴: Tools > Crafting > Set Up Web Cap Gimmick

using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NewCapGame 프리팹을 웹 본편 병따기 화면으로 바꾼다. 다시 실행해도 같은 결과가 나온다.
///
/// 예전 링 한 단계짜리 연출(병·뚜껑 스프라이트, 고리, HUD 캔버스)을 지우고, 웹 그림을 그대로 쓰는
/// CapOpenView·CapOpenAudio를 Cap Manager에 붙인다. 배경과 입력, 예전 경로의 버튼 캔버스는 그대로 둔다.
/// 배치 모드: -executeMethod CapWebGimmickSetup.Run
/// </summary>
public static class CapWebGimmickSetup
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/04.Cap/NewCapGame.prefab";
    const string ArtFolder = "Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/04.Open";
    const string BottlePath = ArtFolder + "/item_beer_open.png";
    const string OpenerPath = ArtFolder + "/bottle_opener_open.png";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/Regular_PFStardust_3_0_SDF.asset";
    const string OpenClipPath = "Assets/01.Game/03.Content/04.Audio/03.SFX/Gameplay/Crafting/bottle_can_open.wav";
    const string FizzClipPath = "Assets/01.Game/03.Content/04.Audio/03.SFX/Gameplay/Crafting/fizz_tail.wav";
    const string BalanceGuid = "63f99327f1a0418a94d6ec6cd9c49dca";

    /// <summary>웹 화면에는 없는 예전 연출. 이름으로 찾아 지운다.</summary>
    static readonly string[] LegacyObjects = { "Ring Center", "HUD Canvas" };

    [MenuItem("Tools/Crafting/Set Up Web Cap Gimmick")]
    public static void Run()
    {
        ConfigureTexture(BottlePath);
        ConfigureTexture(OpenerPath);

        var bottle = Require<Texture2D>(BottlePath);
        var opener = Require<Texture2D>(OpenerPath);
        var font = Require<TMP_FontAsset>(FontPath);
        var openClip = Require<AudioClip>(OpenClipPath);
        var fizzClip = Require<AudioClip>(FizzClipPath);
        var balance = Require<NewBalanceDataSO>(AssetDatabase.GUIDToAssetPath(BalanceGuid));

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (string name in LegacyObjects)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child != null && child.name == name)
                    {
                        Object.DestroyImmediate(child.gameObject);
                        break;
                    }

            var manager = root.GetComponentInChildren<CapManager>(true);
            if (manager == null) throw new System.InvalidOperationException("[CapSetup] 프리팹에 CapManager가 없습니다.");

            // 에디터의 GetComponent는 없을 때 가짜 null을 돌려주므로 ??가 아니라 TryGetComponent로 확인한다.
            if (!manager.TryGetComponent(out CapOpenView view)) view = manager.gameObject.AddComponent<CapOpenView>();
            if (!manager.TryGetComponent(out CapOpenAudio audio)) audio = manager.gameObject.AddComponent<CapOpenAudio>();

            Set(view, "bottleTexture", bottle);
            Set(view, "openerTexture", opener);
            Set(view, "font", font);
            Set(audio, "openClip", openClip);
            Set(audio, "fizzClip", fizzClip);
            Set(manager, "view", view);
            Set(manager, "openAudio", audio);
            Set(manager, "balanceData", balance);

            new CapUiAuthoring(view).Build();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[CapSetup] NewCapGame을 웹 병따기 화면으로 갱신했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>웹은 이 그림을 확대할 때 보간하지 않는다(imageSmoothingEnabled=false). 원본 픽셀을 그대로 읽는다.</summary>
    static void ConfigureTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("[CapSetup] 텍스처가 없습니다: " + path);

        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static T Require<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.InvalidOperationException($"[CapSetup] {typeof(T).Name}를 찾지 못했습니다: {path}");
        return asset;
    }

    static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null) throw new System.InvalidOperationException($"[CapSetup] {target.GetType().Name}.{field} 필드가 없습니다.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
