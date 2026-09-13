#if !UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio
{
    public static class ToolbarRegister
    {
        public static void OnDrawToolbarLeft(VisualElement element)
        {
#if UNITY_6000
            // Unity Preview 캡션 지우기
            Unity6CaptionGUI.RemoveCaption(element);
#endif
            // Enter Play Mode Option
            EnterPlayModeGUI.OnGUI(element);

#if USE_LOCALIZATION
            // Localization
            LocalizationGUI.OnGUI(element);
#endif

#if USE_FMOD
            // FMOD Debug
            FMODDebugGUI.OnGUI(element);
#endif
            
        }

        public static void OnDrawToolbarRight(VisualElement element)
        {
        }

        public static void OnDrawToolbarCenterLeft(VisualElement element)
        {
            // 0번째 씬에 이동
            SceneSwitchFirstSceneGUI.OnGUI(element);
        }

        public static void OnDrawToolbarCenterRight(VisualElement element)
        {
            // 씬 메뉴를 통해 이동
            SceneSwitchListMoveGUI.OnGUI(element);
        }
    }
}
#endif