#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static WingmanInspector.WingmanUtility;

namespace WingmanInspector {

    public class WingmanPersistentData : ScriptableObject {

        public readonly WingmanClipboard clipboard = new WingmanClipboard();
        [SerializeField] private List<WingmanId> indexLookUp = new List<WingmanId>();
        [SerializeField] private List<string> searchFields = new List<string>();
        [SerializeField] private List<SelectionData> selectedCompIds = new List<SelectionData>();
        [SerializeField] private List<LockedInspectorRestoreState> lockedInspectorRestoreStates = new List<LockedInspectorRestoreState>();
        
        [Serializable]
        private class SelectionData {
            public List<WingmanId> selectionList = new List<WingmanId>();
        }
        
        [Serializable]
        private class LockedInspectorRestoreState {
            public WingmanId inspectorInstanceId;
            public Object inspectingObject;
        }

        public List<WingmanId> SelectedCompIds(Object obj) {
            if (GetObjectIndex(obj, out int index)) {
                return selectedCompIds[index].selectionList;
            }
            return null;
        } 
        
        public string SearchString(Object obj) {
            if (GetObjectIndex(obj, out int index)) {
                return searchFields[index];
            }
            return string.Empty;
        }

        public void SetSearchString(Object obj, string str) {
            if (GetObjectIndex(obj, out int index)) {
                searchFields[index] = str;
            }
        }

        public void AddDataForContainer(Object obj) {
            WingmanId id = GetWingmanId(obj);

            // BinarySearch returns index if found in list, or negative bitwise compliment of index if not found
            int index = indexLookUp.BinarySearch(id);
            if (index >= 0) return;
            
            index = ~index; // Turn negative bitwise compliment into insertion index 
            indexLookUp.Insert(index, id); 
            selectedCompIds.Insert(index, new SelectionData());
            searchFields.Insert(index, string.Empty);
        }

        public void SetDataForLockedInspector(EditorWindow inspectorWindow, Object inspectingObject) {
            int entryIndex = -1;
            for (int i = 0; i < lockedInspectorRestoreStates.Count; i++) {
                if (lockedInspectorRestoreStates[i].inspectorInstanceId == GetWingmanId(inspectorWindow)) {
                    entryIndex = i;
                    break;
                }
            }

            if (entryIndex == -1) {
                LockedInspectorRestoreState newState = new LockedInspectorRestoreState();
                newState.inspectorInstanceId = GetWingmanId(inspectorWindow);
                newState.inspectingObject = inspectingObject;
                lockedInspectorRestoreStates.Add(newState);
                return;
            }

            lockedInspectorRestoreStates[entryIndex].inspectingObject = inspectingObject;
        }

        public Object GetRestoredObjectForInspectorWindow(EditorWindow inspectorWindow) {
            foreach (LockedInspectorRestoreState state in lockedInspectorRestoreStates) {
                if (state.inspectorInstanceId == GetWingmanId(inspectorWindow)) {
                    return state.inspectingObject;
                }
            }
            return null;
        }

        public void ClearAllData() {
            indexLookUp.Clear();
            selectedCompIds.Clear();
            searchFields.Clear();
            lockedInspectorRestoreStates.Clear();
            AssetDatabase.SaveAssetIfDirty(this);
        }
        
        private bool GetObjectIndex(Object obj, out int index) {
            index = indexLookUp.BinarySearch(GetWingmanId(obj));
            return index >= 0;
        }
        
        [CustomEditor(typeof(WingmanPersistentData))]
        private class Editor : UnityEditor.Editor {
            
            public override void OnInspectorGUI() {
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.wordWrap = true;
                
                EditorGUILayout.LabelField(
                    $"Stores persistent data for {nameof(Wingman)} like selected components and search strings.\n\n" +
                    "This data clears every time the editor is restarted.\n\n" +
                    "This file can be safely ignored by version control.", 
                    labelStyle 
                );
            }
            
        }

    }

}
#endif