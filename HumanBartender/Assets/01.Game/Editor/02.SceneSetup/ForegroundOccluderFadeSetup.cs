using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OutSide의 near_foreground_Fade(간판·기둥·파이프)를 조각별로 흐려지게 만든다(QA-54, 웹 outside-art.js).
/// 그림을 불투명한 세로 띠마다 한 조각으로 자르고, 각 조각의 불투명 부분을 원본 PNG에서 사각형으로 구워 ForegroundOccluderFade에 넣는다.
/// 다시 실행하면 이미 만든 조각·연결·조절한 값은 두고 비어 있는 것만 채운다.
/// </summary>
public static class ForegroundOccluderFadeSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    public const string ObjectName = "near_foreground_Fade";
    const string PlayerName = "Luna";
    static readonly string[] PartNames = { "Sign", "Column", "Pipe" };
    // 원본 PNG 알파가 이 값보다 크면 불투명으로 본다(웹은 0보다 크면).
    const byte AlphaThreshold = 0;

    [MenuItem("Tools/Human Bartender/Outside Ground/Setup Foreground Occluder Fade")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ForegroundOccluderFadeSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (Apply(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    /// <summary>batchmode용: 설정을 실행하고 종료 코드로 결과를 알린다.</summary>
    public static void RunBatch()
    {
        try { Run(); Debug.Log("OCCLUDER_FADE_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    /// <summary>씬에 조각 페이드를 만들고 연결한다. 바뀐 것이 있으면 true.</summary>
    public static bool Apply(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var root = all.FirstOrDefault(t => t.name == ObjectName);
        if (root == null)
        {
            Debug.LogWarning("[ForegroundOccluderFadeSetup] " + ObjectName + " 오브젝트가 없습니다.");
            return false;
        }
        bool changed = false;
        var fade = root.GetComponent<ForegroundOccluderFade>();
        var source = root.GetComponent<SpriteRenderer>();
        if (fade == null)
        {
            if (source == null || source.sprite == null)
            {
                Debug.LogWarning("[ForegroundOccluderFadeSetup] " + ObjectName + "에 원본 SpriteRenderer가 없습니다.");
                return false;
            }
            changed = Split(root, source, out var parts);
            fade = Undo.AddComponent<ForegroundOccluderFade>(root.gameObject);
            using var data = new SerializedObject(fade);
            var list = data.FindProperty("parts");
            list.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("renderer").objectReferenceValue = parts[i].renderer;
                var rects = item.FindPropertyRelative("opaqueRects");
                rects.arraySize = parts[i].rects.Count;
                for (int r = 0; r < parts[i].rects.Count; r++) rects.GetArrayElementAtIndex(r).rectValue = parts[i].rects[r];
            }
            var player = all.FirstOrDefault(t => t.name == PlayerName && t.CompareTag("Player"))?.GetComponent<SpriteRenderer>();
            if (player != null)
            {
                data.FindProperty("target").objectReferenceValue = player;
                data.FindProperty("body").rectValue = OpaqueBody(player.sprite);
            }
            else Debug.LogWarning("[ForegroundOccluderFadeSetup] Player 태그의 Luna를 찾지 못해 대상을 비워 둡니다.");
            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[ForegroundOccluderFadeSetup] " + ObjectName + "를 조각 " + parts.Count + "개로 나눠 연결했습니다.");
            return true;
        }

        // 이미 만든 경우: 사람이 조절한 값은 두고 대상 연결만 채운다.
        using (var data = new SerializedObject(fade))
        {
            var target = data.FindProperty("target");
            if (target.objectReferenceValue == null)
            {
                var player = all.FirstOrDefault(t => t.name == PlayerName && t.CompareTag("Player"))?.GetComponent<SpriteRenderer>();
                if (player != null)
                {
                    target.objectReferenceValue = player;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
        }
        return changed;
    }

    sealed class PartData
    {
        public SpriteRenderer renderer;
        public readonly List<Rect> rects = new();
    }

    /// <summary>원본 그림을 불투명한 세로 띠마다 한 조각으로 자르고, 원래 자리에 자식 SpriteRenderer로 둔다.</summary>
    static bool Split(Transform root, SpriteRenderer source, out List<PartData> parts)
    {
        var sprite = source.sprite;
        string path = AssetDatabase.GetAssetPath(sprite);
        var pixels = ReadPixels(path, out int width, out int height);
        float ppu = sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot;
        var columns = OpaqueColumnRuns(pixels, width, height);
        if (columns.Count != PartNames.Length)
            Debug.LogWarning($"[ForegroundOccluderFadeSetup] 불투명한 세로 띠가 {columns.Count}개입니다(예상 {PartNames.Length}개). 찾은 띠마다 조각을 만듭니다.");

        // 그림을 조각마다 자른다(왼쪽 아래 피벗). 기존 식별자는 보존한다.
        string baseName = Path.GetFileNameWithoutExtension(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        var definitions = columns.Select((c, i) => new SpriteRect
        {
            name = baseName + "_" + (i < PartNames.Length ? PartNames[i] : "Part" + i),
            rect = new Rect(c.x, 0, c.y - c.x, height),
            alignment = SpriteAlignment.BottomLeft,
            pivot = Vector2.zero
        }).ToList();
        NpcSpriteSheetImporter.Apply(importer, definitions);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var slices = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);

        parts = new List<PartData>();
        var material = source.sharedMaterial;
        int layerId = source.sortingLayerID, order = source.sortingOrder;
        var color = source.color;
        Undo.DestroyObjectImmediate(source);
        for (int i = 0; i < definitions.Count; i++)
        {
            string name = (i < PartNames.Length ? PartNames[i] : "Part" + i) + "Sprite";
            var child = root.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, "Create " + name);
                child.SetParent(root, false);
            }
            child.gameObject.layer = root.gameObject.layer;
            var x0 = definitions[i].rect.x;
            child.localPosition = new Vector3((x0 - pivot.x) / ppu, -pivot.y / ppu, 0);
            // GetComponent의 빈 값은 Unity 가짜 null이라 ??로 이어 쓰지 않는다.
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(child.gameObject);
            if (!slices.TryGetValue(definitions[i].name, out var slice))
                Debug.LogWarning("[ForegroundOccluderFadeSetup] 자른 스프라이트가 없어 비워 둡니다: " + definitions[i].name);
            renderer.sprite = slice;
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            renderer.color = color;
            var part = new PartData { renderer = renderer };
            foreach (var r in OpaqueRects(pixels, width, height, (int)columns[i].x, (int)columns[i].y))
                part.rects.Add(new Rect((r.x - pivot.x) / ppu, (r.y - pivot.y) / ppu, r.width / ppu, r.height / ppu));
            parts.Add(part);
        }
        return true;
    }

    static Color32[] ReadPixels(string path, out int width, out int height)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("[ForegroundOccluderFadeSetup] PNG를 읽지 못했습니다: " + path);
            width = texture.width; height = texture.height;
            return texture.GetPixels32();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    /// <summary>한 칸이라도 불투명한 열이 이어진 구간들(x 시작, x 끝 제외).</summary>
    static List<Vector2> OpaqueColumnRuns(Color32[] pixels, int width, int height)
    {
        var runs = new List<Vector2>();
        int start = -1;
        for (int x = 0; x <= width; x++)
        {
            bool opaque = false;
            if (x < width)
                for (int y = 0; y < height && !opaque; y++) opaque = pixels[y * width + x].a > AlphaThreshold;
            if (opaque && start < 0) start = x;
            else if (!opaque && start >= 0) { runs.Add(new Vector2(start, x)); start = -1; }
        }
        return runs;
    }

    /// <summary>[x0, x1) 범위의 불투명 픽셀을 행 단위 구간으로 나누고, 위아래로 같은 구간은 하나로 합친 사각형(픽셀, 아래가 0).</summary>
    static List<RectInt> OpaqueRects(Color32[] pixels, int width, int height, int x0, int x1)
    {
        var done = new List<RectInt>();
        var open = new Dictionary<(int, int), RectInt>();
        for (int y = 0; y < height; y++)
        {
            var row = new List<(int, int)>();
            int start = -1;
            for (int x = x0; x <= x1; x++)
            {
                bool opaque = x < x1 && pixels[y * width + x].a > AlphaThreshold;
                if (opaque && start < 0) start = x;
                else if (!opaque && start >= 0) { row.Add((start, x)); start = -1; }
            }
            var next = new Dictionary<(int, int), RectInt>();
            foreach (var run in row)
                next[run] = open.TryGetValue(run, out var rect)
                    ? new RectInt(rect.x, rect.y, rect.width, rect.height + 1)
                    : new RectInt(run.Item1, y, run.Item2 - run.Item1, 1);
            foreach (var pair in open) if (!next.ContainsKey(pair.Key)) done.Add(pair.Value);
            open = next;
        }
        done.AddRange(open.Values);
        return done;
    }

    /// <summary>캐릭터 그림(반전 전)의 불투명 범위를 피벗 기준 월드 단위로 돌려준다.</summary>
    static Rect OpaqueBody(Sprite sprite)
    {
        if (sprite == null) return new Rect(-.15f, -.27f, .3f, .78f);
        var pixels = ReadPixels(AssetDatabase.GetAssetPath(sprite), out int width, out _);
        var rect = sprite.rect;
        int left = int.MaxValue, right = -1, bottom = int.MaxValue, top = -1;
        for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
            for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                if (pixels[y * width + x].a > 32)
                {
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
        if (right < left) return new Rect(-.15f, -.27f, .3f, .78f);
        float ppu = sprite.pixelsPerUnit;
        return new Rect((left - rect.x - sprite.pivot.x) / ppu, (bottom - rect.y - sprite.pivot.y) / ppu,
            (right - left + 1) / ppu, (top - bottom + 1) / ppu);
    }
}
