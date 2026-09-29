// Editor/DialogueAnimationTestWindow.cs
// 메뉴: Tools > Dialogue > Animation Test

using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class DialogueAnimationTestWindow : EditorWindow
{
    // ─── 입력 ───────────────────────────────────────────
    private ESlotType _slot = ESlotType.Left;
    private string _speaker = "luna";
    private string _expression = "joy";

    // ─── 파트 개별 테스트 ───────────────────────────────
    private static readonly string[] PART_NAMES = { "body", "eyes", "eyebrows", "upper_face", "lower_face", "extra" };
    private bool[] _partEnabled = { true, true, true, true, true, true };

    // ─── 타겟 ────────────────────────────────────────────
    private DialogueCharacterManager _targetManager;
    CancellationTokenSource preview;

    bool CanControl => _targetManager != null && Application.isPlaying && preview == null &&
        !(FindFirstObjectByType<StoryScriptRunner>()?.IsRunning ?? false) &&
        !(FindFirstObjectByType<DialogueRunner>()?.IsRunning ?? false);

    void OnDisable() => preview?.Cancel();

    // ─── 주소 미리보기 ───────────────────────────────────
    private bool _showAddressPreview = true;

    // ─── 로드 결과 로그 ──────────────────────────────────
    private readonly List<LogEntry> _logs = new();
    private Vector2 _logScroll;

    // ─── 빠른 테스트 프리셋 ──────────────────────────────
    private string _presetSpeaker = "luna";
    private string _newPresetExpression = "";
    private readonly List<string> _presetExpressions = new() { "default", "joy", "sadness", "anger", "surprise", "fear" };

    // ─── 스타일 캐시 ─────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _logSuccessStyle;
    private GUIStyle _logWarningStyle;
    private GUIStyle _logErrorStyle;
    private bool _stylesInitialized;

    [MenuItem("Tools/Dialogue/Animation Test")]
    public static void Open()
    {
        var window = GetWindow<DialogueAnimationTestWindow>("Animation Test");
        window.minSize = new Vector2(420, 650);
    }

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎭 Dialogue Animation Test", _headerStyle);
        EditorGUILayout.Space(4);

        DrawTargetSection();
        EditorGUILayout.Space(8);
        DrawInputSection();
        EditorGUILayout.Space(8);
        DrawPartToggleSection();
        EditorGUILayout.Space(8);
        DrawAddressPreviewSection();
        EditorGUILayout.Space(8);
        DrawQuickPresetSection();
        EditorGUILayout.Space(8);
        DrawLogSection();
    }

    // ── 타겟 매니저 선택 ──────────────────────────────────
    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        using var horizontal = new EditorGUILayout.HorizontalScope();

        _targetManager = (DialogueCharacterManager)EditorGUILayout.ObjectField(
            _targetManager, typeof(DialogueCharacterManager), true);

        GUI.enabled = _targetManager == null;
        if (GUILayout.Button("Auto Find", GUILayout.Width(80)))
        {
            _targetManager = FindFirstObjectByType<DialogueCharacterManager>();
            if (_targetManager == null)
                AddLog(LogLevel.Warning, "씬에서 DialogueCharacterManager를 찾지 못했습니다.");
        }
        GUI.enabled = true;
    }

    // ── Slot / Speaker / Expression 입력 및 제어 버튼 ────
    private void DrawInputSection()
    {
        EditorGUILayout.LabelField("Input & Control", EditorStyles.boldLabel);

        _slot = (ESlotType)EditorGUILayout.EnumPopup("Slot", _slot);
        _speaker = EditorGUILayout.TextField("Speaker", _speaker);
        _expression = EditorGUILayout.TextField("Expression", _expression);

        EditorGUILayout.Space(4);

        // 재생/정지/릴리즈 버튼
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = CanControl;
            if (GUILayout.Button("▶  Play", GUILayout.Height(30)))
                PlayAsync(_slot, _speaker, _expression).Forget();

            if (GUILayout.Button("⏹  Off", GUILayout.Height(30)))
            {
                _targetManager.ResetCharacter(_slot);
                AddLog(LogLevel.Info, $"OffCharacter({_slot}) 호출");
            }

            if (GUILayout.Button("🗑  Release", GUILayout.Height(30)))
            {
                _targetManager.ReleaseAll();
                AddLog(LogLevel.Info, "ReleaseAll 호출");
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space(4);

        // On/Off Dialogue 파라미터 제어 버튼
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = CanControl;
            if (GUILayout.Button("💬 On Dialogue", GUILayout.Height(26)))
                SetDialogueBool(_slot, true);

            if (GUILayout.Button("🔇 Off Dialogue", GUILayout.Height(26)))
                SetDialogueBool(_slot, false);
            GUI.enabled = true;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode에서만 제어 가능합니다.", MessageType.Info);
        }
    }

    // ── 파트 개별 활성화 토글 ─────────────────────────────
    private void DrawPartToggleSection()
    {
        EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);

        using var horizontal = new EditorGUILayout.HorizontalScope();
        for (int i = 0; i < PART_NAMES.Length; i++)
        {
            _partEnabled[i] = GUILayout.Toggle(
                _partEnabled[i],
                PART_NAMES[i],
                GUI.skin.button,
                GUILayout.Height(24));
        }
    }

    // ── 주소 미리보기 ─────────────────────────────────────
    private void DrawAddressPreviewSection()
    {
        _showAddressPreview = EditorGUILayout.Foldout(_showAddressPreview, "Address Preview", true);
        if (!_showAddressPreview) return;

        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

        if (_targetManager == null || !Application.isPlaying)
            EditorGUILayout.HelpBox("Play Mode에서 로드된 CSV의 리소스 후보 주소를 표시합니다.", MessageType.Info);
        else
            foreach (var key in _targetManager.GetConfiguredResourceKeys(_speaker, _expression)) DrawAddressLabel(key);
    }

    private void DrawAddressLabel(string address)
    {
        EditorGUILayout.SelectableLabel(address, EditorStyles.miniLabel, GUILayout.Height(16));
    }

    // ── 빠른 프리셋 ───────────────────────────────────────
    private void DrawQuickPresetSection()
    {
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _presetSpeaker = EditorGUILayout.TextField(_presetSpeaker, GUILayout.Width(80));
            _newPresetExpression = EditorGUILayout.TextField(_newPresetExpression);
            if (GUILayout.Button("+", GUILayout.Width(24)) && !string.IsNullOrEmpty(_newPresetExpression))
            {
                if (!_presetExpressions.Contains(_newPresetExpression))
                    _presetExpressions.Add(_newPresetExpression);
                _newPresetExpression = "";
            }
        }

        EditorGUILayout.Space(2);

        int columns = 3;
        for (int i = 0; i < _presetExpressions.Count; i += columns)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            for (int j = i; j < Mathf.Min(i + columns, _presetExpressions.Count); j++)
            {
                string exp = _presetExpressions[j];
                GUI.enabled = CanControl;
                if (GUILayout.Button(exp, GUILayout.Height(22)))
                {
                    _speaker = _presetSpeaker;
                    _expression = exp;
                    PlayAsync(_slot, _speaker, _expression).Forget();
                }
                GUI.enabled = true;
            }
        }
    }

    // ── 로그 섹션 ─────────────────────────────────────────
    private void DrawLogSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _logs.Clear();
        }

        float logHeight = Mathf.Max(80, position.height - 520);
        using var scrollView = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.Height(logHeight));
        _logScroll = scrollView.scrollPosition;

        foreach (var entry in _logs)
        {
            var style = entry.Level switch
            {
                LogLevel.Success => _logSuccessStyle,
                LogLevel.Warning => _logWarningStyle,
                LogLevel.Error => _logErrorStyle,
                _ => EditorStyles.miniLabel
            };
            EditorGUILayout.LabelField(entry.Message, style);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 재생 로직
    // ═══════════════════════════════════════════════════════

    private async UniTask PlayAsync(ESlotType slot, string speaker, string expression)
    {
        if (!CanControl) return;
        var target = _targetManager;
        using var source = new CancellationTokenSource();
        preview = source;
        try
        {
            AddLog(LogLevel.Info, $"[{slot} / {speaker} / {expression}] 로드 시작...");
            foreach (var key in target.GetConfiguredResourceKeys(speaker, expression))
            {
                bool exists = await CheckAddressExistsAsync(key, source.Token);
                AddLog(exists ? LogLevel.Success : LogLevel.Info, $"{key}: {(exists ? "있음" : "없음 (선택적 후보 포함)")}");
            }
            await target.SetCharacterAsync(speaker, expression, slot, source.Token);
            source.Token.ThrowIfCancellationRequested();
            ApplyPartToggles(slot);
            AddLog(LogLevel.Success, $"[{slot} / {speaker} / {expression}] 완료");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (this != null) AddLog(LogLevel.Error, e.Message); }
        finally { if (ReferenceEquals(preview, source)) preview = null; }
    }

    private void SetDialogueBool(ESlotType slot, bool value)
    {
        if (_targetManager == null) return;

        var so = new SerializedObject(_targetManager);
        var slotPartsProp = so.FindProperty("slotParts");
        if (slotPartsProp == null) return;

        bool animatorFound = false;

        for (int s = 0; s < slotPartsProp.arraySize; s++)
        {
            var slotProp = slotPartsProp.GetArrayElementAtIndex(s);
            var typeProp = slotProp.FindPropertyRelative("type");
            if (typeProp == null || typeProp.enumValueIndex != (int)slot) continue;

            var partsProp = slotProp.FindPropertyRelative("parts");
            if (partsProp == null) return;

            for (int i = 0; i < partsProp.arraySize; i++)
            {
                var partProp = partsProp.GetArrayElementAtIndex(i);

                // 버그 수정: "Animator" (대문자) -> "animator" (소문자, CharacterPart.cs와 동일)
                var animProp = partProp.FindPropertyRelative("animator");

                if (animProp?.objectReferenceValue is Animator animator)
                {
                    animator.SetBool("OnDialogue", value);
                    animatorFound = true;
                }
            }
            break;
        }

        if (animatorFound)
            AddLog(LogLevel.Info, $"[{slot}] OnDialogue = {value}");
        else
            AddLog(LogLevel.Warning, $"[{slot}] 설정할 Animator를 찾을 수 없습니다.");
    }

    // ═══════════════════════════════════════════════════════
    // 유틸
    // ═══════════════════════════════════════════════════════

    private void ApplyPartToggles(ESlotType slot)
    {
        if (_targetManager == null) return;

        var so = new SerializedObject(_targetManager);
        var slotPartsProp = so.FindProperty("slotParts");
        if (slotPartsProp == null) return;

        for (int s = 0; s < slotPartsProp.arraySize; s++)
        {
            var slotProp = slotPartsProp.GetArrayElementAtIndex(s);
            var typeProp = slotProp.FindPropertyRelative("type");
            if (typeProp == null || typeProp.enumValueIndex != (int)slot) continue;

            var partsProp = slotProp.FindPropertyRelative("parts");
            if (partsProp == null) return;

            for (int i = 0; i < partsProp.arraySize; i++)
            {
                var partProp = partsProp.GetArrayElementAtIndex(i);
                var nameProp = partProp.FindPropertyRelative("partName");
                if (nameProp == null) continue;

                int idx = (EAnimationPart)nameProp.enumValueIndex switch
                {
                    EAnimationPart.Body => 0, EAnimationPart.Eyes => 1, EAnimationPart.Eyeblows => 2,
                    EAnimationPart.Upper_Face => 3, EAnimationPart.Lower_Face => 4, EAnimationPart.Extra => 5, _ => -1
                };
                if (idx < 0) continue;

                // 버그 수정: "Animator" (대문자) -> "animator" (소문자)
                var animProp = partProp.FindPropertyRelative("animator");
                if (animProp?.objectReferenceValue is Animator animatorObj)
                    animatorObj.enabled = _partEnabled[idx];
            }
            return;
        }
    }

    private async UniTask<bool> CheckAddressExistsAsync(string address, CancellationToken token)
    {
        var handle = Addressables.LoadResourceLocationsAsync(address);
        try
        {
            var locations = await handle.ToUniTask(cancellationToken: token);
            return handle.Status == AsyncOperationStatus.Succeeded && locations.Count > 0;
        }
        finally { if (handle.IsValid()) Addressables.Release(handle); }
    }

    // ── 로그 유틸 ─────────────────────────────────────────
    private void AddLog(LogLevel level, string message)
    {
        string prefix = level switch
        {
            LogLevel.Success => "✓ ",
            LogLevel.Warning => "⚠ ",
            LogLevel.Error => "✕ ",
            _ => "  "
        };
        _logs.Add(new LogEntry(level, $"{System.DateTime.Now:HH:mm:ss}  {prefix}{message}"));
        Repaint();
    }

    // ── 스타일 초기화 ─────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.85f, 1f) }
        };

        _logSuccessStyle = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.4f, 0.9f, 0.5f) } };
        _logWarningStyle = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
        _logErrorStyle = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
    }

    // ── 내부 타입 ─────────────────────────────────────────
    private enum LogLevel { Info, Success, Warning, Error }

    private class LogEntry
    {
        public LogLevel Level;
        public string Message;
        public LogEntry(LogLevel level, string message) { Level = level; Message = message; }
    }
}