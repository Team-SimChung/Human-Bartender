using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal static class StudioCatalogRegistration
    {
        // 위치 대신 타입과 GUID로 기존 목록을 찾아 중복 목록 생성을 막음.
        internal static StudioCatalog FindCatalog(bool create)
        {
            string[] guids = AssetDatabase.FindAssets("t:StudioCatalog");
            if (guids.Length > 1)
                throw new InvalidOperationException("컷씬 등록 목록이 여러 개입니다. 사용할 목록을 하나로 정리하세요.");
            if (guids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<StudioCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (!create)
                return null;
            StudioEditorAssets.EnsureFolder(StudioEditorAssets.Root);
            var catalog = ScriptableObject.CreateInstance<StudioCatalog>();
            AssetDatabase.CreateAsset(catalog, StudioEditorAssets.Root + "/CutsceneStudioCatalog.asset");
            return catalog;
        }

        internal static void EnsureCatalogRegistered()
        {
            var catalog = FindCatalog(false);
            if (catalog == null)
                return;
            RegisterAddress(catalog, StudioCatalog.Address);
        }

        internal static void Register(StudioSequence sequence)
        {
            var catalog = FindCatalog(true);
            string key = "cutscene-studio/" + sequence.Id;
            var entry = RegisterAddress(sequence, key);
            UpdateRegistration(catalog, sequence.Id, null, key, entry);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            RegisterAddress(catalog, StudioCatalog.Address);
        }

        internal static void UpdateRegistration(StudioCatalog catalog, string id, string memoryKey, string key, string previousKey = null)
        {
            for (int i = catalog.Sequences.Count - 1; i >= 0; i--)
            {
                var existing = catalog.Sequences[i];
                if (existing == null) { catalog.Sequences.RemoveAt(i); continue; }
                if (!string.IsNullOrEmpty(previousKey) && existing.ResourceKey == previousKey) existing.ResourceKey = key;
                if (existing.Id == id && (existing.MemoryKey ?? "") == (memoryKey ?? "")) catalog.Sequences.RemoveAt(i);
            }

            catalog.Sequences.Add(new StudioCatalogEntry { Id = id, MemoryKey = memoryKey, ResourceKey = key });
        }

        internal static void RegisterBinding(string id, string memoryKey, StudioSequence sequence)
        {
            if (string.IsNullOrWhiteSpace(id) || sequence == null) throw new ArgumentException("[CutsceneStudio] 호출 ID와 제작 파일을 지정하세요.");
            Register(sequence);
            var catalog = FindCatalog(false);
            Undo.RecordObject(catalog, "컷씬 호출 연결");
            UpdateRegistration(catalog, id.Trim(), memoryKey ?? "", "cutscene-studio/" + sequence.Id);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static string RegisterAddress(UnityEngine.Object asset, string key)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (var candidate in group.entries)
                    if (candidate.address == key && candidate.guid != guid)
                        throw new InvalidOperationException("이미 등록된 컷씬 주소입니다: " + key);
            }

            var current = settings.FindAssetEntry(guid);
            string oldKey = current?.address;
            if (current != null && current.address == key)
                return oldKey;
            current = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            current.SetAddress(key);
            EditorUtility.SetDirty(current.parentGroup);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(current.parentGroup);
            AssetDatabase.SaveAssetIfDirty(settings);
            return oldKey;
        }
    }
}
