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
        private bool continueDuringWait;
        internal static void Open(CutsceneStudioWindow owner, StudioSequence sequence, StudioTrack track, string name)
        {
            var window = CreateInstance<StudioTrackSettingsWindow>();
            window.owner = owner;
            window.sequence = sequence;
            window.track = track;
            window.trackName = name;
            window.continueDuringWait = track.ContinueDuringWait;
            window.titleContent = new GUIContent("트랙 설정");
            window.minSize = window.maxSize = new Vector2(440, 255);
            window.position = new Rect(owner.position.center - new Vector2(220, 127), new Vector2(440, 255));
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

            continueDuringWait = EditorGUI.Toggle(new Rect(16, 98, 400, 22), "입력 대기 중 계속 재생", continueDuringWait);
            EditorGUI.HelpBox(new Rect(16, 126, position.width - 32, 78),"반복 스프라이트·음원·흔들림만 계속됩니다. 일회성 음원은 끝까지 재생됩니다. 이동·카메라·강도 키는 정지하며 설정창에서는 모두 멈춥니다. " +
                "클립 속성의 '입력 대기 중'이 '트랙 설정 따름'인 클립에 적용됩니다.", MessageType.Info);
            if (GUI.Button(new Rect(position.width - 180, 219, 76, 24), "취소"))
            {
                Close();
                return;
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(trackName)))
                if (GUI.Button(new Rect(position.width - 96, 219, 80, 24), "적용"))
                {
                    ApplyName();
                    return;
                }
        }

        private void ApplyName()
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return;
            Undo.RecordObject(track, "트랙 대기 설정");
            track.ContinueDuringWait = continueDuringWait;
            EditorUtility.SetDirty(track);
            owner.RenameTimelineTrack(sequence, track, trackName);
            Close();
        }
    }
}
