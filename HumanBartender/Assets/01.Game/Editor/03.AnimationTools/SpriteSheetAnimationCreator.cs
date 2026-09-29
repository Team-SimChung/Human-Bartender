// Editor/SpriteSheetAnimationCreator.cs
// 메뉴: Tools > Sprite Sheet Animation Creator

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D.Sprites; // 추가: 모던 2D API 사용을 위해 필요
using UnityEngine;

public class SpriteSheetAnimationCreator : EditorWindow
{
    // ─── 공통 설정 ──────────────────────────────────────
    private string _characterName = "luna";
    private string _expression = "joy";
    private string _clipState = "Loop";
    private int _frameRate = 12;
    private bool _isLoop = true;
    private string _addressableGroup = "Default Local Group";

    // ─── 파츠 엔트리 리스트 ─────────────────────────────
    [System.Serializable]
    private class PartEntry
    {
        public string partName = "";
        public Texture2D texture = null;
        public int columns = 4;
        public int rows = 1;
        public bool enabled = true;

        // 추가: 피벗 설정 변수
        public SpriteAlignment alignment = SpriteAlignment.BottomCenter;
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        public string GetAddress(string character, string expression, string state)
            => $"{character}_{partName}_{expression}_{state}";

        public string GetClipName(string character, string expression, string state)
            => $"{character}_{partName}_{expression}_{state}";
    }

    private static readonly string[] DEFAULT_PARTS = { "eyes", "eyebrows", "upper_face", "lower_face", "body", "extra" };

    private List<PartEntry> _entries = new();
    private Vector2 _entryScroll;

    // ─── 로그 ───────────────────────────────────────────
    private readonly List<string> _logs = new();
    private Vector2 _logScroll;

    // ─── 스타일 ─────────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _successStyle;
    private GUIStyle _warningStyle;
    private GUIStyle _errorStyle;
    private bool _stylesInit;

    [MenuItem("Tools/Sprite Sheet Animation Creator")]
    public static void Open()
    {
        var window = GetWindow<SpriteSheetAnimationCreator>("Batch Anim Creator");
        window.minSize = new Vector2(550, 750);
    }

    private void OnEnable()
    {
        if (_entries.Count == 0)
            ResetToDefaultParts();
    }

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎞 Batch Sprite Sheet → Animation Clips", _headerStyle);
        EditorGUILayout.Space(6);

