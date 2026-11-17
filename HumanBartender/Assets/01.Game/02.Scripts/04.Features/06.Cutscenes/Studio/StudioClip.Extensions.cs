using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    public sealed partial class StudioClip
    {
        [SerializeField, Header("대사 제작"), Tooltip("스튜디오에서 본문·속도 편집을 확정할 때 적용합니다. 기존 파일은 수동입니다.")]
        private StudioDialogueLengthMode dialogueLengthMode;
        public StudioDialogueLengthMode DialogueLengthMode { get => dialogueLengthMode; internal set => dialogueLengthMode = value; }
        [SerializeField, Header("타이핑 효과음"), Tooltip("시퀀스 공통 설정을 사용하거나 이 대사만 끄고 직접 지정합니다.")]
        private StudioTypingAudioMode typingAudioMode;
        public StudioTypingAudioMode TypingAudioMode { get => typingAudioMode; internal set => typingAudioMode = value; }
        [SerializeField] private StudioTypingAudioSettings typingAudio = new();
        public StudioTypingAudioSettings TypingAudio => typingAudio;
        internal StudioTypingAudioSettings ResolveTypingAudio(StudioSequence sequence) => typingAudioMode switch
        {
            StudioTypingAudioMode.Off => null,
            StudioTypingAudioMode.Custom => typingAudio,
            _ => sequence?.TypingAudio
        };

        [System.NonSerialized] internal StudioDialogueTiming TimingCache;
        [System.NonSerialized] internal object TimingSignature;
        [SerializeField] private GameObject dialoguePrefab;
        public GameObject DialoguePrefab { get => dialoguePrefab; internal set => dialoguePrefab = value; }

        // 프리팹을 해제할 때 자동 전환했던 편집 설정만 복원함. 재생은 기존 설정을 평가함.
        [SerializeField, HideInInspector] private bool hasDialogueSettingsBeforePrefab;
        [SerializeField, HideInInspector] private bool dialogueStyleBeforePrefab;
        [SerializeField, HideInInspector] private bool bubbleLayoutBeforePrefab;
        [SerializeField, HideInInspector] private TMPro.TMP_FontAsset dialogueFontBeforePrefab;

        [SerializeField] private string cameraTargetId;
        public string CameraTargetId { get => cameraTargetId; internal set => cameraTargetId = value; }
        [SerializeField] private Vector2 cameraFollowOffset;
        public Vector2 CameraFollowOffset { get => cameraFollowOffset; internal set => cameraFollowOffset = value; }

        [SerializeField, Min(0)] private float shakeSpeed = 1;
        public float ShakeSpeed { get => shakeSpeed; internal set => shakeSpeed = value; }
        [SerializeField] private Vector2 shakeAmplitude = new(12, 12);
        public Vector2 ShakeAmplitude { get => shakeAmplitude; internal set => shakeAmplitude = value; }

        [SerializeField, Min(0)] private float animationOffset;
        public float AnimationOffset { get => animationOffset; internal set => animationOffset = value; }
        [SerializeField, Min(.01f)] private float animationSpeed = 1;
        public float AnimationSpeed { get => animationSpeed; internal set => animationSpeed = value; }

        [SerializeField, Min(0)] private float backgroundDissolve;
        public float BackgroundDissolve { get => backgroundDissolve; internal set => backgroundDissolve = value; }
    }

    // 저장된 범위 밖 값도 미리보기와 게임에서 같은 규칙으로 해석함.
    internal static class StudioExtensionValues
    {
        internal static float NonNegative(float value, float fallback = 0) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
        internal static float Speed(float value) => Mathf.Max(.01f, NonNegative(value, 1));
        internal static Vector2 Position(Vector2 value) => new(
            float.IsNaN(value.x) || float.IsInfinity(value.x) ? 0 : value.x,
            float.IsNaN(value.y) || float.IsInfinity(value.y) ? 0 : value.y);
    }
}
