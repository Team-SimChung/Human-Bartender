// Editor/AnimatorPlaceholderSetup.cs
// 메뉴: Tools > Dialogue > Setup Base Controller Placeholders

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Base AnimatorController의 ExpressionIntro / ExpressionLoop 상태에
/// 플레이스홀더 클립을 자동 생성해서 할당.
/// AnimatorOverrideController가 클립을 교체하려면 Base에 클립이 있어야 함.
/// </summary>
public class AnimatorPlaceholderSetup : EditorWindow
{
    private AnimatorController _baseController;
    private string _outputFolder = "Assets/01.Game/03.Content/07.Animations/04.Shared/01.Clips/01.Expressions";

    private const string STATE_INTRO = "ExpressionIntro";
    private const string STATE_LOOP  = "ExpressionLoop";

    [MenuItem("Tools/Dialogue/Setup Base Controller Placeholders")]
    public static void Open()
    {
        GetWindow<AnimatorPlaceholderSetup>("Placeholder Setup").minSize = new Vector2(360, 160);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Base Controller Placeholder Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "ExpressionIntro / ExpressionLoop 상태에 플레이스홀더 클립을 생성 후 할당합니다.\n" +
            "클립 이름이 Override Controller의 키로 사용됩니다.",
            MessageType.Info);

        EditorGUILayout.Space(6);
        _baseController = (AnimatorController)EditorGUILayout.ObjectField(
            "Base Controller", _baseController, typeof(AnimatorController), false);
        _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

        EditorGUILayout.Space(8);

        GUI.enabled = _baseController != null;
        if (GUILayout.Button("▶  Setup", GUILayout.Height(30)))
            Run();
        GUI.enabled = true;
    }

    private void Run()
    {
        if (!System.IO.Directory.Exists(_outputFolder))
            System.IO.Directory.CreateDirectory(_outputFolder);

        var introClip = CreatePlaceholderClip(STATE_INTRO, loop: false);
        var loopClip  = CreatePlaceholderClip(STATE_LOOP,  loop: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Base Controller의 각 State에 클립 할당
        bool introAssigned = false;
        bool loopAssigned  = false;

        foreach (var layer in _baseController.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == STATE_INTRO)
                {
                    state.state.motion = introClip;
                    introAssigned = true;
                }
                else if (state.state.name == STATE_LOOP)
                {
                    state.state.motion = loopClip;
                    loopAssigned = true;
                }
            }
        }

        EditorUtility.SetDirty(_baseController);
        AssetDatabase.SaveAssets();

        string result = $"Intro: {(introAssigned ? "✓" : "✕ 상태 없음")} / Loop: {(loopAssigned ? "✓" : "✕ 상태 없음")}";
        EditorUtility.DisplayDialog("완료", result, "확인");
        Debug.Log($"[PlaceholderSetup] {result}");
    }

    private AnimationClip CreatePlaceholderClip(string clipName, bool loop)
    {
        string path = $"{_outputFolder}/{clipName}.anim";

        // 이미 있으면 재사용
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;

        var clip = new AnimationClip { name = clipName };

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // m_Sprite 더미 키 1개 — 이게 있어야 Animator가 SpriteRenderer를 구동 대상으로 인식
        var binding = new EditorCurveBinding
        {
            path         = "",
            type         = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        var keys = new ObjectReferenceKeyframe[1];
        keys[0] = new ObjectReferenceKeyframe { time = 0f, value = null };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }
}
