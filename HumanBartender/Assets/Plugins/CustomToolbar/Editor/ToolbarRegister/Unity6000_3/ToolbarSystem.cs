#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEngine;
#if USE_LOCALIZATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif
using UnityEngine.UIElements;

namespace NKStudio
{
    [InitializeOnLoad]
    public static class ToolbarSystem
    {
        #region Fields

        private static VisualElement _mainToolBarWindow;

        #endregion

        #region Initialization

        /// <summary>
        /// Static constructor that subscribes to editor events.
        /// </summary>
        static ToolbarSystem()
        {
            SubscribeToEvents();
            EditorApplication.delayCall += OnInitialize;
        }

        private static void SubscribeToEvents()
        {
            // Unsubscribe first to prevent duplicate subscriptions
            UnsubscribeFromEvents();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorBuildSettings.sceneListChanged += OnSceneListChanged;
            
#if USE_LOCALIZATION
            LocalizationSettings.SelectedLocaleChanged += OnLocalizationChanged;
#endif
        }

        /// <summary>
        /// Unsubscribes from all editor events.
        /// </summary>
        private static void UnsubscribeFromEvents()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorBuildSettings.sceneListChanged -= OnSceneListChanged;
            
#if USE_LOCALIZATION
            LocalizationSettings.SelectedLocaleChanged -= OnLocalizationChanged;
#endif
        }

        /// <summary>
        /// Initializes the toolbar system.
        /// </summary>
        private static void OnInitialize()
        {
            if (TryFindMainToolbarWindow(out var window))
            {
                ToolbarEventHandler.OnInit(window);
                return;
            }

            Debug.LogWarning($"[{nameof(ToolbarSystem)}] Failed to find MainToolbarWindow");
        }

        /// <summary>
        /// Handles scene list changes in Build Settings.
        /// </summary>
        private static void OnSceneListChanged()
        {
            if (TryFindMainToolbarWindow(out var window))
                ToolbarEventHandler.SceneListChanged(window);
        }

#if USE_LOCALIZATION
        /// <summary>
        /// Handles localization changes.
        /// </summary>
        /// <param name="locale">The newly selected locale.</param>
        private static void OnLocalizationChanged(Locale locale)
        {
            if (TryFindMainToolbarWindow(out var window))
                ToolbarEventHandler.OnLocalizationChanged(window, locale);
        }
#endif

        /// <summary>
        /// Handles play mode state changes.
        /// </summary>
        /// <param name="state">The new play mode state.</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (TryFindMainToolbarWindow(out var window))
                ToolbarEventHandler.OnPlayModeChanged(window);
        }

        /// <summary>
        /// Attempts to find the main toolbar window.
        /// </summary>
        /// <returns>True if the toolbar window was found, false otherwise.</returns>
        public static bool TryFindMainToolbarWindow(out VisualElement window)
        {
            _mainToolBarWindow = FindMainToolBarWindow();
            window = _mainToolBarWindow;

            if (_mainToolBarWindow != null)
            {
                var center = _mainToolBarWindow.Q(className: "unity-overlay-container__middle-container");

                center.style.position = Position.Absolute;
                center.style.top = 0;
                center.style.bottom = 0;
                center.style.left = 0;
                center.style.right = 0;
                center.style.justifyContent = Justify.Center;

                return true;
            }

            return false;
        }

        #endregion

        #region Element Finding Utilities

        /// <summary>
        /// Finds the MainToolbarWindow visual element.
        /// </summary>
        /// <returns>The root visual element of the main toolbar, or null if not found.</returns>
        private static VisualElement FindMainToolBarWindow()
        {
            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.MainToolbarWindow");
            if (toolbarType == null) return null;
            Object[] toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars.Length == 0 || toolbars[0] is not EditorWindow toolbarWindow) return null;
            VisualElement root = toolbarWindow.rootVisualElement;

            return root;
        }

        #endregion
    }
}
#endif