        DrawCommonSection();
        EditorGUILayout.Space(8);
        DrawPartsSection();
        EditorGUILayout.Space(8);
        DrawAddressPreview();
        EditorGUILayout.Space(8);
        DrawActionButtons();
        EditorGUILayout.Space(8);
        DrawLogSection();
    }

    private void DrawCommonSection()
    {
        EditorGUILayout.LabelField("Common Settings", EditorStyles.boldLabel);
        _characterName = EditorGUILayout.TextField("Character", _characterName);
        _expression = EditorGUILayout.TextField("Expression", _expression);

        using (new EditorGUILayout.HorizontalScope())
        {
            _clipState = EditorGUILayout.TextField("State", _clipState);
            if (GUILayout.Button("Intro", GUILayout.Width(55))) { _clipState = "Intro"; _isLoop = false; }
            if (GUILayout.Button("Loop", GUILayout.Width(55))) { _clipState = "Loop"; _isLoop = true; }
            if (GUILayout.Button("Dialogue", GUILayout.Width(65))) { _clipState = "Dialogue"; _isLoop = false; }
        }

        _frameRate = EditorGUILayout.IntField("Frame Rate", Mathf.Max(1, _frameRate));
        _isLoop = EditorGUILayout.Toggle("Loop Clip", _isLoop);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            var groupNames = settings.groups.Where(g => g != null).Select(g => g.Name).ToArray();
            int idx = System.Array.IndexOf(groupNames, _addressableGroup);
            if (idx < 0) idx = 0;
            idx = EditorGUILayout.Popup("Addressable Group", idx, groupNames);
            if (idx >= 0 && idx < groupNames.Length) _addressableGroup = groupNames[idx];
        }
    }

    private void DrawPartsSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset Default", GUILayout.Width(100))) ResetToDefaultParts();
            if (GUILayout.Button("+ Add", GUILayout.Width(60))) _entries.Add(new PartEntry());
        }

        EditorGUILayout.Space(2);
        float listHeight = Mathf.Min(_entries.Count * 85 + 10, 400); // 높이 약간 상향
        using var scroll = new EditorGUILayout.ScrollViewScope(_entryScroll, GUILayout.Height(listHeight));
        _entryScroll = scroll.scrollPosition;

        int removeIndex = -1;
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(16));
                    GUI.enabled = entry.enabled;
                    entry.partName = EditorGUILayout.TextField(entry.partName, GUILayout.Width(90));
                    entry.texture = (Texture2D)EditorGUILayout.ObjectField(entry.texture, typeof(Texture2D), false, GUILayout.Width(160));

                    EditorGUILayout.LabelField("C", GUILayout.Width(12));
                    entry.columns = EditorGUILayout.IntField(entry.columns, GUILayout.Width(30));
                    EditorGUILayout.LabelField("R", GUILayout.Width(12));
                    entry.rows = EditorGUILayout.IntField(entry.rows, GUILayout.Width(30));

                    if (GUILayout.Button("×", GUILayout.Width(22))) removeIndex = i;
                }

                if (entry.enabled)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Pivot", GUILayout.Width(40));
                        entry.alignment = (SpriteAlignment)EditorGUILayout.EnumPopup(entry.alignment, GUILayout.Width(100));

                        if (entry.alignment == SpriteAlignment.Custom)
                            entry.pivot = EditorGUILayout.Vector2Field("", entry.pivot, GUILayout.Width(120));

                        EditorGUI.indentLevel--;
                    }
                }
                GUI.enabled = true;
            }
        }
        if (removeIndex >= 0) _entries.RemoveAt(removeIndex);
    }

    private void DrawAddressPreview()
    {
        EditorGUILayout.LabelField("Address Preview", EditorStyles.boldLabel);
        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        foreach (var entry in _entries)
        {
            if (!entry.enabled || string.IsNullOrEmpty(entry.partName)) continue;
            string addr = entry.GetAddress(_characterName, _expression, _clipState);
            EditorGUILayout.LabelField(entry.texture != null ? $"✓ {addr}" : $"✕ {addr}", EditorStyles.miniLabel);
        }
    }

    private void DrawActionButtons()
    {
        int activeCount = _entries.Count(e => e.enabled && e.texture != null && !string.IsNullOrEmpty(e.partName));
        using var horizontal = new EditorGUILayout.HorizontalScope();
        GUI.enabled = activeCount > 0;
        if (GUILayout.Button("1. Slice All", GUILayout.Height(32))) BatchSlice();
        if (GUILayout.Button("2. Create Clips", GUILayout.Height(32))) BatchCreateClips();
        GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
        if (GUILayout.Button($"▶ All In One ({activeCount})", GUILayout.Height(32)))
        {
            BatchSlice();
            AssetDatabase.Refresh();
            BatchCreateClips();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawLogSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) _logs.Clear();
        }
        float logHeight = Mathf.Max(60, position.height - 680);
        using var scroll = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.Height(logHeight));
        _logScroll = scroll.scrollPosition;
        foreach (var log in _logs)
        {
            GUIStyle style = log.Contains("✓") ? _successStyle : (log.Contains("⚠") ? _warningStyle : (log.Contains("✕") ? _errorStyle : EditorStyles.miniLabel));
            EditorGUILayout.LabelField(log, style);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 배치 처리 (개선된 API 적용)
    // ═══════════════════════════════════════════════════════

    private void BatchSlice()
    {
        AddLog($"── Slice 시작: {_characterName} / {_expression} ──");
        foreach (var entry in _entries)
        {
            if (!entry.enabled || entry.texture == null || string.IsNullOrEmpty(entry.partName)) continue;
            SliceSpriteSheet(entry);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddLog("── Slice 완료 ──");
    }

    private void SliceSpriteSheet(PartEntry entry)
    {
        string path = AssetDatabase.GetAssetPath(entry.texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        // 기초 설정
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport(); // API 활성화를 위해 1차 저장

        // 모던 API 사용 (SpriteDataProvider)
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        int cellW = entry.texture.width / Mathf.Max(1, entry.columns);
        int cellH = entry.texture.height / Mathf.Max(1, entry.rows);
        int total = entry.rows * entry.columns;

        var spriteRects = new List<SpriteRect>();
        int idx = 0;

        for (int row = 0; row < entry.rows; row++)
        {
            for (int col = 0; col < entry.columns; col++)
            {
                spriteRects.Add(new SpriteRect
                {
                    name = $"{entry.texture.name}_{idx:D3}",
                    rect = new Rect(col * cellW, (entry.rows - 1 - row) * cellH, cellW, cellH),
                    alignment = entry.alignment,
                    pivot = entry.pivot,
                    spriteID = GUID.Generate() // 핵심: 고유 ID를 생성하여 중복 에러 방지
                });
                idx++;
            }
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply(); // 변경 사항 적용

        importer.SaveAndReimport();
        AddLog($"  ✓ [{entry.partName}] 슬라이스 완료 (Pivot: {entry.alignment})");
    }

    private void BatchCreateClips()
    {
        AddLog($"── Clip 생성 시작 ──");
        int successCount = 0;
        foreach (var entry in _entries)
        {
            if (entry.enabled && entry.texture != null && !string.IsNullOrEmpty(entry.partName))
            {
                if (CreateAnimationClip(entry)) successCount++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddLog($"── 완료: 성공 {successCount} ──");
    }

    private bool CreateAnimationClip(PartEntry entry)
    {
        string path = AssetDatabase.GetAssetPath(entry.texture);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();

        if (sprites.Length == 0) return false;

        string outputFolder = Path.GetDirectoryName(path);
        var clip = new AnimationClip { frameRate = _frameRate };

        var keyframes = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe { time = i / (float)_frameRate, value = sprites[i] };
        }

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = _isLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string clipName = entry.GetClipName(_characterName, _expression, _clipState);
        string clipPath = $"{outputFolder}/{clipName}.anim";

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
        else AssetDatabase.CreateAsset(clip, clipPath);

        string address = entry.GetAddress(_characterName, _expression, _clipState);
        SetAddressableAddress(clipPath, address);
        return true;
    }

    private void SetAddressableAddress(string assetPath, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;
        var group = settings.groups.FirstOrDefault(g => g != null && g.Name == _addressableGroup);
        if (group == null) return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    private void ResetToDefaultParts()
    {
        _entries.Clear();
        foreach (var name in DEFAULT_PARTS) _entries.Add(new PartEntry { partName = name });
    }

    private void AddLog(string message) { _logs.Add($"{System.DateTime.Now:HH:mm:ss}  {message}"); Repaint(); }

    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = new Color(0.9f, 0.85f, 1f) } };
        _successStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.9f, 0.5f) } };
        _warningStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
        _errorStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
    }
}