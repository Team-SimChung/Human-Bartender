using System;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildWaitSettings(VisualElement section)
        {
            Note(section, "클립 시작에서 재생을 멈추고, 진행 입력을 받으면 이어서 재생합니다. 키프레임은 사용하지 않습니다.");
            ReadOnly(section, "대기 시점", "wait-trigger").value = "클립 시작";
            ReadOnly(section, "대기 해제", "wait-release").value = "진행 입력";
            ReadOnly(section, "미리보기 입력", "wait-input").value = "E / Enter";
            var gameInput = ReadOnly(section, "게임 기본 입력", "wait-runtime-input");
            gameInput.tooltip = "기본 컷씬 재생기의 입력 / 게임 옵션에서 지정한 상호작용 키를 표시합니다.";
            refreshFields.Add(() => gameInput.SetValueWithoutNotify(GameKeyBindings.DisplayName(GameControl.Interact) + " / Enter"));
            Note(section, "진행 입력 전까지 대기를 유지합니다. 클립 길이를 줄여도 자동으로 진행하지 않습니다.");
        }

        private void BuildWaitPosition(VisualElement section, TimelineClip clip)
        {
            var current = ReadOnly(section, "현재 시점 (초)", "wait-current-time");
            var trigger = ReadOnly(section, "대기 시작 (초)", "wait-start-time");
            var relation = ReadOnly(section, "재생 헤드 위치", "wait-position");
            var trackState = new Label { name = "wait-track-state" };
            trackState.AddToClassList("studio-properties-note"); section.Add(trackState);
            var seek = StudioGUI.ActionButton("대기 시점으로 이동", () =>
            {
                if (host.Playing) return;
                host.Seek(clip.start); SyncTimingFields();
            }, "wait-seek-start");
            var move = StudioGUI.ActionButton("현재 시점을 대기 시작으로 적용", MoveToCurrent, "wait-use-current");
            move.tooltip = "클립 길이를 유지하면서 시작 시간을 옮깁니다. 키프레임을 추가하는 기능이 아닙니다.";
            section.Add(seek); section.Add(move);
            Note(section, "미리보기의 입력 대기 적용이 켜져 있어야 멈춥니다. 재생 헤드를 이동하는 탐색은 입력 대기를 실행하지 않습니다.");
            refreshFields.Add(Refresh);
            Refresh();

            void MoveToCurrent()
            {
                if (host.Playing || !StudioPropertyEditing.IsFinite(host.CurrentTime)) return;
                double at = Math.Max(0, host.Snap(host.CurrentTime));
                if (!StudioPropertyEditing.IsFinite(at) || Math.Abs(at - clip.start) < .000001) return;
                Undo.IncrementCurrentGroup();
                if (StudioPropertyEditing.SetTiming(host.Sequence, clip, at, clip.duration)) host.Changed();
                Undo.IncrementCurrentGroup();
                SyncTimingFields();
            }

            void Refresh()
            {
                bool valid = StudioPropertyEditing.IsFinite(host.CurrentTime);
                current.SetValueWithoutNotify(valid ? host.CurrentTime.ToString("0.###") : "—");
                trigger.SetValueWithoutNotify(clip.start.ToString("0.###"));
                double difference = host.CurrentTime - clip.start;
                relation.SetValueWithoutNotify(!valid ? "확인할 수 없음" : Math.Abs(difference) < .000001 ? "대기 시작 시점" :
                    difference < 0 ? "대기 시작 이전" : "대기 시작 이후");
                bool muted = clip.GetParentTrack().mutedInHierarchy;
                trackState.text = "비활성 트랙 / 재생 중 입력 대기를 실행하지 않습니다.";
                trackState.style.display = muted ? DisplayStyle.Flex : DisplayStyle.None;
                seek.SetEnabled(!host.Playing);
                double at = valid ? Math.Max(0, host.Snap(host.CurrentTime)) : double.NaN;
                move.SetEnabled(!host.Playing && StudioPropertyEditing.IsFinite(at) && Math.Abs(at - clip.start) >= .000001);
            }
        }
    }
}
