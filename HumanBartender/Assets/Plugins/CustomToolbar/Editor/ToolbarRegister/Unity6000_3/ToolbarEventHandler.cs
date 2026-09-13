#if UNITY_6000_3_OR_NEWER
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if USE_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace NKStudio
{
    public static class ToolbarEventHandler
    {
        /// <summary>
        /// 커스텀 툴바가 동작되고 난 이후 1회 동작하는 Event 
        /// </summary>
        /// <param name="element">MainToolbarWindow</param>
        public static void OnInit(VisualElement element)
        {
            InitializeComponents(element);
        }

        private static void InitializeComponents(VisualElement element)
        {
#if UNITY_6000
            // Unity Preview 캡션 지우기
            // Unity6CaptionGUI.RemoveCaption(element);
#endif
            
#if USE_LOCALIZATION
            // Localization
            LocalizationGUI.Init(element);
#endif
            // SceneSwitcher
            SceneSwitcherGUI.Init(element);

#if USE_FMOD
            // FMOD Debug
            FMODDebugGUI.Init(element);
#endif
        }

        /// <summary>
        /// PlayMode에 변화가 생겼을 때 동작하는 Event
        /// </summary>
        /// <param name="element">MainToolbarWindow</param>
        public static void OnPlayModeChanged(VisualElement element)
        {
            bool isEnable = ToolbarUtility.IsNotPlaying;
            UpdateComponentsEnabledState(element, isEnable);
        }

        /// <summary>
        /// Updates the enabled state of all toolbar components.
        /// </summary>
        /// <param name="element">The MainToolbarWindow root visual element.</param>
        /// <param name="isEnabled">Whether components should be enabled.</param>
        private static void UpdateComponentsEnabledState(VisualElement element, bool isEnabled)
        {
            EnterPlayModeGUI.SetEnable(element, isEnabled);
            SceneSwitcherGUI.SetEnable(element, isEnabled);
#if USE_FMOD
            FMODDebugGUI.SetEnable(element, isEnabled);
#endif
        }

#if USE_LOCALIZATION
        /// <summary>
        /// Locale이 변경되었을 때 동작하는 Event
        /// </summary>
        /// <param name="element">MainToolbarWindow</param>
        /// <param name="locale">변경된 Locale</param>
        public static void OnLocalizationChanged(VisualElement element, Locale locale)
        {
            LocalizationGUI.UpdateText(element, locale);
        }
#endif

        /// <summary>
        /// 씬 리스트가 변경되었을 때 동작하는 Event
        /// </summary>
        /// <param name="element">MainToolbarWindow</param>
        public static void SceneListChanged(VisualElement element)
        {
            SceneSwitcherGUI.UpdateText(element);
        }
    }
}
#endif