using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio
{
    // 제작 참고 사항이다. Validate/IsPrepared/ExportPrefab의 재생 오류 목록에 합치지 않는다.
    public static class StudioSequenceWarnings
    {
        public static List<string> Collect(StudioSequence sequence)
        {
            var result = new List<string>();
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
    }
}
