using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TMPro;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio
{
    [Serializable]
    public sealed class StudioActor
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Id")]
        private string id = "actor";
        public string Id
        {
            get { return id; }

            internal set { id = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Name")]
        private string name = "캐릭터";
        public string Name
        {
            get { return name; }

            internal set { name = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Sprite")]
        private Sprite sprite;
        public Sprite Sprite
        {
            get { return sprite; }

            internal set { sprite = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Position")]
        private Vector2 position;
        public Vector2 Position
        {
            get { return position; }

            internal set { position = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Size")]
        private Vector2 size = new(160, 240);
        public Vector2 Size
        {
            get { return size; }

            internal set { size = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Color")]
        private Color color = Color.white;
        public Color Color
        {
            get { return color; }

            internal set { color = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Layer")]
        private int layer;
        public int Layer
        {
            get { return layer; }

            internal set { layer = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ClipControlled")]
        private bool clipControlled;
        public bool ClipControlled
        {
            get { return clipControlled; }

            internal set { clipControlled = value; }
        }
    }

    /// <summary>컷씬 원본과 Unity Timeline의 연결을 저장함.</summary>
    public sealed class StudioSequence : ScriptableObject
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Id")]
        private string id = "cutscene";
        public string Id
        {
            get { return id; }

            internal set { id = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("DisplayName")]
        private string displayName;
        public string DisplayName
        {
            get { return displayName; }

            internal set { displayName = value; }
        }

        public string Title => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Timeline")]
        private TimelineAsset timeline;
        public TimelineAsset Timeline
        {
            get { return timeline; }

            internal set { timeline = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("BackgroundColor")]
        private Color backgroundColor = new(.06f, .07f, .09f, 1);
        public Color BackgroundColor
        {
            get { return backgroundColor; }

            internal set { backgroundColor = value; }
        }

        // 글꼴을 개별 지정하지 않은 기존 대사에 저장된 기본 글꼴을 사용함.
        [HideInInspector]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Font")]
        private TMP_FontAsset font;
        public TMP_FontAsset Font
        {
            get { return font; }

            internal set { font = value; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Actors")]
        private List<StudioActor> actors = new();
        [NonSerialized] private List<StudioActor> actorViewSource;
        [NonSerialized] private ReadOnlyCollection<StudioActor> actorView;
        public IReadOnlyList<StudioActor> Actors
        {
            get
            {
                // 역직렬화와 실행 취소로 원본 목록이 교체되면 읽기 전용 보기도 갱신함.
                if (!ReferenceEquals(actorViewSource, actors) || actorView == null)
                {
                    actorViewSource = actors;
                    actorView = actors.AsReadOnly();
                }

                return actorView;
            }
        }

        internal List<StudioActor> EditableActors
        {
            get { return actors; }
        }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("AllowSkip")]
        private bool allowSkip = true;
        public bool AllowSkip
        {
            get { return allowSkip; }

            internal set { allowSkip = value; }
        }

        public const float Width = 960;
        public const float Height = 540;
        public double Duration => Timeline == null ? 0 : Timeline.duration;

        public StudioActor FindActor(string id)
        {
            foreach (var actor in Actors)
                if (actor != null && actor.Id == id)
                    return actor;
            return null;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("컷씬 ID를 입력하세요.");
            if (Timeline == null)
            {
                errors.Add("Timeline 파일이 없습니다.");
                return errors;
            }

            var ids = new HashSet<string>();
            foreach (var actor in Actors)
                if (actor == null || string.IsNullOrWhiteSpace(actor.Id) || !ids.Add(actor.Id))
                    errors.Add("캐릭터 ID는 비어 있지 않고 서로 달라야 합니다.");
            foreach (var entry in StudioEvaluation.Clips(this))
            {
                var c = entry.Asset;
                if (entry.Clip.duration <= 0)
                    errors.Add(entry.Clip.displayName + ": 길이를 확인하세요.");
                if (StudioEvaluation.UsesActor(c) && !ids.Contains(c.ActorId))
                    errors.Add(entry.Clip.displayName + ": 연결된 캐릭터가 없습니다.");
                if (c.Kind == StudioKind.Audio && c.Sound == null)
                    errors.Add(entry.Clip.displayName + ": 사운드가 없습니다.");
                if (c.Kind == StudioKind.Dialogue && string.IsNullOrWhiteSpace(c.Text))
                    errors.Add(entry.Clip.displayName + ": 대사가 비어 있습니다.");
                if (entry.Clip.end > Duration + .001)
                    errors.Add(entry.Clip.displayName + ": 컷씬 길이를 넘습니다.");
            }

            return errors;
        }
    }
}
