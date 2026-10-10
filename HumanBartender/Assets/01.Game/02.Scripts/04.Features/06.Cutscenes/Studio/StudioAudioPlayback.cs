using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 재생 중 생성한 음원만 소유하며 대기·종료 시 함께 정리함.
    internal sealed class StudioAudioPlayback : IDisposable
    {
        private readonly GameObject owner;
        private readonly Dictionary<StudioClip, AudioSource> voices = new();
        private readonly HashSet<StudioClip> active = new();
        private readonly List<StudioClip> stale = new();
        private readonly List<StudioEntry> entries = new();
        internal StudioAudioPlayback(GameObject owner)
        {
            this.owner = owner;
        }

        internal void Evaluate(StudioSequence sequence, double time, bool waiting)
        {
            active.Clear();
            StudioEvaluation.CollectClips(sequence, entries);
            foreach (var entry in entries)
            {
                var clip = entry.Asset;
                if (clip.Kind != StudioKind.Audio || clip.Sound == null || !entry.Contains(time))
                    continue;
                active.Add(clip);
                if (!voices.TryGetValue(clip, out var voice))
                {
                    voice = CreateVoice(entry, time);
                    voices.Add(clip, voice);
                }

                voice.volume = StudioEvaluation.AudioGain(entry, time);
                if (waiting)
                    voice.Pause();
                else
                    voice.UnPause();
            }

            stale.Clear();
            foreach (var pair in voices)
            {
                if (active.Contains(pair.Key))
                    continue;
                Release(pair.Value);
                stale.Add(pair.Key);
            }

            foreach (var key in stale)
                voices.Remove(key);
        }

        private AudioSource CreateVoice(StudioEntry entry, double time)
        {
            var clip = entry.Asset;
            var voice = owner.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.ignoreListenerPause = true;
            voice.clip = clip.Sound;
            voice.loop = clip.Loop;
            double offset = Math.Max(0, time - entry.Clip.start + clip.AudioOffset);
            if (clip.Loop || offset < clip.Sound.length)
            {
                voice.time = (float)(offset % Math.Max(.001, clip.Sound.length));
                voice.Play();
            }

            return voice;
        }

        public void Dispose()
        {
            foreach (var voice in voices.Values)
                Release(voice);
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
            UnityEngine.Object.Destroy(voice);
        }
    }
}
