using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;

/// <summary>One immutable interpretation of a line. Source offsets are bound to TMP glyphs after layout.</summary>
public static class DialogueTextCompiler
{
    public struct Style
    {
        public float SpeedSeconds;
        public bool Shake, Wave, Pop;
        public float ShakeAmp, ShakeHz, WaveAmp, WaveHz, WavePhase, PopPeak, PopDuration;
    }

    public sealed class Line
    {
        public string Text { get; internal set; }
        internal Style[] SourceStyles;
        internal Dictionary<int, float> SourceWaits;
        public Style[] Characters { get; private set; }
        public float[] WaitBefore { get; private set; }
        public bool[] ContinuesPrevious { get; private set; }

        public void Bind(TMP_TextInfo info)
        {
            int count = info.characterCount;
            Characters = new Style[count];
            WaitBefore = new float[count + 1];
            ContinuesPrevious = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int source = info.characterInfo[i].index;
                if (source >= 0 && source < SourceStyles.Length)
                    Characters[i] = SourceStyles[source];
                if (i > 0 && source >= 0 && source < Text.Length)
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(Text, source);
                    ContinuesPrevious[i] = category is UnicodeCategory.NonSpacingMark or
                        UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark ||
                        Text[source] is '\u200d' or '\ufe0f' ||
                        source > 0 && Text[source - 1] == '\u200d';
                }
            }
            foreach (var wait in SourceWaits)
            {
                int boundary = count;
                for (int i = 0; i < count; i++)
                    if (info.characterInfo[i].index >= wait.Key) { boundary = i; break; }
                WaitBefore[boundary] += wait.Value;
            }
        }
    }

    struct Frame
    {
        public string Name;
        public Style Previous;
        public string TmpClose;
        public float PauseAfter;
    }

    const int MaxLength = 8192;
    const int MaxDepth = 32;
    static readonly char[] Spaces = { ' ' };

    public static bool TryCompile(string raw, NewTextTagDataSO tags, DialoguePresentationSettings settings,
        string cocktailName, float defaultSpeed, out Line line, out string error)
    {
        try
        {
            line = Compile(raw, tags, settings, cocktailName, defaultSpeed);
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            line = null;
            error = exception.Message;
            return false;
        }
    }

    public static Line Compile(string raw, NewTextTagDataSO tags, DialoguePresentationSettings settings,
        string cocktailName = null, float defaultSpeed = 0.05f)
    {
        raw ??= "";
        if (raw.Length > MaxLength) throw new ArgumentException($"Dialogue length exceeds {MaxLength}.");
        if (cocktailName != null && cocktailName.Length > MaxLength)
            throw new ArgumentException($"Cocktail name length exceeds {MaxLength}.");
        var output = new StringBuilder(raw.Length);
        var styles = new List<Style>(raw.Length);
        var waits = new Dictionary<int, float>();
        var stack = new List<Frame>();
        Style current = new() { SpeedSeconds = defaultSpeed };
        for (int i = 0; i < raw.Length;)
        {
            if (cocktailName != null && raw.AsSpan(i).StartsWith("{cocktail}".AsSpan(), StringComparison.Ordinal))
            {
                // Data values are text, never trusted markup.
                foreach (char ch in cocktailName)
                    Append(ch is '<' ? '＜' : ch is '>' ? '＞' : ch, current, output, styles);
                i += 10;
                continue;
            }
            if (raw[i] == '<')
            {
                int end = raw.IndexOf('>', i + 1);
                if (end > i)
                {
                    string token = raw.Substring(i + 1, end - i - 1);
                    if (TryDelay(token, out float wait))
                    {
                        if (waits.TryGetValue(output.Length, out float old)) wait += old;
                        waits[output.Length] = wait;
                        i = end + 1;
                        continue;
                    }
                    bool closing = token.StartsWith("/", StringComparison.Ordinal);
                    string head = (closing ? token.Substring(1) : token).Split(Spaces, 2)[0];
                    bool preset = !closing && token.StartsWith("fx=", StringComparison.Ordinal) || closing && token == "/fx";
                    if (preset) head = "fx";
                    bool alias = tags != null && tags.textTagData != null && tags.textTagData.TryGetValue(head, out _);
                    bool effect = head is "shake" or "wave" or "pop";
                    if (alias || effect || preset)
                    {
                        if (closing)
                        {
                            if (stack.Count == 0 || stack[stack.Count - 1].Name != head)
                                throw new ArgumentException($"Mismatched </{head}> at UTF-16 {i}.");
                            Frame frame = stack[stack.Count - 1];
                            stack.RemoveAt(stack.Count - 1);
                            if (frame.TmpClose != null) AppendTag(frame.TmpClose, output, styles);
                            AddWait(waits, output.Length, frame.PauseAfter);
                            current = frame.Previous;
                        }
                        else
                        {
                            if (stack.Count >= MaxDepth) throw new ArgumentException($"Tag depth exceeds {MaxDepth} at UTF-16 {i}.");
                            Style next = current;
                            string tmpOpen = null, tmpClose = null;
                            float pauseBefore = 0, pauseAfter = 0;
                            if (effect) ParseEffect(head, token, settings, ref next, i);
                            else if (preset) ParsePreset(token, settings, ref next,
                                out tmpOpen, out tmpClose, out pauseBefore, out pauseAfter, i);
                            else ParseAlias(head, tags.textTagData[head], ref next, out tmpOpen, out tmpClose, i);
                            stack.Add(new Frame { Name = head, Previous = current, TmpClose = tmpClose, PauseAfter = pauseAfter });
                            AddWait(waits, output.Length, pauseBefore);
                            if (tmpOpen != null) AppendTag(tmpOpen, output, styles);
                            current = next;
                        }
                        i = end + 1;
                        continue;
                    }
                    if (head is "shake" or "wave" or "pop" || head.StartsWith("fx", StringComparison.Ordinal))
                        throw new ArgumentException($"Invalid <{head}> at UTF-16 {i}.");
                    // Existing TMP tags, including color, size, sprite and br, remain TMP's responsibility.
                    AppendTag(raw.Substring(i, end - i + 1), output, styles);
                    i = end + 1;
                    continue;
                }
            }
            Append(raw[i], current, output, styles);
            i++;
        }
        if (stack.Count != 0) throw new ArgumentException($"Unclosed <{stack[stack.Count - 1].Name}>.");
        if (output.Length > MaxLength)
            throw new ArgumentException($"Expanded dialogue length exceeds {MaxLength}.");
        return new Line { Text = output.ToString(), SourceStyles = styles.ToArray(), SourceWaits = waits };
    }

    static void Append(char value, Style style, StringBuilder output, List<Style> styles)
    { output.Append(value); styles.Add(style); }

    static void AppendTag(string tag, StringBuilder output, List<Style> styles)
    { foreach (char ch in tag) Append(ch, default, output, styles); }

    static void AddWait(Dictionary<int, float> waits, int source, float seconds)
    {
        if (seconds <= 0) return;
        waits.TryGetValue(source, out float previous);
        waits[source] = previous + seconds;
    }

    static void ParsePreset(string token, DialoguePresentationSettings settings, ref Style style,
        out string open, out string close, out float pauseBefore, out float pauseAfter, int position)
    {
        string id = token.Substring(3);
        if (id.Length == 0 || !char.IsLower(id[0]))
            throw new ArgumentException($"Invalid FX preset name at UTF-16 {position}.");
        foreach (char ch in id)
            if (!(ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '_'))
                throw new ArgumentException($"Invalid FX preset name at UTF-16 {position}.");
        DialogueFxPreset fx = settings != null ? settings.GetPreset(id) :
            throw new ArgumentException($"No dialogue FX settings for '{id}' at UTF-16 {position}.");
        if (!Valid(fx.SizePercent) || fx.SizePercent < 1 || fx.SizePercent > 400 ||
            !Valid(fx.TypingIntervalMs) || fx.TypingIntervalMs < 0 || fx.TypingIntervalMs > 60000 ||
            !Valid(fx.PauseBeforeMs) || fx.PauseBeforeMs < 0 || fx.PauseBeforeMs > 60000 ||
            !Valid(fx.PauseAfterMs) || fx.PauseAfterMs < 0 || fx.PauseAfterMs > 60000 ||
            !Valid(fx.Amplitude) || fx.Amplitude < 0 || fx.Amplitude > 100 ||
            !Valid(fx.Frequency) || fx.Frequency < 0 || fx.Frequency > 120 ||
            fx.Motion is not (DialogueFxMotion.None or DialogueFxMotion.Shake or DialogueFxMotion.Wave))
            throw new ArgumentException($"Invalid FX preset '{id}' at UTF-16 {position}.");
        string color = fx.ColorHex;
        if (!string.IsNullOrEmpty(color))
        {
            if (color.Length != 7 || color[0] != '#')
                throw new ArgumentException($"Invalid FX color in '{id}' at UTF-16 {position}.");
            for (int c = 1; c < color.Length; c++)
                if (!Uri.IsHexDigit(color[c]))
                    throw new ArgumentException($"Invalid FX color in '{id}' at UTF-16 {position}.");
        }
        if (fx.OverrideSpeed) style.SpeedSeconds = fx.TypingIntervalMs / 1000f;
        if (fx.Motion == DialogueFxMotion.Shake)
        {
            style.Shake = true;
            style.ShakeAmp = fx.Amplitude;
            style.ShakeHz = fx.Frequency;
        }
        else if (fx.Motion == DialogueFxMotion.Wave)
        {
            style.Wave = true;
            style.WaveAmp = fx.Amplitude;
            style.WaveHz = fx.Frequency;
            style.WavePhase = settings.WavePhase;
        }
        open = close = null;
        if (!string.IsNullOrEmpty(color)) { open = $"<color={color}>"; close = "</color>"; }
        if (fx.SizePercent != 100)
        {
            open += "<size=" + fx.SizePercent.ToString(CultureInfo.InvariantCulture) + "%>";
            close = "</size>" + close;
        }
        pauseBefore = fx.PauseBeforeMs / 1000f;
        pauseAfter = fx.PauseAfterMs / 1000f;
    }

    static bool TryDelay(string token, out float seconds)
    {
        seconds = 0;
        if (token.Length == 0 || !char.IsDigit(token[0])) return false;
        foreach (char ch in token) if (ch < '0' || ch > '9') throw new ArgumentException($"Invalid delay <{token}>.");
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int ms) || ms > 60000)
            throw new ArgumentException($"Delay out of range <{token}>.");
        seconds = ms / 1000f;
        return true;
    }

    static void ParseAlias(string name, NewTextTagData tag, ref Style style,
        out string open, out string close, int position)
    {
        open = close = null;
        string value = tag.Value?.ToString();
        if (tag.Kind == "color")
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"Empty color <{name}> at {position}.");
            open = "<color=" + (value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value) + ">";
            close = "</color>";
        }
        else if (tag.Kind == "size_pct")
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float size) || !Valid(size) || size <= 0)
                throw new ArgumentException($"Invalid size <{name}> at {position}.");
            open = "<size=" + size.ToString(CultureInfo.InvariantCulture) + "%>";
            close = "</size>";
        }
        else if (tag.Kind == "speed_ms")
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ms) || !Valid(ms) || ms < 0 || ms > 60000)
                throw new ArgumentException($"Invalid speed <{name}> at {position}.");
            style.SpeedSeconds = ms / 1000f;
        }
        else throw new ArgumentException($"Unsupported alias <{name}> at {position}.");
    }

    static void ParseEffect(string name, string token, DialoguePresentationSettings settings, ref Style style, int position)
    {
        if (name == "shake")
        {
            style.ShakeAmp = settings != null ? settings.ShakeAmplitude : 1.5f;
            style.ShakeHz = settings != null ? settings.ShakeFrequency : 18f;
        }
        else if (name == "wave")
        {
            style.WaveAmp = settings != null ? settings.WaveAmplitude : 2f;
            style.WaveHz = settings != null ? settings.WaveFrequency : 2f;
            style.WavePhase = settings != null ? settings.WavePhase : 0.65f;
        }
        else
        {
            style.PopPeak = settings != null ? settings.PopPeak : 1.2f;
            style.PopDuration = settings != null ? settings.PopDuration : 0.12f;
        }
        string[] parts = token.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>();
        for (int p = 1; p < parts.Length; p++)
        {
            string[] attribute = parts[p].Split('=');
            if (attribute.Length != 2 || !seen.Add(attribute[0]) ||
                !float.TryParse(attribute[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !Valid(value) || value < 0)
                throw new ArgumentException($"Invalid <{name}> attribute at UTF-16 {position}.");
            switch (name + "." + attribute[0])
            {
                case "shake.amp": style.ShakeAmp = value; break;
                case "shake.hz": style.ShakeHz = value; break;
                case "wave.amp": style.WaveAmp = value; break;
                case "wave.hz": style.WaveHz = value; break;
                case "wave.phase": style.WavePhase = value; break;
                case "pop.peak": style.PopPeak = value; break;
                case "pop.duration": style.PopDuration = value; break;
                default: throw new ArgumentException($"Unknown <{name}> attribute '{attribute[0]}' at UTF-16 {position}.");
            }
        }
        if (name == "pop" && (style.PopPeak < 1 || style.PopPeak > 4 || style.PopDuration <= 0 || style.PopDuration > 10) ||
            name == "shake" && (style.ShakeAmp > 100 || style.ShakeHz > 120) ||
            name == "wave" && (style.WaveAmp > 100 || style.WaveHz > 120 || style.WavePhase > 100))
            throw new ArgumentException($"Invalid <{name}> range at UTF-16 {position}.");
        if (name == "shake") style.Shake = true;
        else if (name == "wave") style.Wave = true;
        else style.Pop = true;
    }

    static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
