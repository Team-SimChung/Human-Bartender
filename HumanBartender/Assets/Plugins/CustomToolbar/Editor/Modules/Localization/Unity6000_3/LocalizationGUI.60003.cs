#if USE_LOCALIZATION
using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static partial class LocalizationGUI
    {
#if UNITY_6000_3_OR_NEWER

        /// <summary>
        /// Initializes the localization dropdown UI.
        /// </summary>
        /// <param name="element">The MainToolbarWindow root visual element.</param>
        public static void Init(VisualElement element)
        {
            if (element == null)
            {
                Debug.Log("element is null");
                return;
            }

            if (TryGetDropdown(element, out var dropdown))
            {
                if (TryGetLocales(out var locales))
                    dropdown.text = $"Localization : {locales[CurrentLocalIndex]}";
                else
                    dropdown.text = "Localization : None";
                
                dropdown.style.maxWidth = new StyleLength(StyleKeyword.Auto);
            }
        }

        /// <summary>
        /// Sets the enabled state of the localization dropdown.
        /// </summary>
        /// <param name="element">The MainToolbarWindow root visual element.</param>
        /// <param name="enable">Whether the dropdown should be enabled.</param>
        public static void SetEnable(VisualElement element, bool enable)
        {
            if (TryGetDropdown(element, out var dropdown))
                dropdown.SetEnabled(enable);
        }

        /// <summary>
        /// Updates the dropdown text when the locale changes.
        /// </summary>
        /// <param name="element">The MainToolbarWindow root visual element.</param>
        /// <param name="locale">The newly selected locale.</param>
        public static void UpdateText(VisualElement element, Locale locale)
        {
            if (TryGetDropdown(element, out var dropdown))
                dropdown.text = $"Localization : {locale}";
        }

        [MainToolbarElement("NK Toolbar/Localization Switcher", defaultDockPosition = MainToolbarDockPosition.Left,
            defaultDockIndex = 14)]
        [UsedImplicitly]
        private static MainToolbarElement OnGUI()
        {
            // Text
            string text;
            if (TryGetLocales(out var locales))
                text = $"Localization : {locales[CurrentLocalIndex]}";
            else
                text = "Localization : None";

            // Icon
            var icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Packages/com.unity.localization/Editor/Icons/Locale/{ToolbarUtility.DarkModePrefix}Locale.png");

            MainToolbarContent content = new MainToolbarContent(text, tooltip: string.Empty, image: icon);
            MainToolbarDropdown dropdown = new MainToolbarDropdown(content, OpenDropdown);

            // Init
            EditorApplication.delayCall += SetupToolbarElement;
            
            return dropdown;
        }

        /// <summary>
        /// Sets up the toolbar element for the localization dropdown in the main toolbar window.
        /// </summary>
        /// <remarks>
        /// This method waits for the next frame to ensure that the UI elements are properly initialized.
        /// It attempts to locate the main toolbar window, and if successful, initializes the toolbar element provided by it.
        /// </remarks>
        private static void SetupToolbarElement()
        {
            if (ToolbarSystem.TryFindMainToolbarWindow(out var window)) 
                Init(window);
        }

        /// <summary>
        /// Displays a dropdown menu for selecting localization options.
        /// </summary>
        /// <param name="rect">The position and dimensions of the dropdown menu.</param>
        private static void OpenDropdown(Rect rect)
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
            
            menu.DropDown(rect);
        }

        /// <summary>
        /// Attempts to get the localization dropdown from the toolbar.
        /// </summary>
        /// <param name="element">The root visual element.</param>
        /// <param name="dropdown">The found dropdown, if successful.</param>
        /// <returns>True if the dropdown was found, false otherwise.</returns>
        private static bool TryGetDropdown(VisualElement element, out EditorToolbarDropdown dropdown)
        {
            var parent = element.Q<VisualElement>("NKToolbar/LocalizationSwitcher");

            if (parent == null)
            {
                dropdown = null;
                return false;
            }

            dropdown = parent.Q<EditorToolbarDropdown>();

            if (dropdown != null)
                return true;

            return false;
        }

#endif
    }
}
#endif