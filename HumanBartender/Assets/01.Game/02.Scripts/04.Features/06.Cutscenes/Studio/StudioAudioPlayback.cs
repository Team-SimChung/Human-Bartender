using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 재생 중 생성한 음원만 소유하며 대기·종료 시 함께 정리함.
    internal sealed class StudioAudioPlayback : IDisposable
    {
        private readonly GameObject owner;
        private readonly AudioOutputSettings output;
        private readonly StudioTypingAudio typing;
        private sealed class Voice
        {
            internal AudioSource Source;
            internal float Offset;
            internal double Start;
            internal bool Started, Paused;
            internal int Revision;
        }

        private readonly Dictionary<StudioClip, Voice> voices = new();
        private readonly HashSet<StudioClip> active = new();
        private readonly List<StudioClip> stale = new();
        private readonly List<StudioEntry> entries = new();
        internal float Speed { get; set; } = 1;
        internal StudioAudioPlayback(GameObject owner, AudioOutputSettings output)
        {
            this.owner = owner;
            this.output = output != null ? output : throw new ArgumentNullException(nameof(output));
            typing = new StudioTypingAudio(owner, this.output);
        }

        internal void Evaluate(StudioSequence sequence, double time, bool waiting, StudioClock clock = null, bool paused = false)
        {
            active.Clear();
            StudioEvaluation.CollectClips(sequence, entries);
            typing.Evaluate(sequence, entries, time, clock, paused);
            foreach (var entry in entries)
            {
                var clip = entry.Asset;
                if (clip.Kind != StudioKind.Audio || clip.Sound == null || !entry.Contains(time))
                    continue;
                double motion = clock != null && entry.ContinuesDuringWait ? clock.MotionTime(entry, time) : time;
                double offset = Math.Max(0, motion - entry.Clip.start + clip.AudioOffset);
                if (clip.Sound.length <= 0 || !clip.Loop && offset >= clip.Sound.length)
                    continue;
                active.Add(clip);
                if (!voices.TryGetValue(clip, out var voice))
                {
                    voice = CreateVoice();
                    voices.Add(clip, voice);
                }

                EvaluateVoice(voice, entry, time, offset, paused || waiting && !(clock != null && entry.ContinuesDuringWait), clock?.Revision ?? 0);
            }

            stale.Clear();
            foreach (var pair in voices)
            {
                if (active.Contains(pair.Key))
                    continue;
                Release(pair.Value.Source);
                stale.Add(pair.Key);
            }

            foreach (var key in stale)
                voices.Remove(key);
        }

        private Voice CreateVoice()
        {
            var voice = owner.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.ignoreListenerPause = true;
            return new Voice { Source = voice };
        }

        // 작성된 설정과 시간 계산을 미리보기·실제 재생에서 함께 사용함.
        private void EvaluateVoice(Voice voice, StudioEntry entry, double time, double offset, bool waiting, int revision)
        {
            var clip = entry.Asset;
            var source = voice.Source;
            float speed = Mathf.Clamp(Speed, .01f, 3f);
            bool changed = source.clip != clip.Sound || source.loop != clip.Loop ||
                voice.Offset != clip.AudioOffset || voice.Start != entry.Clip.start || source.pitch != speed || voice.Revision != revision;
            if (source.clip != clip.Sound)
            {
                source.Stop();
                source.clip = clip.Sound;
                voice.Started = false;
            }

            output.Route(source, clip.Bus);
            source.volume = StudioEvaluation.AudioGain(entry, time) * (clip.Bus == AudioBus.SoundEffects ? .55f : 1f);
            source.loop = clip.Loop;
            source.pitch = speed;
            voice.Offset = clip.AudioOffset;
            voice.Start = entry.Clip.start;
            if (waiting)
            {
                source.Pause();
                voice.Paused = true;
                return;
            }

            // 점프·설정 변경 때만 탐색한다. 설정창/입력 대기 복귀는 실제 재생 위치를 유지한다.
            if (!voice.Started || changed)
                source.time = (float)(offset % clip.Sound.length);
            if (!voice.Started)
                source.Play();
            else if (voice.Paused)
                source.UnPause();
            voice.Revision = revision;
            voice.Started = true;
            voice.Paused = false;
        }

        public void Dispose()
        {
            typing.Dispose();
            foreach (var voice in voices.Values)
                Release(voice.Source);
            voices.Clear();
            active.Clear();
            stale.Clear();
            entries.Clear();
        }

        private static void Release(AudioSource voice)
        {
            if (voice == null)
                return;
            voice.Stop();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(voice);
            else
                UnityEngine.Object.DestroyImmediate(voice);
        }
    }
}
