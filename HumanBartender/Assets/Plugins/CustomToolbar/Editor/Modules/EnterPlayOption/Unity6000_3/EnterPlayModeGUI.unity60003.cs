using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static partial class EnterPlayModeGUI
    {
#if UNITY_6000_3_OR_NEWER

        /// <summary>
        /// Sets the enabled state of the Enter Play Mode toggle.
        /// </summary>
        /// <param name="element">The MainToolbarWindow root visual element.</param>
        /// <param name="enable">Whether the toggle should be enabled.</param>
        public static void SetEnable(VisualElement element, bool enable)
        {
            if (element == null)
                return;

            if (TryGetToggle(element, out EditorToolbarToggle toggle)) 
                toggle.SetEnabled(enable);
        }

        [MainToolbarElement("NK Toolbar/Enter Play Mode", defaultDockPosition = MainToolbarDockPosition.Left,
            defaultDockIndex = 13)]
        [UsedImplicitly]
        private static MainToolbarElement OnGUI()
        {
            // Text
            const string text = "Enter Play Mode";

            // Icon
            var iconPath = AssetDatabase.GUIDToAssetPath("c0b301a9175a74304992d875f40f670d");
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/{ToolbarUtility.DarkModePrefix}rush.png");

            MainToolbarContent content = new MainToolbarContent(text, tooltip: string.Empty, image: image);

            // Toggle
            bool initValue = EditorSettings.enterPlayModeOptionsEnabled;
            MainToolbarToggle toggle = new MainToolbarToggle(content, initValue, OnValueChanged);

            return toggle;
        }

        /// <summary>
        /// Handles value changes of the Enter Play Mode toggle.
        /// </summary>
        /// <param name="newValue">The new toggle value.</param>
        private static void OnValueChanged(bool newValue)
        {
            EditorSettings.enterPlayModeOptionsEnabled = newValue;
        }

        /// <summary>
        /// Attempts to get the Enter Play Mode toggle from the toolbar.
        /// </summary>
        /// <param name="element">The root visual element.</param>
        /// <param name="toggle">The found toggle, if successful.</param>
        /// <returns>True if the toggle was found, false otherwise.</returns>
        private static bool TryGetToggle(VisualElement element, out EditorToolbarToggle toggle)
        {
            var parent = element.Q<VisualElement>("NKToolbar/EnterPlayMode");

            if (parent == null)
            {
                toggle = null;
                return false;
            }

            toggle = parent.Q<EditorToolbarToggle>();

            if (toggle == null)
                return false;
            
            return true;
        }
#endif
    }
}