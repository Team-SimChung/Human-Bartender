using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private static readonly List<string> MotionCurveNames = new()
        { "사용자 지정", "선형", "부드럽게 시작", "부드럽게 종료", "부드럽게 시작 / 종료" };

        private void BuildMotionPlayback(VisualElement parent, SerializedObject data, bool holdEnd = true)
        {
            string kind = host.Selected.Kind.ToString().ToLowerInvariant();
            var preset = new PopupField<string>("곡선 프리셋", MotionCurveNames, 0) { name = kind + "-curve-preset" };
            parent.Add(preset);
            ClipField(parent, data, "Curve");
            if (holdEnd) ClipField(parent, data, "HoldEnd");
            preset.RegisterValueChangedCallback(e =>
            {
                int index = MotionCurveNames.IndexOf(e.newValue);
                if (index <= 0) return;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("진행 곡선 프리셋 변경");
                using var serialized = new SerializedObject(host.Selected);
                StudioSerializedFields.Find(serialized, "Curve").animationCurveValue = MotionCurve(index);
                if (serialized.ApplyModifiedProperties()) host.Changed();
                Undo.IncrementCurrentGroup();
            });
            refreshFields.Add(Refresh);
            Refresh();
            void Refresh() { preset.SetValueWithoutNotify(MotionCurveNames[MotionCurveIndex(host.Selected.Curve)]); }
        }

        private static AnimationCurve MotionCurve(int index) => index switch
        {
            1 => AnimationCurve.Linear(0, 0, 1, 1),
            2 => new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2)),
            3 => new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0)),
            _ => AnimationCurve.EaseInOut(0, 0, 1, 1)
        };
        private static int MotionCurveIndex(AnimationCurve curve)
        {
            if (curve == null) return 1;
            for (int index = 1; index < MotionCurveNames.Count; index++)
            {
                var expected = MotionCurve(index).keys; var actual = curve.keys;
                if (actual.Length != expected.Length) continue;
                bool same = true;
                for (int i = 0; i < actual.Length; i++)
                    same &= actual[i].weightedMode == WeightedMode.None && Mathf.Approximately(actual[i].time, expected[i].time) &&
                        Mathf.Approximately(actual[i].value, expected[i].value) && Mathf.Approximately(actual[i].inTangent, expected[i].inTangent) &&
                        Mathf.Approximately(actual[i].outTangent, expected[i].outTangent);
                if (same) return index;
            }
            return 0;
        }

        private void AddPoseCapture(VisualElement heading, TimelineClip clip, bool end)
        {
            bool camera = host.Selected.Kind == StudioKind.Camera;
            bool dialogue = host.Selected.Kind == StudioKind.Dialogue;
            bool background = host.Selected.Kind == StudioKind.Background;
            bool sampleClip = dialogue || background;
            string target = camera ? "카메라 구도" : dialogue ? "말풍선 상태" : background ? "배경 상태" : host.Selected.Kind == StudioKind.Visual ? "오브젝트 상태" : "액터 상태";
            var button = StudioGUI.ActionButton(camera ? "현재 구도 가져오기" : "현재 값 가져오기", Capture,
                "capture-" + host.Selected.Kind.ToString().ToLowerInvariant() + "-" + (end ? "end" : "start"));
            button.tooltip = "현재 " + target + "를 " + (end ? "도착점" : "시작점") + "에 기록합니다. 중간 키는 유지됩니다." +
                (dialogue ? " 이동은 상대값으로 기록하며 화자의 기준 위치와 미기록 입력은 포함하지 않습니다." :
                background ? " 이 배경 클립의 키 값을 사용하며 카메라 변형과 미기록 입력은 포함하지 않습니다." : "");
            heading.Add(button);
            refreshFields.Add(() => button.SetEnabled(CanRecord(clip) && (camera || sampleClip || host.ActorIndex >= 0)));
            void Capture()
            {
                if (!CanRecord(clip)) return;
                if (!camera && !sampleClip && host.ActorIndex < 0) return;
                // 말풍선 / 배경은 화자나 카메라 변형을 제외한 클립 자체의 키 값을 기록함.
                var pose = sampleClip ? StudioKeyframes.Sample(host.Selected, (float)(host.CurrentTime - clip.start), clip.duration) :
                    host.CapturePose(camera ? null : host.Selected.ActorId);
                if (pose == null) return;
                if (!camera && !sampleClip)
                {
                    float local = (float)(host.CurrentTime - clip.start);
                    float rotation = StudioKeyframes.Evaluate(host.Selected, StudioProperty.Rotation, local, clip.duration);
                    // Transform의 0..360도 표현이 음수 각도나 여러 바퀴 회전 의도를 바꾸지 않게 함.
                    pose.Rotation = rotation + Mathf.DeltaAngle(rotation, pose.Rotation);
                    // 완전히 투명한 공통 색상에서는 표시 알파로 클립 불투명도를 복원할 수 없음.
                    if (host.Sequence.FindActor(host.Selected.ActorId).Color.a < .001f)
                        pose.Opacity = StudioKeyframes.Evaluate(host.Selected, StudioProperty.Opacity, local, clip.duration);
                }
                var values = new Dictionary<StudioProperty, float>();
                foreach (var property in StudioKeyframes.Properties(host.Selected))
                    values.Add(property, StudioKeyframes.Value(pose, property));
                if (!StudioCurrentKeyEditing.Write(clip, end ? (float)clip.duration : 0, values, target + " 가져오기")) return;
                currentKeys.ClearPending(); host.Session.ClearKey(); currentKeys.Observe(clip, host.CurrentTime);
                host.Changed(); SyncTimingFields();
            }
        }
    }
}
