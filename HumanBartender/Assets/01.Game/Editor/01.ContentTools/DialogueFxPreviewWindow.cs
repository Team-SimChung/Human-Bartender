using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>Play Mode preview using the same speech bubbles and renderer as the game.</summary>
public sealed class DialogueFxPreviewWindow : EditorWindow
{
    const string Example = "이 잔을 <color=#FF5555><size=130%><shake amp=1.5 hz=18>또</shake></size></color> 버렸다고<pop peak=1.2 duration=0.12><size=150%>!</size></pop>";
    DynamicSpeechBubble first;
    DynamicSpeechBubble second;
    NewTextTagDataSO tags;
    string sentence = Example;
    string rowId = "preview";

    [MenuItem("Tools/DialogueFX/Preview")]
    static void Open() => GetWindow<DialogueFxPreviewWindow>("Dialogue FX Preview");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Enter Play Mode, assign one or two active speech bubbles, then preview. No story data is changed.", MessageType.Info);
        first = (DynamicSpeechBubble)EditorGUILayout.ObjectField("First bubble", first, typeof(DynamicSpeechBubble), true);
        second = (DynamicSpeechBubble)EditorGUILayout.ObjectField("Second bubble", second, typeof(DynamicSpeechBubble), true);
        tags = (NewTextTagDataSO)EditorGUILayout.ObjectField("CSV tag asset", tags, typeof(NewTextTagDataSO), false);
        rowId = EditorGUILayout.TextField("Dialogue / row ID", rowId);
        sentence = EditorGUILayout.TextArea(sentence, GUILayout.MinHeight(80));
        if (GUILayout.Button("Validate markup"))
        {
            if (DialogueTextCompiler.TryCompile(sentence, tags, DialoguePresentationSettings.Shared,
                    null, 0.05f, out _, out string error)) Debug.Log($"[DialogueFX] {rowId}: valid");
            else Debug.LogError($"[DialogueFX] {rowId}: {error}");
        }
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || first == null))
        {
            if (GUILayout.Button("Type first")) Type(first);
            if (GUILayout.Button("Show instant first")) first.SetText(sentence, tags);
            if (GUILayout.Button("Reveal all first")) first.TextPlayer.Skip();
            if (GUILayout.Button("Stop first")) first.TextPlayer.Stop();
        }
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || first == null || second == null))
        {
            if (GUILayout.Button("Type both independently")) { Type(first); Type(second); }
            if (GUILayout.Button("Stop both")) { first.TextPlayer.Stop(); second.TextPlayer.Stop(); }
        }
        if (GUILayout.Button("Restore required example")) sentence = Example;
    }

    void Type(DynamicSpeechBubble bubble)
    {
        var data = new TypingData(sentence, "Preview", Vector3.zero, Color.white, true);
        bubble.TextPlayer.PlayAsync(data, tags, 0.05f).Forget(Debug.LogException);
    }
}
