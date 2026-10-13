using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio
{
    // 저장된 Timeline의 열거형 값이 바뀌지 않도록 새 유형은 끝에 추가함.
    public enum StudioKind
    {
        Actor,
        Camera,
        Dialogue,
        Effect,
        Audio,
        Wait,
        Visual,
        State,
        Background
    }

    public enum StudioEffect
    {
        FadeIn,
        FadeOut,
        Flash,
        Shake,
        Tint
    }

    public sealed class StudioClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Kind")]
        private StudioKind kind;
        public StudioKind Kind
        {
            get { return kind; }

            internal set { kind = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ActorId")]
        private string actorId;
        public string ActorId
        {
            get { return actorId; }

            internal set { actorId = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("From")]
        private Vector2 from;
        public Vector2 From
        {
            get { return from; }

            internal set { from = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("To")]
        private Vector2 to;
        public Vector2 To
        {
            get { return to; }

            internal set { to = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ScaleFrom")]
        private float scaleFrom = 1;
        public float ScaleFrom
        {
            get { return scaleFrom; }

            internal set { scaleFrom = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ScaleTo")]
        private float scaleTo = 1;
        public float ScaleTo
        {
            get { return scaleTo; }

            internal set { scaleTo = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("RotationFrom")]
        private float rotationFrom;
        public float RotationFrom
        {
            get { return rotationFrom; }

            internal set { rotationFrom = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("RotationTo")]
        private float rotationTo;
        public float RotationTo
        {
            get { return rotationTo; }

            internal set { rotationTo = value; }
        }

        [Range(0, 1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("OpacityFrom")]
        private float opacityFrom = 1;
        public float OpacityFrom
        {
            get { return opacityFrom; }

            internal set { opacityFrom = value; }
        }

        [Range(0, 1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("OpacityTo")]
        private float opacityTo = 1;
        public float OpacityTo
        {
            get { return opacityTo; }

            internal set { opacityTo = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Curve")]
        private AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve Curve
        {
            get { return curve; }

            internal set { curve = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("HoldEnd")]
        private bool holdEnd = true;
        public bool HoldEnd
        {
            get { return holdEnd; }

            internal set { holdEnd = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("FlipX")]
        private bool flipX;
        public bool FlipX
        {
            get { return flipX; }

            internal set { flipX = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Frames")]
        private Sprite[] frames;
        internal Sprite[] Frames
        {
            get { return frames; }

            set { frames = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("FrameTimes")]
        private float[] frameTimes;
        internal float[] FrameTimes
        {
            get { return frameTimes; }

            set { frameTimes = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("AnimationLength")]
        private float animationLength;
        public float AnimationLength
        {
            get { return animationLength; }

            internal set { animationLength = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("StateCamera")]
        private bool stateCamera;
        public bool StateCamera
        {
            get { return stateCamera; }

            internal set { stateCamera = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Keys")]
        private StudioPoseKey[] keys;
        internal StudioPoseKey[] Keys
        {
            get { return keys; }

            set { keys = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("PropertyKeys")]
        private StudioPropertyKeys[] propertyKeys;
        internal StudioPropertyKeys[] PropertyKeys
        {
            get { return propertyKeys; }

            set { propertyKeys = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("BackgroundSprite")]
        private Sprite backgroundSprite;
        public Sprite BackgroundSprite
        {
            get { return backgroundSprite; }

            internal set { backgroundSprite = value; }
        }

        [Min(1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("FramesPerSecond")]
        private float framesPerSecond = 12;
        public float FramesPerSecond
        {
            get { return framesPerSecond; }

            internal set { framesPerSecond = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Loop")]
        private bool loop = true;
        public bool Loop
        {
            get { return loop; }

            internal set { loop = value; }
        }

        [TextArea(3, 8)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Text")]
        private string text = "대사를 입력하세요.";
        public string Text
        {
            get { return text; }

            internal set { text = value; }
        }

        [Tooltip("비워두면 기존 시퀀스에 저장된 기본 글꼴을 사용합니다.")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Font")]
        private TMPro.TMP_FontAsset font;
        public TMPro.TMP_FontAsset Font
        {
            get { return font; }

            internal set { font = value; }
        }

        [Min(1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("CharactersPerSecond")]
        private float charactersPerSecond = 22;
        public float CharactersPerSecond
        {
            get { return charactersPerSecond; }

            internal set { charactersPerSecond = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("WaitForInput")]
        private bool waitForInput;
        public bool WaitForInput
        {
            get { return waitForInput; }

            internal set { waitForInput = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("BubbleOffset")]
        private float bubbleOffset = 140;
        public float BubbleOffset
        {
            get { return bubbleOffset; }

            internal set { bubbleOffset = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Effect")]
        private StudioEffect effect;
        public StudioEffect Effect
        {
            get { return effect; }

            internal set { effect = value; }
        }

        [Range(0, 1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Strength")]
        private float strength = 1;
        public float Strength
        {
            get { return strength; }

            internal set { strength = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("EffectColor")]
        private Color effectColor = Color.black;
        public Color EffectColor
        {
            get { return effectColor; }

            internal set { effectColor = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Sound")]
        private AudioClip sound;
        public AudioClip Sound
        {
            get { return sound; }

            internal set { sound = value; }
        }

        [Range(0, 1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Volume")]
        private float volume = .7f;
        public float Volume
        {
            get { return volume; }

            internal set { volume = value; }
        }

        [Min(0)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("AudioOffset")]
        private float audioOffset;
        public float AudioOffset
        {
            get { return audioOffset; }

            internal set { audioOffset = value; }
        }

        [Min(0)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("FadeIn")]
        private float fadeIn;
        public float FadeIn
        {
            get { return fadeIn; }

            internal set { fadeIn = value; }
        }

        [Min(0)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("FadeOut")]
        private float fadeOut = .1f;
        public float FadeOut
        {
            get { return fadeOut; }

            internal set { fadeOut = value; }
        }

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
    }

    [System.Serializable]
    public sealed class StudioPoseKey
    {
        [Min(0)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Time")]
        private float time;
        public float Time
        {
            get { return time; }

            internal set { time = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Position")]
        private Vector2 position;
        public Vector2 Position
        {
            get { return position; }

            internal set { position = value; }
        }

        [Min(.01f)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Scale")]
        private float scale = 1;
        public float Scale
        {
            get { return scale; }

            internal set { scale = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Rotation")]
        private float rotation;
        public float Rotation
        {
            get { return rotation; }

            internal set { rotation = value; }
        }

        [Range(0, 1)]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Opacity")]
        private float opacity = 1;
        public float Opacity
        {
            get { return opacity; }

            internal set { opacity = value; }
        }
    }
}
