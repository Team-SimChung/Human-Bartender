using System;
using System.Collections.Generic;

namespace HumanBartender.CutsceneStudio
{
    /// <summary>스토리 시간·입력 대사·대기 중 반복 위상을 소유한다. Seek는 대기 이력을 초기화한다.</summary>
    public sealed class StudioClock
    {
        private const double Epsilon = 1e-8;
        private readonly HashSet<StudioClip> passed = new();
        private readonly HashSet<StudioClip> suppressed = new();
        private readonly Dictionary<StudioClip, double> offsets = new();
        private readonly List<StudioEntry> entries = new();
        private StudioSequence sequence;
        public double Time { get; private set; }
        public StudioEntry? Waiting { get; private set; }
        public StudioEntry? Dialogue { get; private set; }
        public double DialogueElapsed { get; private set; }
        public bool Finished { get; private set; }
        public int Revision { get; private set; }
        // 음원 위치를 바꾸는 탐색과 구분한다. 전체 표시 입력에서는 타이핑 효과음을 몰아 재생하지 않는다.
        internal int RevealRevision { get; private set; }
        public bool IsHolding => Waiting.HasValue || Dialogue.HasValue && Time >= Dialogue.Value.Clip.end - Epsilon;
        public bool CanAdvance => Waiting.HasValue || Dialogue.HasValue;
        public bool IsSuppressed(StudioClip clip) => suppressed.Contains(clip);
        public double DisplayTime => Dialogue.HasValue ? Math.Min(Time, Math.Max(Dialogue.Value.Clip.start, Dialogue.Value.Clip.end - .000001)) : Time;
        public double MotionTime(StudioEntry entry, double storyTime) => storyTime + (offsets.TryGetValue(entry.Asset, out var extra) ? extra : 0);

        public void Seek(double time)
        {
            Time = Math.Max(0, time); Waiting = Dialogue = null; DialogueElapsed = 0;
            Revision++; Finished = false; passed.Clear(); suppressed.Clear(); offsets.Clear();
        }

        // 첫 입력은 전체 표시만. 다음 입력은 현재 대사의 끝으로 이동한다.
        public void Advance()
        {
            if (Waiting.HasValue)
            {
                var entry = Waiting.Value;
                passed.Add(entry.Asset);
                if (entry.Asset.Kind == StudioKind.Dialogue)
                {
                    double destination = entry.Clip.end;
                    foreach (var other in entries)
                    {
                        if (other.Asset == entry.Asset || other.Clip.start < entry.Clip.start - Epsilon || other.Clip.start >= destination - Epsilon) continue;
                        if (other.Asset.Kind is StudioKind.Dialogue or StudioKind.Wait)
                        { passed.Add(other.Asset); suppressed.Add(other.Asset); }
                    }
                    // 계속 재생 중인 반복 연출은 점프한 시간만큼 앞당기지 않는다.
                    foreach (var other in entries)
                        if (other.ContinuesDuringWait && other.Contains(DisplayTime) && other.Contains(destination))
                            AddOffset(other.Asset, Time - destination);
                    Time = destination; Revision++;
                    Dialogue = null; DialogueElapsed = 0;
                }
                Waiting = null;
            }
            else if (Dialogue.HasValue)
            {
                RevealRevision++;
                DialogueElapsed = StudioDialogueTiming.For(Dialogue.Value.Asset, sequence).Duration;
                Waiting = Dialogue;
            }
        }

        public void Tick(StudioSequence value, double delta, bool gates)
        {
            sequence = value;
            StudioEvaluation.CollectClips(sequence, entries);
            if (Finished) return;
            delta = Math.Max(0, delta);
            if (!gates)
            {
                Dialogue = Waiting = null; DialogueElapsed = 0;
                Time = Math.Min(sequence.Duration, Time + delta);
                Finished = Time >= sequence.Duration;
                return;
            }
            for (int guard = 0; guard < entries.Count * 2 + 8; guard++)
            {
                if (Waiting.HasValue) { Hold(delta); return; }
                if (Dialogue.HasValue)
                {
                    var line = Dialogue.Value;
                    double duration = StudioDialogueTiming.For(line.Asset, sequence).Duration;
                    double take = Math.Min(delta, Math.Max(0, duration - DialogueElapsed));
                    var marker = NextGate(Time, Math.Min(line.Clip.end, Time + take), true);
                    if (marker.HasValue) take = Math.Max(0, marker.Value.Clip.start - Time);
                    double moving = Math.Min(take, Math.Max(0, line.Clip.end - Time));
                    Time += moving; DialogueElapsed += take; delta -= take;
                    // 짧은 클립도 끝에 머물며 남은 글자를 표시한다.
                    if (take > moving) Hold(take - moving);
                    if (marker.HasValue) { Waiting = marker; continue; }
                    if (DialogueElapsed + Epsilon >= duration) { Waiting = Dialogue; continue; }
                    return;
                }

                double next = Math.Min(sequence.Duration, Time + delta);
                var gate = NextGate(Time, next, false);
                if (gate.HasValue)
                {
                    double at = Math.Max(Time, gate.Value.Clip.start);
                    delta = Math.Max(0, delta - (at - Time)); Time = at;
                    if (gate.Value.Asset.Kind == StudioKind.Wait) Waiting = gate;
                    else { Dialogue = gate; DialogueElapsed = Math.Max(0, Time - gate.Value.Clip.start); }
                    continue;
                }
                Time = next; Finished = Time >= sequence.Duration; return;
            }
        }

        private StudioEntry? NextGate(double from, double to, bool onlyWait)
        {
            foreach (var entry in entries)
            {
                if (passed.Contains(entry.Asset)) continue;
                bool marker = entry.Asset.Kind == StudioKind.Wait;
                if (!marker && (onlyWait || entry.Asset.Kind != StudioKind.Dialogue || !entry.Asset.WaitForInput)) continue;
                if (entry.Clip.start > to + Epsilon) continue;
                if (entry.Clip.start < from - Epsilon && (marker || entry.Clip.end <= from)) continue;
                return entry;
            }
            return null;
        }

        private void Hold(double delta)
        {
            if (delta <= 0) return;
            foreach (var entry in entries)
                if (entry.ContinuesDuringWait && entry.Contains(DisplayTime)) AddOffset(entry.Asset, delta);
        }
        private void AddOffset(StudioClip clip, double delta) => offsets[clip] = (offsets.TryGetValue(clip, out var old) ? old : 0) + delta;
    }
}
