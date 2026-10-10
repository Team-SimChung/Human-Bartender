using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    /// <summary>편집 미리보기와 게임 재생이 같은 Unity UI 구조를 사용함.</summary>
    public sealed class StudioStage : MonoBehaviour
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Sequence")]
        private StudioSequence sequence;
        private StudioSequence builtSequence;
        public StudioSequence Sequence => sequence;
        private RectTransform frame, world, bubble;
        private Image effect, background;
        private CanvasGroup bubbleVisibility;
        private TextMeshProUGUI dialogue;
        private readonly Dictionary<string, Image> actors = new();
        internal bool RevealDialogue { get; set; }
        internal int EvaluationVersion { get; private set; }
        public RectTransform Frame => frame;
        public TextMeshProUGUI Dialogue => dialogue;
        public float ScreenEffectAlpha => effect != null ? effect.color.a : 0;
        public RectTransform BackgroundTransform => background != null ? background.rectTransform : null;
        public float BackgroundOpacity => background != null ? background.color.a : 0;
        public RectTransform DialogueTransform => bubble;
        public float DialogueOpacity => bubbleVisibility != null ? bubbleVisibility.alpha : 0;

        public void Build(StudioSequence value)
        {
            ClearVisuals();
            sequence = value;
            if (sequence == null)
                return;
            builtSequence = sequence;
            BuildVisuals();
            Evaluate(0);
        }

        private void ClearVisuals()
        {
            if (frame != null)
            {
                frame.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(frame.gameObject);
                else
                    DestroyImmediate(frame.gameObject);
            }

            actors.Clear();
            entries.Clear();
            ownedActors.Clear();
            frame = world = bubble = null;
            effect = background = null;
            bubbleVisibility = null;
            dialogue = null;
            builtSequence = null;
            RevealDialogue = false;
        }

        private void BuildVisuals()
        {
            frame = Rect("16:9 Frame", transform, new Vector2(StudioSequence.Width, StudioSequence.Height));
            frame.gameObject.AddComponent<RectMask2D>();
            var fill = Image("Backdrop", frame, frame.sizeDelta);
            fill.color = sequence.BackgroundColor;
            world = Rect("World", frame, frame.sizeDelta);
            background = Image("Background", world, frame.sizeDelta);
            background.color = Color.clear;
            foreach (var actor in sequence.Actors.Where(MatchesActorsWhere).OrderBy(SelectNullOrderBy))
            {
                if (string.IsNullOrEmpty(actor.Id) || actors.ContainsKey(actor.Id))
                    continue;
                var visual = Image(actor.Name, world, actor.Size);
                visual.preserveAspect = true;
                actors.Add(actor.Id, visual);
            }

            bubble = Rect("Dialogue", frame, new Vector2(620, 106));
            bubbleVisibility = bubble.gameObject.AddComponent<CanvasGroup>();
            var panel = Image("Panel", bubble, bubble.sizeDelta);
            panel.color = new Color(.025f, .035f, .05f, .94f);
            var label = Rect("Text", bubble, new Vector2(576, 82));
            dialogue = label.gameObject.AddComponent<TextMeshProUGUI>();
            dialogue.font = sequence.Font != null ? sequence.Font : TMP_Settings.defaultFontAsset;
            dialogue.fontSize = 22;
            dialogue.alignment = TextAlignmentOptions.MidlineLeft;
            dialogue.textWrappingMode = TextWrappingModes.Normal;
            dialogue.raycastTarget = false;
            effect = Image("Screen Effect", frame, frame.sizeDelta);
            bool MatchesActorsWhere(StudioActor a)
            {
                return a != null;
            }

            int SelectNullOrderBy(StudioActor a)
            {
                return a.Layer;
            }
        }

        private readonly List<StudioEntry> entries = new();
        private readonly HashSet<string> ownedActors = new();
        public void Evaluate(double time, bool revealDialogue = false)
        {
            // 씬 복원이나 Inspector 직렬화로 참조가 바뀐 경우에도 화면을 함께 교체함.
            if (builtSequence != sequence || frame == null && sequence != null)
            {
                ClearVisuals();
                if (sequence != null)
                {
                    builtSequence = sequence;
                    BuildVisuals();
                }
            }
            if (Sequence == null)
                return;
            EvaluationVersion++;
            // 마지막 시각에서도 종료 페이드가 포함된 최종 프레임을 표시함.
            time = System.Math.Min(time, System.Math.Max(0, Sequence.Duration - .000001));
            StudioEvaluation.CollectClips(Sequence, entries);
            ownedActors.Clear();
            foreach (var entry in entries)
                if (StudioEvaluation.OwnsActor(entry.Asset))
                    ownedActors.Add(entry.Asset.ActorId);
            ResetVisuals();
            StudioEntry? line = null;
            var center = Vector2.zero;
            float zoom = 1;
            var shake = Vector2.zero;
            foreach (var entry in entries)
            {
                if (time < entry.Clip.start)
                    continue;
                var clip = entry.Asset;
                bool active = entry.Contains(time);
                if (!active && !clip.HoldEnd)
                    continue;
                switch (clip.Kind)
                {
                    case StudioKind.Actor:
                    case StudioKind.Visual:
                        ApplyActor(entry, time);
                        break;
                    case StudioKind.Camera:
                        var camera = Sample(entry, time);
                        center = camera.Position;
                        zoom = camera.Scale;
                        break;
                    case StudioKind.Dialogue when active:
                        line = entry;
                        break;
                    case StudioKind.Background:
                        ApplyBackground(entry, time);
                        break;
                    case StudioKind.Effect when active:
                        shake += ApplyEffect(entry, time);
                        break;
                }
            }

            // 오브젝트 키는 기본 이동·카메라 결과 위에 적용함.
            foreach (var entry in entries)
                ApplyState(entry, time, ref center, ref zoom);
            world.anchoredPosition = -center * zoom + shake;
            world.localScale = Vector3.one * zoom;
            if (line.HasValue)
                ApplyDialogue(line.Value, time, center, zoom, revealDialogue);
        }

        private void ResetVisuals()
        {
            world.anchoredPosition = Vector2.zero;
            world.localScale = Vector3.one;
            bubble.gameObject.SetActive(false);
            bubble.localScale = Vector3.one;
            bubble.localRotation = Quaternion.identity;
            bubbleVisibility.alpha = 1;
            background.sprite = null;
            background.color = Color.clear;
            background.rectTransform.anchoredPosition = Vector2.zero;
            background.rectTransform.localScale = Vector3.one;
            background.rectTransform.localRotation = Quaternion.identity;
            effect.color = Color.clear;
            foreach (var actor in Sequence.Actors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.Id) || !actors.TryGetValue(actor.Id, out var image))
                    continue;
                image.sprite = actor.Sprite;
                image.color = actor.Color;
                image.enabled = !actor.ClipControlled && ownedActors.Contains(actor.Id);
                image.rectTransform.sizeDelta = actor.Size;
                image.rectTransform.anchoredPosition = actor.Position;
                image.rectTransform.localScale = Vector3.one;
                image.rectTransform.localRotation = Quaternion.identity;
            }
        }

        private static StudioPoseKey Sample(StudioEntry entry, double time)
        {
            return StudioKeyframes.Sample(entry.Asset, (float)(time - entry.Clip.start), entry.Clip.duration);
        }

        private void ApplyActor(StudioEntry entry, double time)
        {
            var clip = entry.Asset;
            if (!actors.TryGetValue(clip.ActorId ?? "", out var image))
                return;
            if (clip.Kind == StudioKind.Visual)
                image.enabled = true;
            ApplyActorPose(image, clip, Sample(entry, time));
            var sprite = StudioSpriteAnimation.Sample(clip, time - entry.Clip.start, entry.Clip.duration);
            if (clip.Frames != null && clip.Frames.Length > 0)
                image.sprite = sprite;
        }

        private void ApplyActorPose(Image image, StudioClip clip, StudioPoseKey pose)
        {
            ApplyTransform(image.rectTransform, pose, clip.FlipX);
            var color = image.color;
            color.a = (Sequence.FindActor(clip.ActorId)?.Color.a ?? 1) * pose.Opacity;
            image.color = color;
        }

        private static void ApplyTransform(RectTransform rect, StudioPoseKey pose, bool flip)
        {
            rect.anchoredPosition = pose.Position;
            rect.localScale = new Vector3(flip ? -pose.Scale : pose.Scale, pose.Scale, 1);
            rect.localRotation = Quaternion.Euler(0, 0, pose.Rotation);
        }

        private void ApplyBackground(StudioEntry entry, double time)
        {
            var pose = Sample(entry, time);
            background.sprite = entry.Asset.BackgroundSprite;
            ApplyTransform(background.rectTransform, pose, entry.Asset.FlipX);
            background.color = new Color(1, 1, 1, background.sprite != null ? pose.Opacity : 0);
        }

        private Vector2 ApplyEffect(StudioEntry entry, double time)
        {
            var clip = entry.Asset;
            float age = (float)(time - entry.Clip.start);
            float strength = StudioKeyframes.Evaluate(clip, StudioProperty.Strength, age, entry.Clip.duration);
            float alpha = StudioKeyframes.Evaluate(clip, StudioProperty.Opacity, age, entry.Clip.duration);
            if (clip.Effect == StudioEffect.Shake)
                return new Vector2(Mathf.Sin((float)time * 73), Mathf.Sin((float)time * 91)) * (12 * strength * alpha);
            var color = clip.EffectColor;
            color.a *= alpha * strength;
            effect.color = StudioEvaluation.Composite(effect.color, color);
            return Vector2.zero;
        }

        private void ApplyState(StudioEntry entry, double time, ref Vector2 center, ref float zoom)
        {
            var clip = entry.Asset;
            if (clip.Kind != StudioKind.State || time < entry.Clip.start || (!entry.Contains(time) && !clip.HoldEnd))
                return;
            float age = (float)System.Math.Min(time - entry.Clip.start, entry.Clip.duration);
            if (!StudioKeyframes.HasOverrides(clip))
            {
                float first = float.PositiveInfinity;
                if (clip.Keys != null)
                    foreach (var key in clip.Keys)
                        if (key != null)
                            first = Mathf.Min(first, key.Time);
                if (age < first)
                    return;
            }

            var pose = StudioKeyframes.Sample(clip, age, entry.Clip.duration);
            if (clip.StateCamera)
            {
                center = pose.Position;
                zoom = pose.Scale;
            }
            else if (actors.TryGetValue(clip.ActorId ?? "", out var actor))
                ApplyActorPose(actor, clip, pose);
        }

        private void ApplyDialogue(StudioEntry entry, double time, Vector2 center, float zoom, bool reveal)
        {
            var clip = entry.Asset;
            bubble.gameObject.SetActive(true);
            dialogue.font = clip.Font != null ? clip.Font : Sequence.Font != null ? Sequence.Font : TMP_Settings.defaultFontAsset;
            var position = new Vector2(0, -185);
            if (!string.IsNullOrEmpty(clip.ActorId) && actors.TryGetValue(clip.ActorId, out var speaker))
                position = (speaker.rectTransform.anchoredPosition - center) * zoom + new Vector2(0, clip.BubbleOffset);
            // 출력 비율과 관계없이 말풍선 기준 위치를 연출 영역 안으로 제한함.
            position.x = Mathf.Clamp(position.x, -160, 160);
            position.y = Mathf.Clamp(position.y, -205, 205);
            var pose = Sample(entry, time);
            pose.Position += position;
            ApplyTransform(bubble, pose, false);
            bubbleVisibility.alpha = pose.Opacity;
            if (dialogue.text != clip.Text)
                dialogue.text = clip.Text;
            dialogue.maxVisibleCharacters = reveal ? int.MaxValue : Mathf.Max(0, Mathf.FloorToInt((float)(time - entry.Clip.start) * clip.CharactersPerSecond));
        }

        public void FitTo(Vector2 size)
        {
            if (frame == null)
                return;
            frame.localScale = Vector3.one * Mathf.Min(size.x / StudioSequence.Width, size.y / StudioSequence.Height);
        }

        public Vector2 ActorPosition(string id) => actors[id].rectTransform.anchoredPosition;
        public bool ActorVisible(string id) => actors.TryGetValue(id, out var actor) && actor.enabled;
        public RectTransform ActorTransform(string id) => actors.TryGetValue(id ?? "", out var actor) ? actor.rectTransform : null;
        public Vector2 FrameToWorld(Vector2 point) => (point - world.anchoredPosition) / world.localScale.x;
        public Vector2 CameraCenter => -world.anchoredPosition / world.localScale.x;
        public float CameraZoom => world.localScale.x;

        public StudioPoseKey CapturePose(string id) => actors.TryGetValue(id ?? "", out var actor) ? new StudioPoseKey
        {
            Position = actor.rectTransform.anchoredPosition,
            Scale = Mathf.Abs(actor.rectTransform.localScale.x),
            Rotation = actor.rectTransform.localEulerAngles.z,
            Opacity = actor.color.a / Mathf.Max(.001f, (Sequence.FindActor(id)?.Color.a ?? 1))
        }

        : new StudioPoseKey
        {
            Position = CameraCenter,
            Scale = CameraZoom
        };
        public Rect ActorFrameBounds(string id)
        {
            var corners = new Vector3[4];
            actors[id].rectTransform.GetWorldCorners(corners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                var p = (Vector2)frame.InverseTransformPoint(corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 size)
        {
            var image = Rect(name, parent, size).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
