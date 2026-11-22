using System.Collections.Generic;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private string EffectOpacityLabel => StudioTimelinePanel.PropertyLabel(host.Selected, StudioProperty.Opacity);

        private void BuildCurrentEffectFields(VisualElement section, TimelineClip clip)
        {
            bool shake = host.Selected.Effect == StudioEffect.Shake;
            BuildCurrentFloat(section, clip, StudioProperty.Strength, "strength", "강도", "0은 효과 없음 / 1은 최대 강도입니다.");
            BuildCurrentFloat(section, clip, StudioProperty.Opacity, "opacity", EffectOpacityLabel,
                shake ? "1은 흔들림 유지 / 0은 흔들림 없음 / 강도와 곱해 적용됩니다." :
                    "0은 투명 / 1은 불투명 / 강도와 효과 색상의 알파 값이 함께 적용됩니다.");
        }

    }
}
