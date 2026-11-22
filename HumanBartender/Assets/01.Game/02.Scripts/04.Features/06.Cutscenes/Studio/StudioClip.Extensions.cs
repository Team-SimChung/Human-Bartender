using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 입력 대기 중 이 클립을 계속 재생할지. 저장 번호는 바꾸지 않으며 0(트랙 설정 따름)이 기존 동작임.
    public enum StudioWaitPlayback
    {
        [InspectorName("트랙 설정 따름")] FollowTrack = 0,
        [InspectorName("계속 재생")] Continue = 1,
        [InspectorName("멈춤")] Pause = 2
    }

    public sealed partial class StudioClip
    {
        [SerializeField, Tooltip("입력 대기 중에도 반복 프레임 / 소리 / 흔들림을 계속할지 정합니다. 트랙 설정 따름이면 트랙의 '입력 대기 중 계속 재생'을 씁니다. " +
            "반복이 꺼진 그림과 페이드 / 플래시 / 색조 효과는 항상 멈추고, 이동 / 카메라 / 강도 키도 멈춥니다.")]
        private StudioWaitPlayback waitPlayback;
        public StudioWaitPlayback WaitPlayback { get => waitPlayback; internal set => waitPlayback = value; }

        [SerializeField, Tooltip("화면 전체와 대사에 적용할 UI 머티리얼입니다.")] private Material screenMaterial;
        public Material ScreenMaterial { get => screenMaterial; internal set => screenMaterial = value; }

        [SerializeField, Tooltip("화자 오브젝트 없이 표시할 화자 이름입니다.")] private string speakerName;
        public string SpeakerName { get => speakerName; internal set => speakerName = value; }

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
        [SerializeField, Min(.01f), Tooltip("엔진이 원본 애니메이션에 곱하는 속도입니다. 실제 재생 속도는 재생 배속 × 이 값입니다. " +
            "엔진 클립을 놓거나 연결할 때 정해지며, 이 값이 없는 기존 클립은 1입니다.")]
        private float engineSpeed = 1;
        public float EngineSpeed { get => engineSpeed; internal set => engineSpeed = value; }
        [SerializeField, Tooltip("프레임을 가져온 원본 AnimationClip의 GUID입니다. 에디터가 원본을 다시 찾을 때만 쓰며, " +
            "빌드에 클립이 딸려 들어가지 않도록 직접 참조하지 않습니다.")]
        private string sourceAnimationGuid;
        public string SourceAnimationGuid { get => sourceAnimationGuid; internal set => sourceAnimationGuid = value; }

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
