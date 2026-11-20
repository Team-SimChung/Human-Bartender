using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>NPC 연출 목록의 연결과 수치를 검사한다. 씬 상태나 저장 플래그는 변경하지 않는다.</summary>
internal static class NpcSequenceValidator
{
    internal static void ValidateSequences(NpcSequence[] sequences, IEnumerable<NpcActor> actors, string ownerName)
    {
        if (sequences == null || sequences.Any(value => value == null || string.IsNullOrWhiteSpace(value.id)))
            throw new InvalidOperationException("NPC 연출마다 ID를 입력하세요: " + ownerName);
        if (sequences.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() != sequences.Length ||
            sequences.Where(value => value.trigger == NpcSequenceTrigger.WorldAction).GroupBy(value => value.worldAction).Any(group => group.Count() > 1))
            throw new InvalidOperationException("NPC 연출 ID 또는 등록된 CSV 명령 연결이 중복되었습니다: " + ownerName);
        foreach (var sequence in sequences) ValidateActions(sequence, actors);
    }

    internal static void ValidateActions(NpcSequence sequence, IEnumerable<NpcActor> actors)
    {
        if (sequence.actions == null || sequence.actions.Any(value => value == null))
            throw new InvalidOperationException("NPC 연출 동작 목록이 비어 있거나 잘못 연결되었습니다: " + sequence.id);
        foreach (var step in sequence.actions)
        {
            if (!Enum.IsDefined(typeof(NpcActionKind), step.kind)) throw new InvalidOperationException("지원하지 않는 NPC 동작입니다.");
            if (step.performer != null && !actors.Contains(step.performer))
                throw new InvalidOperationException("동작을 실행할 NPC를 대표 또는 추가 Actor에 연결하세요: " + sequence.id);
            if (!Enum.IsDefined(typeof(NpcPositionOrigin), step.positionOrigin))
                throw new InvalidOperationException("NPC 이동의 기준 위치를 확인하세요: " + sequence.id);
            if (!Enum.IsDefined(typeof(NpcOffsetMode), step.offsetMode) || !float.IsFinite(step.unitsPerSecond) || step.unitsPerSecond < 0 ||
                !float.IsFinite(step.arcHeight) || step.arcHeight < 0 || !float.IsFinite(step.cameraWidth) || step.cameraWidth <= 0 ||
                !float.IsFinite(step.cameraOffset.x) || !float.IsFinite(step.cameraOffset.y) ||
                !float.IsFinite(step.fadeInTogether) || step.fadeInTogether < 0)
                throw new InvalidOperationException("NPC 이동·카메라 설정을 확인하세요: " + sequence.id);
            if (!Enum.IsDefined(typeof(NpcMotionEase), step.movementEase) || !Enum.IsDefined(typeof(NpcMotionEase), step.fadeEase))
                throw new InvalidOperationException("NPC 이동·페이드의 변화 방식을 확인하세요: " + sequence.id);
            if (!float.IsFinite(step.duration) || step.duration < 0 ||
                !float.IsFinite(step.walkDuration) || step.walkDuration < 0 ||
                !float.IsFinite(step.distance) || step.distance < 0 ||
                !float.IsFinite(step.offset.x) || !float.IsFinite(step.offset.y))
                throw new InvalidOperationException("NPC 동작의 시간·간격·이동량을 확인하세요: " + sequence.id);
            if ((step.kind is NpcActionKind.MoveTo or NpcActionKind.MovePlayer or NpcActionKind.ArcMove) && step.duration <= 0 && step.unitsPerSecond <= 0)
                throw new InvalidOperationException("NPC 이동 시간은 0보다 커야 합니다: " + sequence.id);
            if (step.kind == NpcActionKind.DepartWhileMoving &&
                (!float.IsFinite(step.walkDuration + step.duration) || step.walkDuration + step.duration <= 0))
                throw new InvalidOperationException("NPC 퇴장의 전체 이동 시간은 0보다 커야 합니다: " + sequence.id);
            if (step.kind == NpcActionKind.Animation && (step.duration <= 0 ||
                string.IsNullOrWhiteSpace(step.stateName) || string.IsNullOrWhiteSpace(step.returnState)))
                throw new InvalidOperationException("NPC 애니메이션의 상태 이름과 재생 시간을 설정하세요: " + sequence.id);
            if (step.kind == NpcActionKind.Sound && string.IsNullOrWhiteSpace(step.text))
                throw new InvalidOperationException("NPC 연출의 효과음 이름을 입력하세요: " + sequence.id);
        }
    }
}
