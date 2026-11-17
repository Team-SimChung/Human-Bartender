using System;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    public enum StudioDialogueLengthMode
    {
        [InspectorName("수동")] Manual,
        [InspectorName("자동 — 이 대사만")] Automatic,
        [InspectorName("자동 — 뒤 클립도 이동")] Ripple
    }

    public enum StudioTypingAudioMode
    {
        [InspectorName("시퀀스 공통 사용")] Inherit,
        [InspectorName("끔")] Off,
        [InspectorName("직접 지정")] Custom
    }

    [Serializable]
    public sealed class StudioTypingAudioSettings
    {
        [Tooltip("기존 제작 파일은 꺼진 상태로 유지됩니다."), InspectorName("사용")]
        public bool Enabled;
        [Tooltip("글자가 나타날 때 재생할 프로젝트 음원. 비어 있으면 무음입니다."), InspectorName("음원")]
        public AudioClip Sound;
        [Range(0, 1), InspectorName("음량")] public float Volume = 1;
        [Min(0), Tooltip("한 번에 여러 글자가 표시되어도 효과음은 한 번만 재생합니다."), InspectorName("최소 간격 (초)")]
        public float MinimumInterval = .06f;
        [InspectorName("공백에서 재생")] public bool Whitespace;
        [InspectorName("문장부호에서 재생")] public bool Punctuation;
        [Tooltip("대사 입력을 기다리는 동안 같은 음원을 지정 간격으로 재생합니다."), InspectorName("입력 대기 중 반복")]
        public bool RepeatWhileWaiting;
        internal const float MinimumWaitInterval = .01f;
        [Min(MinimumWaitInterval), InspectorName("대기 반복 간격 (초)")] public float WaitInterval = .25f;

        internal bool IsReady => Enabled && Sound != null;
        internal double Interval => StudioExtensionValues.NonNegative(MinimumInterval);
        internal double WaitingInterval => Math.Max(MinimumWaitInterval, Math.Max(Interval, StudioExtensionValues.NonNegative(WaitInterval)));
        internal bool Accepts(string character)
        {
            if (string.IsNullOrEmpty(character) || character == "\uFFFC") return false;
            if (char.IsWhiteSpace(character, 0)) return Whitespace;
            if (char.IsPunctuation(character, 0)) return Punctuation;
            return true;
        }
    }
}
