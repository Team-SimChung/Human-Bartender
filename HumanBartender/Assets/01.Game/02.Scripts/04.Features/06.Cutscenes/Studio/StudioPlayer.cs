using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    /// <summary>내보낸 씬과 게임 내 호출에서 같은 화면 덮기 재생기를 사용함.</summary>
    public sealed class StudioPlayer : MonoBehaviour
    {
        public StudioSequence Sequence;
        public bool PlayOnStart;
        public bool PauseGameplay = true;
        public int SortingOrder = 200;
        public bool IsPlaying { get; private set; }
        public bool IsWaiting => clock.Waiting.HasValue;
        public double CurrentTime => clock.Time;
        // 상호작용 재생기는 대사 진행만 전달한다. Space 유지 스킵은 모든 재생 경로에서 공통 처리한다.
        public bool HandleKeyboardInput { get; set; } = true;

        private readonly StudioClock clock = new();
        private CancellationTokenSource run;
        private GameObject canvasObject;
        private StudioStage stage;
        private bool skip;
        private bool advance;
        private static StudioPlayer activePlayer;
        public static bool HasActivePlayback => activePlayer != null && activePlayer.IsPlaying;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaybackOwner() => activePlayer = null;
        private void Awake()
        {
            if (PlayOnStart)
                SceneTransitionManager.SuppressStartupFadeForStandalonePlayback();
        }

        private void Start()
        {
            if (PlayOnStart)
                PlayAsync(Sequence, this.GetCancellationTokenOnDestroy()).Forget(Report);
        }

        public void Continue() => advance = true;
        public void Skip()
        {
            if (Sequence != null && Sequence.AllowSkip)
                skip = true;
        }

        public void Stop() => run?.Cancel();
        private void OnDisable() => Stop();
        public async UniTask PlayAsync(StudioSequence sequence, CancellationToken token = default)
        {
            if (IsPlaying || activePlayer != null && activePlayer.IsPlaying)
                throw new InvalidOperationException("A Cutscene Studio sequence is already playing.");
            if (sequence == null || sequence.Timeline == null)
                throw new ArgumentException("Cutscene Studio sequence is missing.");
            var issues = sequence.Validate();
            if (issues.Count > 0)
                throw new InvalidOperationException(string.Join("\n", issues));
            var visualWarnings = StudioSequenceWarnings.CollectMissingVisuals(sequence);
            if (visualWarnings.Count > 0)
                Debug.LogWarning("[CutsceneStudio] 시각 리소스 누락 · 대사 재생은 계속합니다: " + sequence.Id + "\n" +
                    string.Join("\n", visualWarnings), sequence);
            using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            run = source;
            IsPlaying = true;
            activePlayer = this;
            Sequence = sequence;
            skip = advance = false;
            int startedFrame = Time.frameCount;
            CutsceneSkipScreen skipScreen = null;
            clock.Seek(0);
            clock.Tick(sequence, 0, true);
            try
            {
                source.Token.ThrowIfCancellationRequested();
                using var gameplay = new StudioGameplayScope(PauseGameplay);
                using var audio = new StudioAudioPlayback(gameObject, AudioOutputSettings.Shared);
                var director = CreatePresentation(sequence, out var canvas);
                if (sequence.AllowSkip)
                {
                    skipScreen = FindFirstObjectByType<CutsceneSkipScreen>();
                    if (skipScreen != null) skipScreen.Begin(Skip);
                    else Debug.LogWarning("[CutsceneSkip] 공용 부트스트랩의 Canvas_CutsceneSkip 연결을 확인하세요.", this);
                }
                double last = Time.realtimeSinceStartupAsDouble;
                while (!skip && !clock.Finished)
                {
                    source.Token.ThrowIfCancellationRequested();
                    double now = Time.realtimeSinceStartupAsDouble;
                    // ESC로 설정창을 닫은 프레임도 새 유지 입력으로 사용하지 않는다.
                    if (Time.frameCount != startedFrame)
                        skipScreen?.Tick(UIDisplayOptions.IsOptionOpen || UIDisplayOptions.EscapeHandledThisFrame ||
                            Keyboard.current?.escapeKey.wasPressedThisFrame == true);
                    if (skip) break;
                    if (UIDisplayOptions.IsOptionOpen)
                    {
                        audio.Evaluate(sequence, clock.DisplayTime, true, clock, paused: true);
                        last = now;
                        await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                        continue;
                    }
                    bool keyPressed = HandleKeyboardInput && Time.frameCount != startedFrame &&
                        !DialogueAdvanceInput.ConsumedThisFrame && DialogueAdvanceInput.PressedKey(includeInteraction: true) != null;
                    bool advanceThisFrame = advance || keyPressed;
                    bool acceptedInput = advanceThisFrame && clock.CanAdvance;
                    if (advanceThisFrame)
                    {
                        DialogueAdvanceInput.TryConsume();
                        clock.Advance();
                        advance = false;
                    }

                    clock.Tick(sequence, acceptedInput ? 0 : now - last, true);
                    if (clock.Finished)
                        break;
                    last = now;
                    stage.FitTo(canvas.pixelRect.size);
                    EvaluatePresentation(director);
                    audio.Evaluate(sequence, clock.DisplayTime, clock.IsHolding, clock);
                    await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                }

                source.Token.ThrowIfCancellationRequested();
            }
            finally
            {
                skipScreen?.End();
                // 스킵·자연 종료 모두 뒤의 대사/선택지가 같은 프레임의 입력을 재사용하지 않게 한다.
                DialogueAdvanceInput.TryConsume();
                if (canvasObject != null)
                {
                    canvasObject.SetActive(false);
                    Destroy(canvasObject);
                }

                canvasObject = null;
                stage = null;
                IsPlaying = false;
                if (activePlayer == this)
                    activePlayer = null;
                run = null;
            }
        }

        private PlayableDirector CreatePresentation(StudioSequence sequence, out Canvas canvas)
        {
            canvasObject = new GameObject("Cutscene Studio · " + sequence.Id, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var backdrop = StudioStage.Rect("Letterbox & input blocker", canvasObject.transform, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            var blocker = backdrop.gameObject.AddComponent<Image>();
            blocker.color = Color.black;
            stage = StudioStage.Rect("Stage", canvasObject.transform, new Vector2(StudioSequence.Width, StudioSequence.Height)).gameObject.AddComponent<StudioStage>();
            stage.Build(sequence);
            var director = stage.gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.playableAsset = sequence.Timeline;
            foreach (var track in sequence.Timeline.GetOutputTracks())
                director.SetGenericBinding(track, stage);
            director.RebuildGraph();
            return director;
        }

        private void EvaluatePresentation(PlayableDirector director)
        {
            stage.PlaybackClock = clock;
            stage.RevealDialogue = clock.Waiting.HasValue;
            int previous = stage.EvaluationVersion;
            director.time = clock.DisplayTime;
            director.Evaluate();
            // 활성 트랙이 없는 프레임도 초기화하고, 믹서가 평가한 프레임은 중복 계산하지 않음.
            if (stage.EvaluationVersion == previous)
                stage.Evaluate(clock.DisplayTime, stage.RevealDialogue);
        }

        private static void Report(Exception error)
        {
            if (error is not OperationCanceledException)
                Debug.LogException(error);
        }
    }
}
