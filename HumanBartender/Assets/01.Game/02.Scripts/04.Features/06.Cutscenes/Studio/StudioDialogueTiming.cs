using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    [Serializable]
    public sealed class StudioTypingRhythm
    {
        [Tooltip("웹과 동일한 단독 마침표 뒤 휴식(초). 0이면 추가 휴식 없음."), Min(0)]
        [InspectorName("마침표 뒤 휴식 (초)")] public float PeriodPause = .35f;
        [Tooltip("연속 마침표 사이의 간격(초)."), Min(0)]
        [InspectorName("연속 마침표 간격 (초)")] public float RepeatedPeriodPause = .3f;
        [Tooltip("추가 휴식을 적용할 문자. 기본은 비어 있음. 예: ,!?…")]
        [InspectorName("추가 문장부호")] public string ExtraPunctuation = "";
        [Tooltip("추가로 지정한 문장부호 뒤 휴식(초)."), Min(0)]
        [InspectorName("추가 문장부호 휴식 (초)")] public float ExtraPause = .35f;
        internal static readonly StudioTypingRhythm Default = new();
    }

    // 표시 수, 입력 대기, 길이 맞추기가 공유하는 글자별 시각. 서식 태그는 글자로 세지 않는다.
    public sealed class StudioDialogueTiming
    {
        private static readonly Regex Tag = new(@"^/?(?:b|i|u|s|color|size|font|material|align|alpha|voffset|cspace|mspace|indent|line-indent|line-height|margin|mark|nobr|page|pos|rotate|space|style|sub|sup|width|link|lowercase|uppercase|smallcaps|font-weight|gradient|sprite|br|noparse)(?:[ =/]|$)", RegexOptions.IgnoreCase);
        private readonly double[] times;
        public double Duration { get; }
        public int Count => times.Length;
        internal double At(int index) => index < times.Length ? times[index] : Duration;
        private StudioDialogueTiming(double[] values, double duration) { times = values; Duration = duration; }

        public int Visible(double elapsed)
        {
            int lo = 0, hi = times.Length;
            while (lo < hi) { int mid = (lo + hi) / 2; if (times[mid] <= elapsed + 1e-9) lo = mid + 1; else hi = mid; }
            return lo;
        }

        public static StudioDialogueTiming For(StudioClip clip, StudioSequence sequence)
        {
            var rhythm = sequence != null ? sequence.TypingRhythm : StudioTypingRhythm.Default;
            string text = clip.ProjectedDialogue?.Text ?? clip.Text ?? "";
            var signature = (text, clip.CharactersPerSecond, rhythm.PeriodPause, rhythm.RepeatedPeriodPause,
                rhythm.ExtraPunctuation ?? "", rhythm.ExtraPause, clip.ProjectedDialogue);
            if (clip.TimingCache != null && Equals(clip.TimingSignature, signature)) return clip.TimingCache;
            clip.TimingSignature = signature;
            return clip.TimingCache = Build(clip, text, rhythm);
        }

        private static StudioDialogueTiming Build(StudioClip clip, string text, StudioTypingRhythm rhythm)
        {
            var chars = new List<(string text, int source)>();
            bool literal = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i + 1);
                    string tag = end > i ? text.Substring(i + 1, end - i - 1) : "";
                    if (tag.Equals("/noparse", StringComparison.OrdinalIgnoreCase)) { literal = false; i = end; continue; }
                    if (!literal && Tag.IsMatch(tag))
                    {
                        if (tag.Equals("noparse", StringComparison.OrdinalIgnoreCase)) literal = true;
                        if (tag.StartsWith("sprite", StringComparison.OrdinalIgnoreCase)) chars.Add(("\uFFFC", i));
                        else if (tag.Equals("br", StringComparison.OrdinalIgnoreCase) || tag.Equals("br/", StringComparison.OrdinalIgnoreCase)) chars.Add(("\n", i));
                        i = end; continue;
                    }
                }
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                chars.Add((text.Substring(i, length), i)); i += length - 1;
            }
            double period = StudioExtensionValues.NonNegative(rhythm.PeriodPause);
            double repeated = StudioExtensionValues.NonNegative(rhythm.RepeatedPeriodPause);
            double extra = StudioExtensionValues.NonNegative(rhythm.ExtraPause);
            double step = 1 / Math.Max(.01, StudioExtensionValues.NonNegative(clip.CharactersPerSecond, 22));
            var result = new double[chars.Count];
            double at = 0;
            var line = clip.ProjectedDialogue;
            int previousSource = -1;
            for (int i = 0; i < chars.Count; i++)
            {
                double interval = line != null && chars[i].source < line.SourceStyles.Length ? line.SourceStyles[chars[i].source].SpeedSeconds : step;
                string previous = i > 0 ? chars[i - 1].text : "";
                if (previous == "." && chars[i].text == "." && repeated > 0) interval = repeated;
                else if (previous == "." && (i < 2 || chars[i - 2].text != ".") && period > 0) interval = period;
                else if (previous.Length == 1 && (rhythm.ExtraPunctuation ?? "").Contains(previous)) interval += extra;
                if (line != null)
                    foreach (var wait in line.SourceWaits)
                        if (wait.Key > previousSource && wait.Key <= chars[i].source) at += wait.Value;
                at += Math.Max(0, interval); result[i] = at; previousSource = chars[i].source;
            }
            if (chars.Count > 0)
            {
                string last = chars[chars.Count - 1].text;
                if (last == "." && (chars.Count < 2 || chars[chars.Count - 2].text != ".")) at += period;
                else if (last.Length == 1 && (rhythm.ExtraPunctuation ?? "").Contains(last)) at += extra;
            }
            if (line != null) foreach (var wait in line.SourceWaits) if (wait.Key > previousSource) at += wait.Value;
            return new StudioDialogueTiming(result, at);
        }
    }
}
