using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>One bubble owns one current line, reveal count, skip, and holding animation.</summary>
[RequireComponent(typeof(DynamicSpeechBubble))]
public sealed class DialogueTextPlayer : MonoBehaviour
{
    sealed class Session
    {
        public int Id;
        public DialogueTextCompiler.Line Line;
        public CancellationTokenSource Reveal;
        public bool Skipped;
        public bool Revealing;
        public bool ReducedMotion;
        public bool ShakeSpanNotified;
        public bool[] VisibleGlyphs;
        public CancellationTokenRegistration ExternalRegistration;
        public CancellationTokenRegistration DestroyRegistration;
    }

    DynamicSpeechBubble bubble;
    DialogueTextAnimator animator;
    Session current;
    int nextId;
    static int warnings;
    ISoundManager sound;
    public void ConfigureAudio(ISoundManager manager) => sound = manager;

    void Awake()
    {
        bubble = GetComponent<DynamicSpeechBubble>();
        animator = GetComponent<DialogueTextAnimator>();
        if (animator == null) animator = gameObject.AddComponent<DialogueTextAnimator>();
    }

    public int CurrentSessionId => current?.Id ?? 0;
    public bool IsHolding => current != null && !current.Revealing;

    // Presentation cues describe the revealed text; subscribers choose the response.
    public event Action ShakeSpanRevealed;
    public event Action MotionStopped;

    public async UniTask PlayAsync(TypingData data, NewTextTagDataSO tags, float defaultDelay,
        CancellationToken external = default, string cocktailName = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        external.ThrowIfCancellationRequested();
        Stop();
        gameObject.SetActive(true);
        if (bubble == null) Awake();
        bubble.nameLabel.text = data.speaker;
        bubble.nameLabel.color = data.nameColor;
        var session = Begin(data.str, tags, cocktailName, defaultDelay, false);
        session.Revealing = true;
        session.ExternalRegistration = external.Register(() => Close(session));
        session.DestroyRegistration = this.GetCancellationTokenOnDestroy().Register(() => Close(session));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(external, this.GetCancellationTokenOnDestroy());
        session.Reveal = source;
        bool completed = false;
        try
        {
            var line = session.Line;
            int count = line.Characters.Length;
            for (int i = 0; i < count;)
            {
                await Wait(line.WaitBefore[i], source.Token);
                await Wait(line.Characters[i].SpeedSeconds, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(current, session)) throw new OperationCanceledException();
                int visible = i + 1;
                while (visible < count && line.ContinuesPrevious[visible]) visible++;
                bubble.textLabel.maxVisibleCharacters = visible;
                bubble.UpdateForVisible(visible);
                animator.SetVisible(visible, Time.time, false);
                if (visible == i + 1) DialogueTypingAudio.TryPlay(sound,
                    DialoguePresentationSettings.Shared, line.Text, bubble.textLabel.textInfo.characterInfo[i]);
                NotifyShakeSpan(session, i, visible);
                source.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(current, session)) throw new OperationCanceledException();
                i = visible;
            }
            await Wait(line.WaitBefore[count], source.Token);
            completed = true;
        }
        catch (OperationCanceledException) when (session.Skipped && ReferenceEquals(current, session) &&
                                                !external.IsCancellationRequested && !this.GetCancellationTokenOnDestroy().IsCancellationRequested)
        {
            ShowAll(session, true);
            completed = true;
        }
        finally
        {
            session.Reveal = null;
            session.Revealing = false;
            if (!completed || external.IsCancellationRequested || !ReferenceEquals(current, session))
                Close(session);
        }
    }

    public void ShowInstant(string text, NewTextTagDataSO tags = null, string cocktailName = null)
    {
        Stop();
        gameObject.SetActive(true);
        if (bubble == null) Awake();
        var session = Begin(text, tags, cocktailName, 0f, true);
        ShowAll(session, true);
    }

    Session Begin(string text, NewTextTagDataSO tags, string cocktailName, float speed, bool instant)
    {
        var settings = DialoguePresentationSettings.Shared;
        if (!DialogueTextCompiler.TryCompile(text, tags, settings, cocktailName, speed, out var line, out string error))
        {
            if (warnings++ < 5) Debug.LogWarning($"[DialogueFX] {error}; showing readable fallback.");
            line = DialogueTextCompiler.Compile(DialogueTextFallback.Readable(text), null, settings, null, speed);
        }
        bubble.PrepareForText(line.Text);
        // Bubble preparation may already rebuild with maxVisibleCharacters = 0.
        // Capture the full layout before hiding it again below, in the same synchronous call.
        bubble.textLabel.maxVisibleCharacters = int.MaxValue;
        bubble.textLabel.ForceMeshUpdate(true, true);
        line.Bind(bubble.textLabel.textInfo);
        var session = new Session
        {
            Id = ++nextId, Line = line,
            ReducedMotion = settings != null && settings.ReducedMotion,
            VisibleGlyphs = new bool[line.Characters.Length]
        };
        // Capture from the full layout: TMP can mark unrevealed glyphs invisible later.
        for (int i = 0; i < session.VisibleGlyphs.Length; i++)
        {
            var ch = bubble.textLabel.textInfo.characterInfo[i];
            session.VisibleGlyphs[i] = ch.isVisible && ch.elementType == TMP_TextElementType.Character;
        }
        current = session;
        try
        {
            bubble.textLabel.maxVisibleCharacters = instant ? line.Characters.Length : 0;
            bubble.UpdateForVisible(instant ? line.Characters.Length : 0);
            animator.Bind(bubble.textLabel, line, session.ReducedMotion);
            animator.SetVisible(instant ? line.Characters.Length : 0, Time.time, instant);
        }
        catch { Stop(); throw; }
        return session;
    }

    void NotifyShakeSpan(Session session, int first, int end)
    {
        if (session.ReducedMotion) return;
        for (int i = first; i < end; i++)
        {
            var style = session.Line.Characters[i];
            if (!style.Shake || style.ShakeAmp <= 0f || style.ShakeHz <= 0f)
            {
                session.ShakeSpanNotified = false;
                continue;
            }
            if (session.ShakeSpanNotified || !session.VisibleGlyphs[i]) continue;
            session.ShakeSpanNotified = true;
            ShakeSpanRevealed?.Invoke();
            if (!ReferenceEquals(current, session) || session.Skipped) return;
        }
    }

    static UniTask Wait(float seconds, CancellationToken token) => seconds <= 0f
        ? UniTask.CompletedTask
        : UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);

    void ShowAll(Session session, bool instant)
    {
        if (!ReferenceEquals(current, session)) return;
        int count = session.Line.Characters.Length;
        bubble.textLabel.maxVisibleCharacters = count;
        bubble.UpdateForVisible(count);
        animator.SetVisible(count, Time.time, instant);
    }

    public void Skip()
    {
        if (current == null || !current.Revealing || current.Skipped) return;
        var session = current;
        session.Skipped = true;
        MotionStopped?.Invoke();
        session.Reveal?.Cancel();
    }

    public void Stop()
    {
        var old = current;
        if (old == null) return;
        current = null;
        old.Reveal?.Cancel();
        old.ExternalRegistration.Dispose();
        old.DestroyRegistration.Dispose();
        animator?.Clear();
        MotionStopped?.Invoke();
    }

    void Close(Session session)
    {
        if (!ReferenceEquals(current, session)) return;
        Stop();
    }

    void OnDisable() => Stop();
    void OnDestroy() => Stop();
}

