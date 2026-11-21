using System.Collections.Generic;
using System.Linq;

namespace HumanBartender.CutsceneStudio
{
    // 제작 참고 사항이다. Validate/IsPrepared/ExportPrefab의 재생 오류 목록에 합치지 않는다.
    public static class StudioSequenceWarnings
    {
        public static List<string> Collect(StudioSequence sequence)
        {
            var result = CollectMissingVisuals(sequence);
            var entries = StudioEvaluation.Clips(sequence);
            foreach (var line in entries)
            {
                if (line.Asset.Kind != StudioKind.Dialogue || !line.Asset.WaitForInput) continue;
                foreach (var other in entries)
                    if (other.Asset != line.Asset && (other.Asset.Kind is StudioKind.Wait or StudioKind.Dialogue) &&
                        other.Clip.start >= line.Clip.start && other.Clip.start < line.Clip.end)
                        result.Add($"{line.Clip.displayName}: 입력 후 끝으로 이동하면 겹친 '{other.Clip.displayName}'을 건너뛸 수 있습니다 ({other.Clip.start:F3}초). 재생은 가능합니다.");
                if (StudioDialogueTiming.For(line.Asset, sequence).Duration > line.Clip.duration)
                    result.Add($"{line.Clip.displayName}: 타이핑이 클립보다 깁니다. 끝 위치에서 글자를 마저 표시합니다. 필요하면 대사 길이 맞추기를 사용하세요.");
            }
            return result;
        }

        // 재생 시작 때 한 번 알린다. 매 프레임 평가나 TV 선택지 준비 검사에서는 로그를 반복하지 않는다.
        public static List<string> CollectMissingVisuals(StudioSequence sequence)
        {
            var result = new List<string>();
            foreach (var entry in StudioEvaluation.Clips(sequence))
            {
                var clip = entry.Asset;
                var actor = sequence.FindActor(clip.ActorId);
                string label = entry.Clip.displayName;
                if (clip.Kind == StudioKind.Visual)
                {
                    if (actor == null)
                        result.Add($"{label}: 그림 오브젝트 '{clip.ActorId}'가 없어 해당 그림만 표시하지 않습니다.");
                    else if (clip.Frames != null && clip.Frames.Length > 0)
                    {
                        if (clip.Frames.Any(frame => frame == null))
                            result.Add($"{label}: '{clip.ActorId}'의 애니메이션 이미지가 누락되어 해당 프레임만 표시하지 않습니다.");
                    }
                    else if (actor.Sprite == null && string.IsNullOrEmpty(actor.Caption) && !actor.HasRegion)
                        result.Add($"{label}: '{clip.ActorId}'의 이미지가 없어 해당 그림만 표시하지 않습니다.");
                }
                else if (clip.Kind == StudioKind.Dialogue && !string.IsNullOrEmpty(clip.ActorId) && actor == null)
                    result.Add($"{label}: 화자 오브젝트 '{clip.ActorId}'가 없어 기본 말풍선 위치를 사용합니다.");
                else if (clip.Kind == StudioKind.Camera && !string.IsNullOrEmpty(clip.CameraTargetId) &&
                         sequence.FindActor(clip.CameraTargetId) == null)
                    result.Add($"{label}: 카메라 대상 '{clip.CameraTargetId}'가 없어 대상 추적 없이 클립의 위치를 사용합니다.");
            }
            return result;
        }
    }
}
