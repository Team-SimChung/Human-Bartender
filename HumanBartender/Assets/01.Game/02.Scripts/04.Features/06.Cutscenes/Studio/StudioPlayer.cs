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

        private readonly StudioClock clock = new();
        private CancellationTokenSource run;
        private GameObject canvasObject;
        private StudioStage stage;
        private bool skip;
        private bool advance;
        private static StudioPlayer activePlayer;
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
            using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            run = source;
            IsPlaying = true;
            activePlayer = this;
            Sequence = sequence;
            skip = advance = false;
            clock.Seek(0);
            try
            {
                source.Token.ThrowIfCancellationRequested();
                using var gameplay = new StudioGameplayScope(PauseGameplay);
                using var audio = new StudioAudioPlayback(gameObject);
                var director = CreatePresentation(sequence, out var canvas);
                double last = Time.realtimeSinceStartupAsDouble;
                while (!skip && !clock.Finished)
                {
                    source.Token.ThrowIfCancellationRequested();
                    double now = Time.realtimeSinceStartupAsDouble;
                    bool waiting = clock.Waiting.HasValue;
                    if (advance || Keyboard.current?.eKey.wasPressedThisFrame == true || Keyboard.current?.enterKey.wasPressedThisFrame == true)
                    {
                        if (waiting)
                            clock.Advance();
                        advance = false;
                    }

                    if (sequence.AllowSkip && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                        break;
                    clock.Tick(sequence, waiting ? 0 : now - last, true);
                    if (clock.Finished)
                        break;
                    last = now;
                    stage.FitTo(canvas.pixelRect.size);
                    EvaluatePresentation(director);
                    audio.Evaluate(sequence, clock.Time, clock.Waiting.HasValue);
                    await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                }

                source.Token.ThrowIfCancellationRequested();
            }
            finally
            {
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
