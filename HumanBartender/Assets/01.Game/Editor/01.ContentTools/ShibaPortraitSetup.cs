using System;
using UnityEditor;
using UnityEngine;

/// <summary>시바 초상의 누락된 시각 배치와 픽셀아트 임포트 설정을 보완한다.</summary>
public static class ShibaPortraitSetup
{
    public const string DataPath = "Assets/01.Game/04.Data/02.RuntimeCaches/NewExpressionDataSO.asset";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/01.Bar/08.Shiba/";

    [MenuItem("Tools/Human Bartender/Characters/Configure Shiba Portrait")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ShibaPortrait] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var data = AssetDatabase.LoadAssetAtPath<NewExpressionDataSO>(DataPath);
        if (data == null) throw new InvalidOperationException("[ShibaPortrait] 기존 표정 SO를 찾을 수 없습니다.");
        var idle = AssetImporter.GetAtPath(ArtPath + "Shiba_Idle.png") as TextureImporter;
        var talk = AssetImporter.GetAtPath(ArtPath + "Shiba_Idle2.png") as TextureImporter;
        if (idle == null || talk == null) throw new InvalidOperationException("[ShibaPortrait] 기존 시바 이미지를 찾을 수 없습니다.");

        AddMissingPlacement(data);
        ConfigureTexture(idle);
        ConfigureTexture(talk);
        AssetDatabase.SaveAssetIfDirty(data);
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
        Debug.Log("[ShibaPortrait] 시바 초상 배치와 Point·무압축 설정을 저장했습니다. 기존 보정값은 유지했습니다. 초상 시각 배치의 offsetPixels에서 높이를 조절하세요.");
    }

    public static void AddMissingPlacement(NewExpressionDataSO data)
    {
        var owner = new SerializedObject(data);
        var entries = owner.FindProperty("portraitPlacements");
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("characterId").stringValue == "shiba") return;
        Undo.RecordObject(data, "Configure Shiba Portrait");
        entries.InsertArrayElementAtIndex(entries.arraySize);
        var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("characterId").stringValue = "shiba";
        // 최초 에디터 배치만 정의한다. 이후 Inspector 수정값은 재실행해도 보존한다.
        entry.FindPropertyRelative("offsetPixels").vector2IntValue = new Vector2Int(0, 35);
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
    }

    static void ConfigureTexture(TextureImporter importer)
    {
        Undo.RecordObject(importer, "Configure Shiba Portrait Texture");
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.format = TextureImporterFormat.RGBA32;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);
        }
        importer.SaveAndReimport();
    }
}
