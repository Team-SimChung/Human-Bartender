#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 여러 폴더를 "지정한 순서대로" 결합해, 폴더 안의 개별 스프라이트들을
/// SpriteRenderer.m_Sprite 스왑 AnimationClip 하나로 만든다.
/// (스프라이트시트가 아니라 낱장 이미지가 폴더로 정리돼 있을 때 사용)
///
/// 사용법:
///  1) 이 파일을 반드시 Assets/Editor/ 같은 "Editor" 폴더 안에 둔다.
///  2) Tools ▸ Cutscene ▸ Sprite Folders → AnimationClip 메뉴로 창을 연다.
///  3) Folders 리스트에 폴더를 드래그로 넣고(드래그로 순서 변경) Generate.
///
/// 결합 순서: 리스트 위→아래 폴더 순서. 각 폴더 안에서는 파일명을 자연 정렬
/// (frame_2 가 frame_10 보다 앞)하여 이어 붙인다.
/// </summary>
public class SpriteFoldersToClipWindow : EditorWindow
{
    private float frameRate = 12f;     // 도트는 12~15fps 정도면 충분
    private bool loop = true;
    private bool legacy = false;       // 구형 Animation 컴포넌트로 재생하면 ON
    private bool recursive = false;    // 하위 폴더까지 포함할지
    private string childPath = "";      // SpriteRenderer 가 자식이면 그 상대 경로
    private string clipName = "";       // 비우면 첫 폴더 이름 사용
    private string outputFolder = "";   // 비우면 첫 폴더에 저장

    private List<DefaultAsset> folders = new List<DefaultAsset>();
    private ReorderableList folderList;
    private int previewCount = -1;

    [MenuItem("Tools/Cutscene/Sprite Folders → AnimationClip")]
    private static void Open()
    {
        GetWindow<SpriteFoldersToClipWindow>("Folders → Clip").minSize = new Vector2(380, 340);
    }

    private void OnEnable()
    {
        folderList = new ReorderableList(folders, typeof(DefaultAsset), true, true, true, true);

        folderList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Folders  (위 → 아래 순서로 결합 · 드래그로 순서 변경)");

        folderList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            var picked = EditorGUI.ObjectField(
                rect, $"#{index + 1}", folders[index], typeof(DefaultAsset), false) as DefaultAsset;

            if (picked != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(picked)))
            {
                Debug.LogWarning("[SpriteFoldersToClip] 폴더만 지정할 수 있습니다.");
                picked = folders[index];
            }

