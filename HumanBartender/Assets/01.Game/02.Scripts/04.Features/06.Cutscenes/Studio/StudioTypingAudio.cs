using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 글자 증가·대기 반복의 판정만 담당한다. 소리 누락분을 쌓아 두거나 다시 재생하지 않는다.
    internal sealed class StudioTypingCueState
    {
        private StudioClip previous;
        private int visible, revision, revealRevision;
        private bool suspended, wasWaiting;
        private double lastCue = double.NegativeInfinity, nextWait;

        internal bool Tick(StudioClip clip, StudioDialogueTiming timing, StudioTypingAudioSettings settings,
            int count, bool waiting, bool paused, double now, int seekRevision, int reveal)
        {
            bool reset = previous != clip || revision != seekRevision || revealRevision != reveal || now < lastCue;
            previous = clip; revision = seekRevision; revealRevision = reveal;
            if (clip == null || settings?.IsReady != true)
            {
                visible = count; suspended = true; wasWaiting = waiting; return false;
            }
            int before = visible;
            visible = count;
            if (reset || paused || suspended || count < before)
            {
                suspended = paused;
                wasWaiting = waiting;
                lastCue = double.NegativeInfinity;
                nextWait = now + settings.WaitingInterval;
                return false;
            }
            bool enteredWait = waiting && !wasWaiting;
            wasWaiting = waiting;
            if (enteredWait) nextWait = now + settings.WaitingInterval;
            bool glyph = timing.HasAudibleCharacter(before, count, settings);
            bool repeat = waiting && settings.RepeatWhileWaiting && now >= nextWait;
            if ((!glyph && !repeat) || now - lastCue < settings.Interval) return false;
            lastCue = now;
            nextWait = now + settings.WaitingInterval;
            return true;
        }
    }

    // 기존 오디오 출력·미리보기 수명을 사용하되 타이핑 전용 음원만 소유한다.
    internal sealed class StudioTypingAudio : IDisposable
    {
        private readonly GameObject owner;
        private readonly AudioOutputSettings output;
        private readonly StudioTypingCueState cues = new();
        private AudioSource source;
        private StudioClip previous;
        private bool sourcePaused;

        internal StudioTypingAudio(GameObject owner, AudioOutputSettings output) { this.owner = owner; this.output = output; }

        internal void Evaluate(StudioSequence sequence, List<StudioEntry> entries, double time, StudioClock clock, bool paused)
        {
            StudioEntry? line = null;
            foreach (var entry in entries)
                if (entry.Asset.Kind == StudioKind.Dialogue && entry.Contains(time) && clock?.IsSuppressed(entry.Asset) != true)
                    line = entry;
            if (clock?.Dialogue != null) line = clock.Dialogue;
            var clip = line?.Asset;
            if (previous != clip)
            {
                source?.Stop(); sourcePaused = false; previous = clip;
            }
            var settings = clip?.ResolveTypingAudio(sequence);
            bool enabled = settings?.IsReady == true;
            if (!enabled) { source?.Stop(); sourcePaused = false; }
            else if (paused && !sourcePaused) { source?.Pause(); sourcePaused = source != null; }
            else if (!paused && sourcePaused) { source?.UnPause(); sourcePaused = false; }

            var timing = clip != null ? StudioDialogueTiming.For(clip, sequence) : null;
            double elapsed = clip == null ? 0 : clock?.Dialogue?.Asset == clip ? clock.DialogueElapsed : time - line.Value.Clip.start;
            bool waiting = clip != null && clock?.Waiting?.Asset == clip;
            int count = timing?.Visible(elapsed) ?? 0;
            if (!cues.Tick(clip, timing, settings, count, waiting, paused, Time.realtimeSinceStartupAsDouble,
                    clock?.Revision ?? 0, clock?.RevealRevision ?? 0)) return;
            if (source == null)
            {
                source = owner.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true;
            }
            output.Route(source, AudioBus.SoundEffects);
            source.Stop();
            source.clip = settings.Sound;
            source.volume = Mathf.Clamp01(StudioExtensionValues.NonNegative(settings.Volume));
            source.Play();
        }

        public void Dispose()
        {
            if (source == null) return;
            source.Stop();
            if (Application.isPlaying) UnityEngine.Object.Destroy(source);
            else UnityEngine.Object.DestroyImmediate(source);
            source = null;
        }
    }
}
