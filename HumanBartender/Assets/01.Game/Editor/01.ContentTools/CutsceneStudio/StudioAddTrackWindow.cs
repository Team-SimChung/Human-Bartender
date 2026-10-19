using HumanBartender.CutsceneStudio;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioAddTrackWindow : EditorWindow
    {
        private static readonly string[] Labels = BuildLabels();
        private static string[] BuildLabels()
        {
            var definitions = StudioClipSchema.Definitions;
            var labels = new string[definitions.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = definitions[i].Label;
            return labels;
        }
        [SerializeField]
        private CutsceneStudioWindow owner;
        [SerializeField]
        private StudioSequence sequence;
        [SerializeField]
        private int kindIndex;
        internal static void Open(CutsceneStudioWindow owner, StudioSequence sequence)
        {
            var window = GetWindow<StudioAddTrackWindow>(true, "새 트랙 추가", true);
            window.owner = owner;
            window.sequence = sequence;
            window.kindIndex = 0;
            window.minSize = window.maxSize = new Vector2(400, 190);
            window.position = new Rect(owner.position.center - new Vector2(200, 95), new Vector2(400, 190));
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (owner == null || sequence == null || sequence.Timeline == null)
            {
                Close();
                return;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, 36), new Color(.20f, .20f, .20f));
            EditorGUI.DrawRect(new Rect(0, 35, position.width, 1), new Color(.10f, .10f, .10f));
            GUI.Label(new Rect(16, 8, 340, 22), "새 트랙 추가", EditorStyles.boldLabel);
            GUI.Label(new Rect(16, 50, 70, 22), "트랙 유형");
            StudioGUI.Popup(new Rect(96, 50, position.width - 112, 22), kindIndex, Labels, this, SelectTrackKind);
            GUI.Label(new Rect(16, 88, position.width - 32, 42), "선택한 유형의 빈 트랙을 만듭니다.\n추가한 트랙에 같은 유형의 클립을 끌어 놓으세요.", EditorStyles.wordWrappedLabel);
            if (GUI.Button(new Rect(position.width - 180, 150, 76, 24), "취소"))
            {
                Close();
                return;
            }

            if (GUI.Button(new Rect(position.width - 96, 150, 80, 24), "추가"))
            {
                owner.AddTimelineTrack(sequence, StudioClipSchema.Definitions[kindIndex].Kind);
                Close();
            }

            void SelectTrackKind(int index)
            {
                kindIndex = index;
            }
        }
    }
}
