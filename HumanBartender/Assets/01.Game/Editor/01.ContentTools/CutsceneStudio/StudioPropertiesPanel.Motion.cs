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

    }
}