            if (picked != folders[index])
            {
                folders[index] = picked;
                previewCount = -1; // 변경되면 미리보기 무효화
            }
        };

        folderList.onChangedCallback = _ => previewCount = -1;

        // DefaultAsset(=UnityEngine.Object 파생)은 기본 add 로직이 Activator.CreateInstance 를
        // 시도하다 예외가 나서 GUILayout 상태가 깨진다. null 항목을 직접 추가하도록 오버라이드.
        folderList.onAddCallback = _ =>
        {
            folders.Add(null);
            previewCount = -1;
        };

        folderList.onRemoveCallback = list =>
        {
            if (list.index >= 0 && list.index < folders.Count)
                folders.RemoveAt(list.index);
            else if (folders.Count > 0)
                folders.RemoveAt(folders.Count - 1);
            previewCount = -1;
        };
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "폴더를 순서대로 넣으면 그 안의 낱장 스프라이트를 모아\n" +
            "SpriteRenderer.m_Sprite 스왑 클립 하나로 만듭니다.",
            MessageType.Info);

        EditorGUILayout.Space();
        folderList.DoLayoutList();

        EditorGUILayout.Space();
        frameRate = EditorGUILayout.FloatField("Frame Rate (fps)", frameRate);
        loop = EditorGUILayout.Toggle("Loop", loop);
        recursive = EditorGUILayout.Toggle(
            new GUIContent("Recursive", "각 폴더의 하위 폴더까지 포함"), recursive);
        legacy = EditorGUILayout.Toggle(
            new GUIContent("Legacy Clip", "구형 Animation 컴포넌트면 ON · Animator/Playables 면 OFF"),
            legacy);
        childPath = EditorGUILayout.TextField(
            new GUIContent("Child Path", "SpriteRenderer 가 재생 오브젝트의 자식이면 그 상대 경로. 같은 오브젝트면 비워둠."),
            childPath);
        clipName = EditorGUILayout.TextField(
            new GUIContent("Clip Name", "비우면 첫 폴더 이름 사용"), clipName);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string p = EditorUtility.OpenFolderPanel("Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(p)) outputFolder = ToAssetPath(p);
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("프레임 수 미리보기"))
                previewCount = CollectSprites().Count;

            string label;
            if (previewCount >= 0)
            {
                string len = frameRate > 0f ? $" · {previewCount / frameRate:0.00}s" : "";
                label = $"총 {previewCount} 프레임{len}";
            }
            else
            {
                label = "총 — 프레임 (미리보기를 눌러 계산)";
            }
            EditorGUILayout.LabelField(label);
        }

        EditorGUILayout.Space();

        bool hasFolder = folders.Any(f => f != null);
        using (new EditorGUI.DisabledScope(!hasFolder || frameRate <= 0f))
        {
            if (GUILayout.Button("Generate", GUILayout.Height(34)))
                Generate();
        }
    }

    private void Generate()
    {
        List<Sprite> sprites = CollectSprites();
        if (sprites.Count == 0)
        {
            Debug.LogWarning("[SpriteFoldersToClip] 모은 스프라이트가 없습니다.");
            return;
        }

        DefaultAsset firstFolder = folders.First(f => f != null);
        string firstFolderPath = AssetDatabase.GetAssetPath(firstFolder);

        string name = string.IsNullOrEmpty(clipName) ? Path.GetFileName(firstFolderPath) : clipName;
        string outFolder = string.IsNullOrEmpty(outputFolder) ? firstFolderPath : outputFolder;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{outFolder}/{name}.anim");

        CreateClip(sprites, path);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);

        Debug.Log($"[SpriteFoldersToClip] 생성: {path}  ({sprites.Count} frames @ {frameRate}fps)");
    }

    private void CreateClip(List<Sprite> sprites, string path)
    {
        var clip = new AnimationClip
        {
            frameRate = frameRate,
            legacy = legacy
        };

        EditorCurveBinding binding =
            EditorCurveBinding.PPtrCurve(childPath, typeof(SpriteRenderer), "m_Sprite");

        // 각 프레임을 i/fps 시점에 배치 + 마지막 프레임 유지용 끝 키프레임 → 길이 = N/fps
        var keys = new ObjectReferenceKeyframe[sprites.Count + 1];
        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }
        keys[sprites.Count] = new ObjectReferenceKeyframe
        {
            time = sprites.Count / frameRate,
            value = sprites[sprites.Count - 1]
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        if (legacy)
        {
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        }
        else
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        AssetDatabase.CreateAsset(clip, path);
    }

    // ── 수집 ──────────────────────────────────────────────────────────

    private List<Sprite> CollectSprites()
    {
        var result = new List<Sprite>();

        foreach (DefaultAsset folder in folders)
        {
            if (folder == null) continue;

            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folderPath)) continue;

            result.AddRange(GetSpritesInFolder(folderPath));
        }

        return result;
    }

    private List<Sprite> GetSpritesInFolder(string folderPath)
    {
        var sprites = new List<Sprite>();

        string abs;
        try { abs = Path.GetFullPath(folderPath); }
        catch { return sprites; }

        string[] files;
        try
        {
            files = Directory.GetFiles(
                abs, "*.*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
        catch { return sprites; }

        List<string> assetPaths = files
            .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .Select(ToAssetPath)
            .Where(p => p.StartsWith("Assets", StringComparison.Ordinal))
            .ToList();

        // 폴더 안에서는 전체 경로 기준 자연 정렬 (하위 폴더 그룹 유지 + 숫자 수치 비교)
        assetPaths.Sort((a, b) => NaturalCompare(a, b));

        foreach (string p in assetPaths)
        {
            List<Sprite> reps = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(p)
                .OfType<Sprite>()
                .ToList();

            if (reps.Count == 0) continue; // 스프라이트가 아닌 파일은 건너뜀

            // 한 파일이 여러 스프라이트(시트)면 그것도 이름순으로
            if (reps.Count > 1)
                reps.Sort((a, b) => NaturalCompare(a.name, b.name));

            sprites.AddRange(reps);
        }

        return sprites;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private static string ToAssetPath(string absolute)
    {
        absolute = absolute.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        return absolute.StartsWith(dataPath, StringComparison.Ordinal)
            ? "Assets" + absolute.Substring(dataPath.Length)
            : absolute;
    }

    /// <summary>"frame_2" 가 "frame_10" 보다 앞에 오도록 숫자 구간을 수치로 비교.</summary>
    private static int NaturalCompare(string a, string b)
    {
        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            char ca = a[ia], cb = b[ib];
            if (char.IsDigit(ca) && char.IsDigit(cb))
            {
                int sa = ia; while (ia < a.Length && char.IsDigit(a[ia])) ia++;
                int sb = ib; while (ib < b.Length && char.IsDigit(b[ib])) ib++;

                string na = a.Substring(sa, ia - sa).TrimStart('0');
                string nb = b.Substring(sb, ib - sb).TrimStart('0');

                if (na.Length != nb.Length) return na.Length - nb.Length;
                int cmp = string.CompareOrdinal(na, nb);
                if (cmp != 0) return cmp;
            }
            else
            {
                if (ca != cb) return ca - cb;
                ia++; ib++;
            }
        }
        return (a.Length - ia) - (b.Length - ib);
    }
}
#endif