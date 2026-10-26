using UnityEditor;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 저장은 공통 SerializedObject 바인딩, 페이드 표시는 공통 재생 평가기에 위임함.
    internal sealed partial class StudioPropertiesPanel
    {
        private void BuildAudioSource(VisualElement section, SerializedObject data)
        {
            ClipField(section, data, "Sound");
            ClipField(section, data, "Bus").label = "출력 채널";
            var asset = host.Selected;
            var length = ReadOnly(section, "원본 길이 (초)", "audio-length");
            refreshFields.Add(() => length.SetValueWithoutNotify(asset.Sound != null ? asset.Sound.length.ToString("0.###") : "오디오 없음"));
            Note(section, "효과음 / BGM 채널의 게임 음량 설정이 미리보기와 실제 재생에 함께 적용됩니다.");
        }

        private void BuildAudioPlayback(VisualElement section, SerializedObject data)
        {
            ClipField(section, data, "Volume", !StudioKeyframes.HasKeys(host.Selected, StudioProperty.Volume)).label = "기본 볼륨 (0–1)";
            ClipField(section, data, "AudioOffset").label = "원본 시작 (초)";
            ClipField(section, data, "Loop").label = "원본 반복";
            Note(section, "기본 볼륨은 볼륨 키가 없을 때 적용됩니다. 볼륨 키가 있으면 현재 시점 / 키프레임에서 편집하세요.");
            Note(section, "원본 시작은 오디오 파일 안의 재생 시작 위치입니다. 반복을 끄면 원본 끝에서 소리가 멈추며, 반복을 켜면 원본 길이를 기준으로 되풀이합니다.");
        }

        private void BuildAudioFade(VisualElement section, SerializedObject data)
        {
            ClipField(section, data, "FadeIn").label = "페이드 인 (초)";
            ClipField(section, data, "FadeOut").label = "페이드 아웃 (초)";
            Note(section, "클립 시작 / 종료를 기준으로 볼륨을 조절합니다. 0초는 즉시 적용 / 두 구간이 겹치면 더 낮은 볼륨을 사용합니다.");
        }

        private void BuildAudioPosition(VisualElement section, TimelineClip clip)
        {
            var current = ReadOnly(section, "현재 시점 (초)", "audio-current-time");
            var local = ReadOnly(section, "클립 내 시간 (초)", "audio-local-time");
            var position = ReadOnly(section, "재생 헤드 위치", "audio-position");
            var gain = ReadOnly(section, "페이드 적용 볼륨", "audio-current-gain");
            gain.tooltip = "클립 볼륨에 페이드를 적용한 계산값 / 채널별 음량과 게임 음량 설정 적용 전입니다.";
            Note(section, "읽기 전용 계산값 / 실제 출력 음량계가 아닙니다. 오디오 미지정, 비활성 트랙, 원본 재생 종료 상태에서는 소리가 나지 않습니다.");
            Note(section, "소리는 미리보기 재생 중에 나옵니다. 재생 헤드만 이동하면 재생하지 않으며, 입력 대기 중에는 일시 정지합니다. 소리가 없으면 Unity의 Mute Audio 설정을 확인하세요.");
            refreshFields.Add(Refresh);
            Refresh();

            void Refresh()
            {
                double time = host.CurrentTime;
                bool valid = StudioPropertyEditing.IsFinite(time);
                var entry = new StudioEntry(clip, host.Selected);
                bool inside = valid && entry.Contains(time);
                current.SetValueWithoutNotify(valid ? time.ToString("0.###") : "—");
                local.SetValueWithoutNotify(inside ? (time - clip.start).ToString("0.###") : "—");
                position.SetValueWithoutNotify(!valid ? "확인할 수 없음" : inside ? "클립 구간 안" : time < clip.start ? "클립 시작 이전" : "클립 종료 이후");
                gain.SetValueWithoutNotify(inside ? StudioEvaluation.AudioGain(entry, time).ToString("0.###") : "—");
            }
        }
    }
}
