using System;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace AssetDeleteGuard
{
    internal static class SafeDeleteMenu
    {
        private const string NativeMenu = "Tools/Asset Delete Guard/Guard Native Delete";

        [MenuItem(NativeMenu)]
        private static void ToggleNative() { NativeDeleteSettings.Enabled = !NativeDeleteSettings.Enabled; }

        [MenuItem(NativeMenu, true)]
        private static bool ValidateNative() { Menu.SetChecked(NativeMenu, NativeDeleteSettings.Enabled); return true; }

        [MenuItem("Assets/Asset Delete Guard/Review References", false, 2000)]
        private static void Review() { Open(false); }

        [MenuItem("Assets/Asset Delete Guard/Review and Delete", false, 2001)]
        private static void Delete() { Open(true); }

        [MenuItem("Assets/Asset Delete Guard/Review References", true)]
        [MenuItem("Assets/Asset Delete Guard/Review and Delete", true)]
        private static bool Validate()
        {
            return !Application.isBatchMode && Selection.objects.Length > 0 &&
                   Selection.objects.All(o => o != null && EditorUtility.IsPersistent(o));
        }

        [Shortcut("Asset Delete Guard/Review and Delete")]
        private static void DeleteShortcut() { if (Validate()) Open(true); }

        internal static void Open(bool allowDelete)
        {
            if (Application.isBatchMode) return;
            try
            {
                var paths = SelectionPaths();
                DeleteReviewWindow.Open(paths, allowDelete);
            }
            catch (Exception ex) { EditorUtility.DisplayDialog("Asset Delete Guard", ex.Message, "OK"); }
        }

        internal static string[] SelectionPaths()
        {
            var selected = Selection.objects;
            if (selected.Length == 0) throw new InvalidOperationException(UiText.Get("Project 창에서 에셋을 선택하세요.", "Select assets in the Project window."));
            foreach (var obj in selected)
            {
                if (obj == null || !EditorUtility.IsPersistent(obj) || AssetDatabase.IsSubAsset(obj) || !AssetDatabase.IsMainAsset(obj))
                    throw new InvalidOperationException(UiText.Get("서브에셋은 삭제하지 않습니다. 부모 파일을 직접 선택하세요.",
                        "Select main asset files explicitly. Sub-assets are not promoted to their parent file."));
            }
            return AssetPaths.ReduceRoots(selected.Select(AssetDatabase.GetAssetPath));
        }
    }
}
