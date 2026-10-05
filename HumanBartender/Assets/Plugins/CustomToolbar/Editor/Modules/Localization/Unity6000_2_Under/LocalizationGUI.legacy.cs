#if USE_LOCALIZATION
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static partial class LocalizationGUI
    {
#if !UNITY_6000_3_OR_NEWER
        internal static void OnGUI(VisualElement element)
        {
            var dropdown = new EditorToolbarDropdown();
            dropdown.name = "LocalizationDropdown";

            // 텍스트 설정
            if (TryGetLocales(out var locals))
                dropdown.text = $"Localization : {locals[CurrentLocalIndex]}";
            else
                dropdown.text = $"Localization : None";
            
            // 아이콘 설정
            dropdown.icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Packages/com.unity.localization/Editor/Icons/Locale/{ToolbarUtility.DarkModePrefix}Locale.png");

            // 로컬 변경 이벤트 설정
            LocalizationSettings.SelectedLocaleChanged += locale => dropdown.text = $"Localization : {locale}";

            // 드롭다운 클릭 이벤트 설정
            dropdown.clicked += () => ShowMenuItem(dropdown);

            element.Add(dropdown);
        }


        /// <summary>
        /// 드롭다운 메뉴를 표시합니다.
        /// </summary>
        /// <param name="dropdown">드롭다운 요소</param>
        private static void ShowMenuItem(EditorToolbarDropdown dropdown)
        {
            var menu = new GenericMenu();

            if (TryGetLocales(out var locales))
            {
                for (int i = 0; i < locales.Count; i++)
                {
                    int index = i;
                    menu.AddItem(new GUIContent(locales[index]), false,
                        () => LocalizationSettings.SelectedLocale = LocalizationEditorSettings.GetLocales()[index]);
                }
            }
            
            menu.DropDown(dropdown.worldBound);
        }
#endif
    }
}
#endif