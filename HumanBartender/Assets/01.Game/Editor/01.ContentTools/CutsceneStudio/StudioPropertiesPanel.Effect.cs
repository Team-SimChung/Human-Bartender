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

        private void BuildEffectEndpoints(VisualElement parent, TimelineClip clip)
        {
            Note(parent, "클립 양 끝의 효과 값 / 현재 시간과 무관하게 편집합니다. 값을 바꾸면 경계의 키를 기록하며 중간 키는 유지됩니다.");
            for (int i = 0; i < 2; i++)
            {
                bool end = i == 1;
                var group = new VisualElement(); group.AddToClassList("studio-properties-pose");
                var heading = new VisualElement(); heading.AddToClassList("studio-properties-pose-heading");
                var title = new Label(end ? "종료값" : "시작값"); title.AddToClassList("studio-properties-subtitle");
                heading.Add(title);
                var capture = StudioGUI.ActionButton("현재 값 가져오기", Capture, "capture-effect-" + (end ? "end" : "start"));
                capture.tooltip = "현재 시간에서 평가한 이 클립의 강도 / " + EffectOpacityLabel + "를 기록합니다. 미기록 입력과 다른 효과의 합성 결과는 포함하지 않습니다.";
                heading.Add(capture); group.Add(heading);
                var time = new Label(); time.AddToClassList("studio-properties-note"); group.Add(time);
                EndpointField(group, clip, StudioProperty.Strength, end, "강도");
                EndpointField(group, clip, StudioProperty.Opacity, end, EffectOpacityLabel);
                parent.Add(group);
                refreshFields.Add(() =>
                {
                    time.text = $"클립 내 {(end ? clip.duration : 0):0.###}초";
                    capture.SetEnabled(CanRecord(clip));
                });
                void Capture()
                {
                    if (!CanRecord(clip)) return;
                    float local = (float)(host.CurrentTime - clip.start);
                    var values = new Dictionary<StudioProperty, float>();
                    foreach (var property in StudioKeyframes.Properties(host.Selected))
                        values.Add(property, StudioKeyframes.Evaluate(host.Selected, property, local, clip.duration));
                    if (!StudioCurrentKeyEditing.Write(clip, end ? (float)clip.duration : 0, values, "효과 값 가져오기")) return;
                    currentKeys.ClearPending(); host.Session.ClearKey(); currentKeys.Observe(clip, host.CurrentTime);
                    host.Changed(); SyncTimingFields();
                }
            }
        }
    }
}
