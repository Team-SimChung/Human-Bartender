using HumanBartender.CutsceneStudio;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioTrackSettingsWindow : EditorWindow
    {
        private CutsceneStudioWindow owner;
        private StudioSequence sequence;
        private StudioTrack track;
        private string trackName;
        private bool focusName = true;
        internal static void Open(CutsceneStudioWindow owner, StudioSequence sequence, StudioTrack track, string name)
        {
            var window = CreateInstance<StudioTrackSettingsWindow>();
            window.owner = owner;
            window.sequence = sequence;
            window.track = track;
            window.trackName = name;
            window.titleContent = new GUIContent("트랙 설정");
            window.minSize = window.maxSize = new Vector2(380, 150);
            window.position = new Rect(owner.position.center - new Vector2(190, 75), new Vector2(380, 150));
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (track == null || owner == null)
            {
                Close();
                return;
            }

            // 입력창이 Enter 이벤트를 처리하기 전에 확인 동작을 처리함.
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    Event.current.Use();
                    Close();
                    return;
                }

                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    Event.current.Use();
                    ApplyName();
                    return;
                }
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, 32), new Color(.20f, .20f, .20f));
            EditorGUI.DrawRect(new Rect(0, 31, position.width, 1), new Color(.10f, .10f, .10f));
            GUI.Label(new Rect(12, 6, 300, 22), "트랙 설정", EditorStyles.boldLabel);
            GUI.Label(new Rect(16, 44, 340, 20), "이름");
            GUI.SetNextControlName("TrackName");
            trackName = EditorGUI.TextField(new Rect(16, 66, position.width - 32, 22), trackName);
            if (focusName)
            {
                EditorGUI.FocusTextInControl("TrackName");
                focusName = false;
            }

            if (GUI.Button(new Rect(position.width - 180, 110, 76, 24), "취소"))
            {
                Close();
                return;
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(trackName)))
                if (GUI.Button(new Rect(position.width - 96, 110, 80, 24), "적용"))
                {
                    ApplyName();
                    return;
                }
        }

        private void ApplyName()
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return;
            owner.RenameTimelineTrack(sequence, track, trackName);
            Close();
        }
    }
}