/// <summary>Shared SE channel budget; it never owns or stops other SoundManager effects.</summary>
static class DialogueTypingAudio
{
    static int lastFrame = -1;
    static float lastTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { lastFrame = -1; lastTime = -1f; }

    public static void TryPlay(ISoundManager sound, DialoguePresentationSettings settings,
        string text, TMP_CharacterInfo ch)
    {
        if (sound == null || settings == null || string.IsNullOrWhiteSpace(settings.TypingSoundKey) ||
            ch.elementType == TMP_TextElementType.Sprite || ch.index < 0 || ch.index >= text.Length ||
            !char.IsLetterOrDigit(text, ch.index) || Time.frameCount == lastFrame ||
            (lastTime >= 0f && Time.time - lastTime < 0.06f)) return;
        lastFrame = Time.frameCount;
        lastTime = Time.time;
        sound.PlaySE(settings.TypingSoundKey);
    }
}

static class DialogueTextFallback
{
    public static string Readable(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var text = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '<')
            {
                int end = raw.IndexOf('>', i + 1);
                if (end > i)
                {
                    string tag = raw.Substring(i + 1, end - i - 1);
                    if (tag.StartsWith("shake", StringComparison.Ordinal) || tag.StartsWith("/shake", StringComparison.Ordinal) ||
                        tag.StartsWith("wave", StringComparison.Ordinal) || tag.StartsWith("/wave", StringComparison.Ordinal) ||
                        tag.StartsWith("pop", StringComparison.Ordinal) || tag.StartsWith("/pop", StringComparison.Ordinal) ||
                        tag.StartsWith("fx=", StringComparison.Ordinal) || tag == "/fx")
                    { i = end; continue; }
                    if (tag is "slow" or "/slow" or "fast" or "/fast" or "big" or "/big" or
                        "small" or "/small" or "world" or "/world" or "name" or "/name" or
                        "order" or "/order" || tag.Length > 0 && char.IsDigit(tag[0]))
                    { i = end; continue; }
                }
            }
            text.Append(raw[i]);
        }
        return text.ToString();
    }
}
