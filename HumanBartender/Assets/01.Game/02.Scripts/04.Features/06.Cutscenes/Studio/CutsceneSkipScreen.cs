using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    /// <summary>에디터에서 만든 공용 안내 화면. 재생기가 시작·종료와 스킵 콜백을 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneSkipScreen : MonoBehaviour
    {
        [Header("화면 연결")]
        [SerializeField, Tooltip("평소에는 꺼 두는 CutsceneSkipRoot입니다.")] private GameObject root;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Slider holdProgress;
        [Header("입력")]
        [SerializeField, Tooltip("공용 Input Actions의 Cutscene/Skip. 대화 진행 키 변경과 무관한 Space입니다.")]
        private InputActionReference skipInput;
        [Header("시간 · 초")]
        [SerializeField, Min(.1f), Tooltip("키를 누른 순간부터 스킵까지의 전체 시간입니다.")] private float holdSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("누르기 시작 후 진행 바를 표시하기까지의 시간입니다.")] private float revealSeconds = .3f;
        [SerializeField, Min(0f)] private float introSeconds = 1.5f;
        [SerializeField, Min(0f)] private float fadeSeconds = .15f;

        private InputAction input;
        private CutsceneSkipHold hold;
        private Action onSkip;
        private Tween fade;
        private bool visible;
        public bool IsRunning => onSkip != null;

        private void Awake() => End();

        public void Begin(Action skip)
        {
            End();
            if (skip == null) return;
            if (root == null || hintGroup == null || hintText == null || holdProgress == null || skipInput?.action == null)
            {
                Debug.LogError("[CutsceneSkip] 안내 UI와 Cutscene/Skip 입력 연결을 확인하세요.", this);
                return;
            }
            // 공유 에셋의 Map을 켜지 않는다. 재생 중에만 독립된 입력을 소유한다.
            input = skipInput.action.Clone();
            input.Enable();
            hold = new CutsceneSkipHold(holdSeconds, revealSeconds, introSeconds);
            hold.Begin(Time.realtimeSinceStartupAsDouble, Keyboard.current?.spaceKey.isPressed == true);
            onSkip = skip;
            hintText.text = $"스페이스 바를 {holdSeconds:0.##}초간 눌러 스킵";
            holdProgress.SetValueWithoutNotify(0f);
            root.SetActive(true);
            SetVisible(true);
        }

        // StudioPlayer의 재생 루프에서 호출한다. 대사 입력 담당 여부와 관계없이 동일하게 실행된다.
        public void Tick(bool blocked)
        {
            if (!IsRunning) return;
            ProcessInput(Time.realtimeSinceStartupAsDouble, input.IsPressed(), input.WasPressedThisFrame(),
                blocked || !Application.isFocused);
        }

        internal void ProcessInput(double now, bool pressed, bool pressedThisFrame, bool blocked)
        {
            if (!IsRunning) return;
            bool completed = hold.Tick(now, pressed, pressedThisFrame, blocked);
            holdProgress.SetValueWithoutNotify(hold.Progress);
            SetVisible(!blocked && hold.HintVisible);
            if (completed) onSkip?.Invoke();
        }

        public void End()
        {
            onSkip = null;
            input?.Dispose();
            input = null;
            hold = null;
            fade?.Kill();
            fade = null;
            visible = false;
            if (hintGroup != null) hintGroup.alpha = 0f;
            if (holdProgress != null) holdProgress.SetValueWithoutNotify(0f);
            if (root != null) root.SetActive(false);
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            fade?.Kill();
            fade = hintGroup.DOFade(value ? 1f : 0f, fadeSeconds).SetUpdate(true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) hold?.Interrupt();
        }

        private void OnDisable() => End();
        private void OnValidate()
        {
            holdSeconds = Mathf.Max(.1f, holdSeconds);
            revealSeconds = Mathf.Clamp(revealSeconds, 0f, holdSeconds - .01f);
        }
    }

    /// <summary>재생 루프에서 실제 시각을 받는 유지 판정. 입력 장치와 게임 시간에 의존하지 않는다.</summary>
    internal sealed class CutsceneSkipHold
    {
        private readonly double duration, reveal, intro;
        private double last, started, introAge, elapsed;
        private bool holding, requireRelease, completed, interrupted;
        internal float Progress => elapsed < reveal ? 0f : (float)Math.Clamp((elapsed - reveal) / (duration - reveal), 0d, 1d);
        internal bool HintVisible => introAge < intro || holding && elapsed >= reveal;

        internal CutsceneSkipHold(double duration, double reveal, double intro)
        {
            this.duration = duration;
            this.reveal = reveal;
            this.intro = intro;
        }

        internal void Begin(double now, bool alreadyPressed)
        {
            last = now;
            introAge = elapsed = 0;
            holding = completed = interrupted = false;
            requireRelease = alreadyPressed;
        }

        internal void Interrupt()
        {
            holding = false;
            elapsed = 0;
            requireRelease = true;
            interrupted = true;
        }

        internal bool Tick(double now, bool pressed, bool pressedThisFrame, bool blocked)
        {
            double delta = interrupted ? 0d : Math.Max(0d, now - last);
            interrupted = false;
            last = now;
            if (blocked) { Interrupt(); return false; }
            if (completed) return false;
            introAge += delta;
            if (!pressed)
            {
                holding = requireRelease = false;
                elapsed = 0;
                return false;
            }
            if (requireRelease) return false;
            if (!holding && pressedThisFrame) { started = now; holding = true; }
            if (!holding) return false;
            elapsed = Math.Max(0d, now - started);
            if (elapsed < duration) return false;
            completed = true;
            return true;
        }
    }
}
