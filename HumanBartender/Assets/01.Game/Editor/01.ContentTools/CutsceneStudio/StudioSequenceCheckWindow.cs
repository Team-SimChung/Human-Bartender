using HumanBartender.CutsceneStudio;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioSequenceCheckWindow : EditorWindow
    {
        [SerializeField]
        private StudioSequence sequence;
        [SerializeField]
        private string[] issues = System.Array.Empty<string>();
        private Vector2 scroll;
        internal static void Open(EditorWindow owner, StudioSequence sequence)
        {
            var window = GetWindow<StudioSequenceCheckWindow>(true, "시퀀스 검사", true);
            window.sequence = sequence;
            window.Check();
            window.minSize = new Vector2(420, 180);
            var size = new Vector2(560, Mathf.Clamp(160 + window.issues.Length * 44, 200, 440));
            window.position = new Rect(owner.position.center - size * .5f, size);
            window.ShowUtility();
        }

        private void Check()
        {
            issues = sequence != null ? sequence.Validate().ToArray() : new[]
            {
                "검사할 시퀀스가 없습니다."
            };
            Repaint();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, 38), new Color(.20f, .20f, .20f));
            EditorGUI.DrawRect(new Rect(0, 37, position.width, 1), new Color(.10f, .10f, .10f));
            GUI.Label(new Rect(16, 9, position.width - 32, 22), $"시퀀스 검사 / {issues.Length}건", EditorStyles.boldLabel);
            GUILayout.BeginArea(new Rect(16, 52, position.width - 32, position.height - 102));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (issues.Length == 0)
                EditorGUILayout.LabelField("검사 항목에서 문제가 발견되지 않았습니다.", EditorStyles.wordWrappedLabel);
            else
                for (int i = 0; i < issues.Length; i++)
                {
                    EditorGUILayout.HelpBox($"{i + 1}. {issues[i]}", MessageType.Warning);
                    GUILayout.Space(5);
                }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.Button(new Rect(position.width - 196, position.height - 36, 92, 24), "다시 검사"))
                Check();
            if (GUI.Button(new Rect(position.width - 96, position.height - 36, 80, 24), "닫기"))
                Close();
        }
    }
}
