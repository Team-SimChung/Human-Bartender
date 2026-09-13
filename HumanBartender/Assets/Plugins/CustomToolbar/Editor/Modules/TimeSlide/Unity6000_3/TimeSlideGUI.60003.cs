using JetBrains.Annotations;
using UnityEditor.Toolbars;
using UnityEngine;

namespace NKStudio
{
    internal static class TimeSlideGUI
    {
#if UNITY_6000_3_OR_NEWER
        private const float MinTimeScale = 0f;
        private const float MaxTimeScale = 5f;

        [MainToolbarElement("NK Toolbar/TimeSlider", defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 7)]
        [UsedImplicitly]
        private static MainToolbarElement OnGUI()
        {
            var content = new MainToolbarContent("Time Scale", "Time Scale");
            var slider = new MainToolbarSlider(content, Time.timeScale, MinTimeScale, MaxTimeScale, OnSliderValueChanged);
        
            // 오른쪽 마우스 옵션, Reset 추가
            slider.populateContextMenu = menu => {
                menu.AppendAction("Reset", _ => {
                    Time.timeScale = 1f;
                    MainToolbar.Refresh("NK Toolbar/TimeSlider");
                });
            };

            return slider;
        }

        private static void OnSliderValueChanged(float newValue) {
            Time.timeScale = newValue;
        }
#endif
    }
}