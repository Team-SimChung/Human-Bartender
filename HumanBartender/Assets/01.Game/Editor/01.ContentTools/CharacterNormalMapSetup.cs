using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 외부 캐릭터 스프라이트 시트에 노말맵을 보조 텍스처(_NormalMap)로 연결한다.
/// 규칙: 시트 옆에 "시트이름-Normal.png"를 두고 메뉴를 실행한다. 입체감의 세기는 CharacterSpriteLighting의 '노말맵 음영'에서 조절한다.
/// 마스크맵(_MaskTex)은 연결하지 않는다. 거리 라이트 대부분이 "Multiply with Mask (R)"라서 외곽선만 칠한 마스크를 넣으면 몸 안쪽이 라이트를 받지 못한다.
/// 다시 실행해도 이미 연결된 보조 텍스처와 이미 노말맵으로 바꾼 임포트 설정은 건드리지 않는다.
/// </summary>
public static class CharacterNormalMapSetup
{
    public const string CharacterFolder = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside";
    public const string NormalSuffix = "-Normal";
    public const string NormalMapSlot = "_NormalMap";

    [MenuItem("Tools/Human Bartender/Characters/Link Outside Normal Maps")]
    public static void Run()
    {
        int linked = Apply();
        Debug.Log($"[CharacterNormalMapSetup] 노말맵 {linked}개를 새로 연결했습니다.");
    }

    /// <summary>batchmode용: 연결하고 종료 코드로 결과를 알린다.</summary>
    public static void RunBatch()
    {
        try { Run(); Debug.Log("CHARACTER_NORMAL_MAP_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    /// <summary>폴더 안의 모든 "-Normal" 그림을 같은 이름의 시트에 연결한다. 새로 연결한 시트 수를 돌려준다.</summary>
    public static int Apply()
    {
        int linked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CharacterFolder }))
        {
            string normalPath = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(normalPath);
            if (!name.EndsWith(NormalSuffix, StringComparison.Ordinal)) continue;
            string sheetPath = SheetPathFor(normalPath);
            if (AssetImporter.GetAtPath(sheetPath) is not TextureImporter sheet)
            {
                Debug.LogWarning($"[CharacterNormalMapSetup] {normalPath}에 맞는 시트({sheetPath})가 없습니다.");
                continue;
            }
            ConfigureNormalMap(normalPath, sheet);
            if (Link(sheet, AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath))) linked++;
        }
        return linked;
    }

    public static string SheetPathFor(string normalPath)
    {
        string name = Path.GetFileNameWithoutExtension(normalPath);
        string folder = Path.GetDirectoryName(normalPath)?.Replace('\\', '/');
        return $"{folder}/{name.Substring(0, name.Length - NormalSuffix.Length)}{Path.GetExtension(normalPath)}";
    }

    static void ConfigureNormalMap(string path, TextureImporter sheet)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == TextureImporterType.NormalMap) return;
        importer.textureType = TextureImporterType.NormalMap;
        // 시트와 같은 픽셀아트 규칙(Point, 무압축, 밉맵 없음). 가장자리 노말이 이웃 프레임으로 번지지 않게 Clamp.
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = sheet.maxTextureSize;
        importer.SaveAndReimport();
    }

    static bool Link(TextureImporter sheet, Texture2D normal)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(sheet);
        provider.InitSpriteEditorDataProvider();
        var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>();
        var textures = secondary.textures ?? Array.Empty<SecondarySpriteTexture>();
        if (textures.Any(t => t.name == NormalMapSlot)) return false;
        secondary.textures = textures.Append(new SecondarySpriteTexture { name = NormalMapSlot, texture = normal }).ToArray();
        provider.Apply();
        sheet.SaveAndReimport();
        return true;
    }
}
