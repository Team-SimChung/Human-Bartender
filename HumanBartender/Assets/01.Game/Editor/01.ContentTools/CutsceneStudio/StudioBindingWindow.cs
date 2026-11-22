using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioBindingWindow : EditorWindow
    {
        [SerializeField] private StudioSequence sequence;
        [SerializeField] private string callId = "", memoryKey = "";
        private Vector2 scroll;
        internal static void Open(StudioSequence value)
        {
            var window = GetWindow<StudioBindingWindow>("컷씬 호출 연결");
            window.sequence = value;
            window.minSize = new Vector2(480, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("memoryKey가 비어 있으면 기본 호출에만 연결됩니다. 구분값이 있는 호출은 정확히 같은 연결만 사용하며, 없으면 CSV 대체 재생으로 이어집니다. 기존 회상 연결은 제작자의 확인 후 추가하세요.", MessageType.Info);
            sequence = (StudioSequence)EditorGUILayout.ObjectField("제작 파일", sequence, typeof(StudioSequence), false);
            callId = EditorGUILayout.TextField("호출 ID", callId);
            memoryKey = EditorGUILayout.TextField("memoryKey (선택)", memoryKey);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || sequence == null || string.IsNullOrWhiteSpace(callId)))
                if (GUILayout.Button("이 조건의 연결 저장")) StudioCatalogRegistration.RegisterBinding(callId, memoryKey, sequence);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("등록된 연결 (동일 조건 저장 시 교체)", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var catalog = StudioCatalogRegistration.FindCatalog(false);
            if (catalog != null)
                foreach (var entry in catalog.Entries)
                    if (entry != null)
                    {
                        EditorGUILayout.LabelField(entry.Id + " / " + (string.IsNullOrEmpty(entry.MemoryKey) ? "기본" : entry.MemoryKey), EditorStyles.boldLabel);
                        EditorGUILayout.SelectableLabel(entry.ResourceKey, GUILayout.Height(18));
                    }
            EditorGUILayout.EndScrollView();
        }
    }
}
