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
        public string Speaker;
        public DialogueTextCompiler.Line Line;
        public CancellationTokenSource Reveal;
        public bool Skipped;
        public bool Revealing;
        public bool ReducedMotion;
        public bool ShakeSpanNotified;
        public int Visible;
        public bool[] VisibleGlyphs;
        public IDisposable AudioScope;
        public CancellationTokenRegistration ExternalRegistration;
        public CancellationTokenRegistration DestroyRegistration;
    }

    DynamicSpeechBubble bubble;
    DialogueTextAnimator animator;
    Session current;
    int nextId;
    static int warnings;
    ISoundEffectPlayer sound;
    bool backgroundAudio;
    Func<bool> audible;
    public void ConfigureAudio(ISoundEffectPlayer manager, bool background = false, Func<bool> canHear = null)
    { sound = manager; backgroundAudio = background; audible = canHear; }

    void Awake()
    {
        bubble = GetComponent<DynamicSpeechBubble>();
        animator = GetComponent<DialogueTextAnimator>();
        if (animator == null) animator = gameObject.AddComponent<DialogueTextAnimator>();
    }

    public int CurrentSessionId => current?.Id ?? 0;
    public bool IsHolding => current != null && !current.Revealing;

    public string SpeakerName => current?.Speaker ?? "";
    public event Action<DialogueTextPlayer> LineStarted;
    public event Action<DialogueTextPlayer> LineEnding;

    /// <summary>현재 줄에서 지금까지 화면에 드러난 글자 수. 타이핑 중이면 전체보다 작고, 즉시 표시·스킵 뒤에는 전체와 같다.</summary>
    public int VisibleCharacterCount => current?.Visible ?? 0;

    /// <summary>현재 재생 중인 한 줄의 전체 본문을 TMP 해석 결과에서 읽는다. 타이핑 진행 상태는 변경하지 않는다.</summary>
    public string ReadFullText() => current == null ? "" : Read(current.Line.Characters.Length);

    /// <summary>현재 줄에서 지금까지 드러난 글자만 읽는다. 아직 나오지 않은 뒷부분은 포함하지 않고, 타이핑 진행 상태도 바꾸지 않는다.</summary>
    public string ReadVisibleText() => current == null ? "" : Read(current.Visible);

    string Read(int count)
    {
        var text = new System.Text.StringBuilder();
        var info = bubble.textLabel.textInfo;
        count = Math.Min(count, current.Line.Characters.Length);
        for (int i = 0; i < count; i++)
        {
            var ch = info.characterInfo[i];
            int at = ch.index;
            if (at >= 0 && at + 1 < current.Line.Text.Length && char.IsSurrogatePair(current.Line.Text, at))
                text.Append(current.Line.Text, at, 2);
            else text.Append(ch.character);
        }
        return text.ToString();
    }

    // Presentation cues describe the revealed text; subscribers choose the response.
    public event Action ShakeSpanRevealed;
    public event Action MotionStopped;

    /// <param name="periodRhythm">거리·집·테라스 대사처럼 웹의 마침표 멈춤 규칙을 따를지. 바 내부 대사는 끈다.</param>
    /// <param name="tagTimeScale">태그가 정한 글자 간격·쉼에 곱하는 배율. 바 내부는 1/3(웹과 같음), 그 밖은 1.</param>
    public async UniTask PlayAsync(TypingData data, NewTextTagDataSO tags, float defaultDelay,
        CancellationToken external = default, string cocktailName = null,
        Func<float, CancellationToken, UniTask> wait = null, bool periodRhythm = false, float tagTimeScale = 1f)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        external.ThrowIfCancellationRequested();
        Stop();
        gameObject.SetActive(true);
        if (bubble == null) Awake();
        bubble.nameLabel.text = data.speaker;
        // 화자별 원본 색상과 관계없이 대화창 이름은 공통 하늘색을 사용한다.
        bubble.nameLabel.color = bubble.speakerNameColor;
        var session = Begin(data.str, tags, cocktailName, defaultDelay, false, periodRhythm, tagTimeScale);
        session.Revealing = true;
        session.ExternalRegistration = external.Register(() => Close(session));
        session.DestroyRegistration = this.GetCancellationTokenOnDestroy().Register(() => Close(session));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(external, this.GetCancellationTokenOnDestroy());
        session.Reveal = source;
        bool completed = false;
        try
        {
            // 별도 시계가 없으면 경과 시간을 누적하는 시계를 쓴다(QA-51, 웹 story-flow.js typeLine과 같음).
            var delay = wait ?? new TypingClock().WaitAsync;
            var line = session.Line;
            int count = line.Characters.Length;
            for (int i = 0; i < count;)
            {
                await delay(line.WaitBefore[i], source.Token);
                await delay(line.Characters[i].SpeedSeconds, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(current, session)) throw new OperationCanceledException();
                int visible = i + 1;
                while (visible < count && line.ContinuesPrevious[visible]) visible++;
                bubble.textLabel.maxVisibleCharacters = visible;
                session.Visible = visible;
                bubble.UpdateForVisible(visible);
                animator.SetVisible(visible, Time.time, false);
                if (visible == i + 1 && (!backgroundAudio || (!GameAudioContext.DirectDialogue && (audible == null || audible()))))
                    DialogueTypingAudio.TryPlayWithGain(sound, DialoguePresentationSettings.Shared, line.Text,
                        bubble.textLabel.textInfo.characterInfo[i], backgroundAudio ? .5f : 1f);
                NotifyShakeSpan(session, i, visible);
                source.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(current, session)) throw new OperationCanceledException();
                i = visible;
            }
            await delay(line.WaitBefore[count], source.Token);
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

    Session Begin(string text, NewTextTagDataSO tags, string cocktailName, float speed, bool instant,
        bool periodRhythm = false, float tagTimeScale = 1f)
    {
        var settings = DialoguePresentationSettings.Shared;
        if (!DialogueTextCompiler.TryCompile(text, tags, settings, cocktailName, speed, out var line, out string error, tagTimeScale))
        {
            if (warnings++ < 5) Debug.LogWarning($"[DialogueFX] {error}; showing readable fallback.");
            line = DialogueTextCompiler.Compile(DialogueTextFallback.Readable(text), null, settings, null, speed, tagTimeScale);
        }
        bubble.PrepareForText(line.Text);
        // Bubble preparation may already rebuild with maxVisibleCharacters = 0.
        // Capture the full layout before hiding it again below, in the same synchronous call.
        bubble.textLabel.maxVisibleCharacters = int.MaxValue;
        bubble.textLabel.ForceMeshUpdate(true, true);
        line.Bind(bubble.textLabel.textInfo);
        if (periodRhythm && settings != null)
            line.ApplyPeriodRhythm(bubble.textLabel.textInfo, settings.LonePeriodPause, settings.ConsecutivePeriodInterval);
        var session = new Session
        {
            Id = ++nextId, Line = line,
            Speaker = bubble.nameLabel != null ? bubble.nameLabel.text : "",
            ReducedMotion = settings != null && settings.ReducedMotion,
            Visible = instant ? line.Characters.Length : 0,
            VisibleGlyphs = new bool[line.Characters.Length]
        };
        // Capture from the full layout: TMP can mark unrevealed glyphs invisible later.
        for (int i = 0; i < session.VisibleGlyphs.Length; i++)
        {
            var ch = bubble.textLabel.textInfo.characterInfo[i];
            session.VisibleGlyphs[i] = ch.isVisible && ch.elementType == TMP_TextElementType.Character;
        }
        current = session;
        if (!instant) session.AudioScope = GameAudioContext.Dialogue(backgroundAudio);
        try
        {
            bubble.textLabel.maxVisibleCharacters = instant ? line.Characters.Length : 0;
            bubble.UpdateForVisible(instant ? line.Characters.Length : 0);
            animator.Bind(bubble.textLabel, line, session.ReducedMotion);
            animator.SetVisible(instant ? line.Characters.Length : 0, Time.time, instant);
        }
        catch { Stop(); throw; }
        LineStarted?.Invoke(this);
        return session;
    }

    void NotifyShakeSpan(Session session, int first, int end)
    {
        if (session.ReducedMotion) return;
        for (int i = first; i < end; i++)
        {
            var style = session.Line.Characters[i];
            if (!style.HasShake)
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

    /// <summary>
    /// 글자마다 새로 기다리지 않고 경과 시간을 누적한다. 한 프레임보다 짧은 간격이 남긴 시간은 다음 글자로 넘어가
    /// 프레임 속도와 상관없이 평균 속도가 설정대로 나온다(60FPS에서 18ms 간격이 두 프레임씩 걸려 30자로 떨어지던 문제).
    /// 게임 시간(Time.deltaTime)을 쓰므로 일시정지 중에는 멈춘다.
    /// </summary>
    public sealed class TypingClock
    {
        readonly Func<CancellationToken, UniTask> nextFrame;
        readonly Func<float> deltaTime;
        float owed;

        public TypingClock() : this(token => UniTask.NextFrame(token), () => Time.deltaTime) { }

        /// <summary>테스트에서 프레임과 프레임 시간을 직접 넣을 때 쓴다.</summary>
        public TypingClock(Func<CancellationToken, UniTask> nextFrame, Func<float> deltaTime)
        {
            this.nextFrame = nextFrame ?? throw new ArgumentNullException(nameof(nextFrame));
            this.deltaTime = deltaTime ?? throw new ArgumentNullException(nameof(deltaTime));
        }

        public async UniTask WaitAsync(float seconds, CancellationToken token)
        {
            if (seconds > 0f) owed += seconds;
            while (owed > 0f)
            {
                await nextFrame(token);
                owed -= deltaTime();
            }
        }
    }

    void ShowAll(Session session, bool instant)
    {
        if (!ReferenceEquals(current, session)) return;
        int count = session.Line.Characters.Length;
        bubble.textLabel.maxVisibleCharacters = count;
        session.Visible = count;
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
        LineEnding?.Invoke(this);
        current = null;
        old.Reveal?.Cancel();
        old.AudioScope?.Dispose();
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

    public static void TryPlay(ISoundEffectPlayer sound, DialoguePresentationSettings settings,
        string text, TMP_CharacterInfo ch) => TryPlayWithGain(sound, settings, text, ch, 1f);

    public static void TryPlayWithGain(ISoundEffectPlayer sound, DialoguePresentationSettings settings,
        string text, TMP_CharacterInfo ch, float gain)
    {
        if (sound == null || settings == null || string.IsNullOrWhiteSpace(settings.TypingSoundKey) ||
            ch.elementType == TMP_TextElementType.Sprite || ch.index < 0 || ch.index >= text.Length ||
            !char.IsLetterOrDigit(text, ch.index) || Time.frameCount == lastFrame ||
            (lastTime >= 0f && Time.unscaledTime - lastTime < settings.TypingSoundInterval)) return;
        lastFrame = Time.frameCount;
        lastTime = Time.unscaledTime;
        sound.PlayCue(settings.TypingSoundKey, gain);
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
