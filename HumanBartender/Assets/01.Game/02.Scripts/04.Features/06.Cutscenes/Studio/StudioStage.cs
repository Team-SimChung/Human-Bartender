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
        private StudioDialoguePresentation dialoguePresentation;
        private StudioDialoguePrefabPresentation prefabDialogue;
        private StudioBackgroundPresentation backgroundPresentation;
        private bool usingPrefabDialogue;
        private readonly Dictionary<string, Image> actors = new();
        internal bool RevealDialogue { get; set; }
        internal int EvaluationVersion { get; private set; }
        public RectTransform Frame => frame;
        public TextMeshProUGUI Dialogue => usingPrefabDialogue ? prefabDialogue.Label : dialogue;
        public float ScreenEffectAlpha => effect != null ? effect.color.a : 0;
        public RectTransform BackgroundTransform => background != null ? background.rectTransform : null;
        public float BackgroundOpacity => background != null ? background.color.a : 0;
        public RectTransform DialogueTransform => usingPrefabDialogue ? prefabDialogue.Root : bubble;
        public float DialogueOpacity => usingPrefabDialogue ? prefabDialogue.Visibility.alpha : bubbleVisibility != null ? bubbleVisibility.alpha : 0;

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
            dialoguePresentation = null;
            prefabDialogue = null; backgroundPresentation = null; usingPrefabDialogue = false;
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
            backgroundPresentation = new StudioBackgroundPresentation(world);
            background = backgroundPresentation.Current;
            foreach (var actor in sequence.Actors.Where(MatchesActorsWhere).OrderBy(SelectNullOrderBy))
            {
                if (string.IsNullOrEmpty(actor.Id) || actors.ContainsKey(actor.Id))
                    continue;
                var visual = Image(actor.Name, world, actor.Size);
                visual.preserveAspect = true;
                actors.Add(actor.Id, visual);
            }

            bubble = Rect("Dialogue", frame, new Vector2(StudioDialoguePresentation.DefaultWidth, StudioDialoguePresentation.DefaultHeight));
            bubbleVisibility = bubble.gameObject.AddComponent<CanvasGroup>();
            var panel = Image("Panel", bubble, bubble.sizeDelta);
            panel.color = new Color(.025f, .035f, .05f, .94f);
            var label = Rect("Text", bubble, bubble.sizeDelta - StudioDialoguePresentation.DefaultPadding * 2);
            dialogue = label.gameObject.AddComponent<TextMeshProUGUI>();
            dialogue.font = sequence.Font != null ? sequence.Font : TMP_Settings.defaultFontAsset;
            dialogue.fontSize = StudioDialoguePresentation.DefaultFontSize;
            dialogue.alignment = TextAlignmentOptions.MidlineLeft;
            dialogue.textWrappingMode = TextWrappingModes.Normal;
            dialogue.raycastTarget = false;
            dialoguePresentation = new StudioDialoguePresentation(bubble, panel.rectTransform, dialogue);
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
        private System.Func<StudioClip, StudioProperty, float, float> valueOverride;

        // 미기록 편집값도 저장된 키와 같은 합성 / 렌더링 경로에서 평가함.
        public void Evaluate(double time, bool revealDialogue = false,
            System.Func<StudioClip, StudioProperty, float, float> overrideValue = null)
        {
            valueOverride = overrideValue;
            try { EvaluateFrame(time, revealDialogue); }
            finally { valueOverride = null; }
        }

        private void EvaluateFrame(double time, bool revealDialogue)
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
            StudioEntry? cameraEntry = null;
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
                    case StudioKind.Visual:
                        ApplyActor(entry, time);
                        break;
                    case StudioKind.Camera:
                        cameraEntry = entry;
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

            if (cameraEntry.HasValue)
            {
                var entry = cameraEntry.Value;
                var camera = Sample(entry, time);
                center = camera.Position;
                zoom = camera.Scale;
                if (!string.IsNullOrEmpty(entry.Asset.CameraTargetId))
                    center += CameraTargetPosition(entry.Asset.CameraTargetId, System.Math.Min(time, entry.Clip.end)) +
                        StudioExtensionValues.Position(entry.Asset.CameraFollowOffset);
            }
            world.anchoredPosition = -center * zoom + shake;
            world.localScale = Vector3.one * zoom;
            if (line.HasValue)
                ApplyDialogue(line.Value, time, center, zoom, revealDialogue);
            if (!usingPrefabDialogue) prefabDialogue?.StopWaiting();
        }

        private void ResetVisuals()
        {
            world.anchoredPosition = Vector2.zero;
            world.localScale = Vector3.one;
            bubble.gameObject.SetActive(false);
            bubble.localScale = Vector3.one;
            bubble.localRotation = Quaternion.identity;
            bubbleVisibility.alpha = 1;
            prefabDialogue?.Hide(); usingPrefabDialogue = false;
            backgroundPresentation.Reset(); background = backgroundPresentation.Current;
            effect.color = Color.clear;
            foreach (var actor in Sequence.Actors)
            {
                if (actor == null || !TryGetActor(actor.Id, out var image))
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

        private StudioPose Sample(StudioEntry entry, double time)
        {
            var pose = StudioKeyframes.Sample(entry.Asset, (float)(time - entry.Clip.start), entry.Clip.duration);
            if (valueOverride == null) return pose;
            pose.Position = new Vector2(Value(entry.Asset, StudioProperty.PositionX, pose.Position.x), Value(entry.Asset, StudioProperty.PositionY, pose.Position.y));
            pose.Rotation = Value(entry.Asset, StudioProperty.Rotation, pose.Rotation);
            pose.Scale = Value(entry.Asset, StudioProperty.Scale, pose.Scale);
            pose.Opacity = Value(entry.Asset, StudioProperty.Opacity, pose.Opacity);
            return pose;
        }

        private float Value(StudioClip clip, StudioProperty property, float evaluated) => valueOverride?.Invoke(clip, property, evaluated) ?? evaluated;

        private void ApplyActor(StudioEntry entry, double time)
        {
            var clip = entry.Asset;
            if (!TryGetActor(clip.ActorId, out var image))
                return;
            image.enabled = true;
            bool flip = Value(clip, StudioProperty.FlipX, StudioKeyframes.Evaluate(clip, StudioProperty.FlipX,
                (float)(time - entry.Clip.start), entry.Clip.duration)) >= .5f;
            ApplyActorPose(image, clip, Sample(entry, time), flip);
            var sprite = StudioSpriteAnimation.Sample(clip, time - entry.Clip.start, entry.Clip.duration);
            if (clip.Frames != null && clip.Frames.Length > 0)
                image.sprite = sprite;
        }

        private void ApplyActorPose(Image image, StudioClip clip, StudioPose pose, bool flip)
        {
            ApplyTransform(image.rectTransform, pose, flip);
            var color = image.color;
            color.a = (Sequence.FindActor(clip.ActorId)?.Color.a ?? 1) * pose.Opacity;
            image.color = color;
        }

        private static void ApplyTransform(RectTransform rect, StudioPose pose, bool flip)
        {
            rect.anchoredPosition = pose.Position;
            rect.localScale = new Vector3(flip ? -pose.Scale : pose.Scale, pose.Scale, 1);
            rect.localRotation = Quaternion.Euler(0, 0, pose.Rotation);
        }

        private void ApplyBackground(StudioEntry entry, double time)
        {
            backgroundPresentation.Apply(entry, time, Sample(entry, time));
            background = backgroundPresentation.Current;
        }

        private Vector2 CameraTargetPosition(string id, double time)
        {
            Vector2 position = Sequence.FindActor(id)?.Position ?? Vector2.zero;
            foreach (var entry in entries)
                if (entry.Asset.Kind == StudioKind.Visual && entry.Asset.ActorId == id && time >= entry.Clip.start &&
                    (entry.Contains(time) || entry.Asset.HoldEnd)) position = Sample(entry, time).Position;
            return position;
        }

        private Vector2 ApplyEffect(StudioEntry entry, double time)
        {
            var clip = entry.Asset;
            float age = (float)(time - entry.Clip.start);
            float strength = Value(clip, StudioProperty.Strength, StudioKeyframes.Evaluate(clip, StudioProperty.Strength, age, entry.Clip.duration));
            float alpha = Value(clip, StudioProperty.Opacity, StudioKeyframes.Evaluate(clip, StudioProperty.Opacity, age, entry.Clip.duration));
            if (clip.Effect == StudioEffect.Shake)
            {
                float speed = StudioExtensionValues.NonNegative(clip.ShakeSpeed, 1);
                var amount = clip.ShakeAmplitude;
                return new Vector2(Mathf.Sin((float)time * 73 * speed) * StudioExtensionValues.NonNegative(amount.x, 12),
                    Mathf.Sin((float)time * 91 * speed) * StudioExtensionValues.NonNegative(amount.y, 12)) * (strength * alpha);
            }
            var color = clip.EffectColor;
            color.a *= alpha * strength;
            effect.color = StudioEvaluation.Composite(effect.color, color);
            return Vector2.zero;
        }

        private void ApplyDialogue(StudioEntry entry, double time, Vector2 center, float zoom, bool reveal)
        {
            var clip = entry.Asset;
            int visible = reveal ? int.MaxValue : Mathf.Max(0, Mathf.FloorToInt((float)(time - entry.Clip.start) * clip.CharactersPerSecond));
            usingPrefabDialogue = StudioDialoguePrefabPresentation.IsCompatible(clip.DialoguePrefab);
            if (usingPrefabDialogue)
            {
                if (prefabDialogue == null || prefabDialogue.Source != clip.DialoguePrefab)
                {
                    prefabDialogue?.Destroy();
                    prefabDialogue = new StudioDialoguePrefabPresentation(clip.DialoguePrefab, frame);
                    prefabDialogue.Root.SetSiblingIndex(effect.transform.GetSiblingIndex());
                }
                prefabDialogue.Apply(clip, Sequence.Font, Sequence.FindActor(clip.ActorId)?.Name, visible, reveal);
            }
            else
            {
                bubble.gameObject.SetActive(true);
                dialoguePresentation.Apply(clip, Sequence.Font);
            }
            var position = new Vector2(0, clip.BubbleScreenHeight);
            if (TryGetActor(clip.ActorId, out var speaker))
                position = (speaker.rectTransform.anchoredPosition - center) * zoom + new Vector2(0, clip.BubbleOffset);
            // 출력 비율과 관계없이 말풍선 기준 위치를 연출 영역 안으로 제한함.
            position = usingPrefabDialogue ? prefabDialogue.ClampAnchor(position) : dialoguePresentation.ClampAnchor(position);
            var pose = Sample(entry, time);
            pose.Position += position;
            ApplyTransform(DialogueTransform, pose, false);
            if (usingPrefabDialogue) prefabDialogue.Visibility.alpha = pose.Opacity;
            else bubbleVisibility.alpha = pose.Opacity;
            Dialogue.maxVisibleCharacters = visible;
        }

        public void FitTo(Vector2 size)
        {
            if (frame == null)
                return;
            frame.localScale = Vector3.one * Mathf.Min(size.x / StudioSequence.Width, size.y / StudioSequence.Height);
        }

        // 미리보기와 재생 모두 연결되지 않은 대상을 같은 기준으로 제외함.
        private bool TryGetActor(string id, out Image actor)
        {
            actor = null;
            return !string.IsNullOrEmpty(id) && actors.TryGetValue(id, out actor) && actor != null;
        }

        public Vector2 ActorPosition(string id) => TryGetActor(id, out var actor) ? actor.rectTransform.anchoredPosition : Vector2.zero;
        public bool ActorVisible(string id) => TryGetActor(id, out var actor) && actor.enabled;
        public RectTransform ActorTransform(string id) => TryGetActor(id, out var actor) ? actor.rectTransform : null;
        public Vector2 FrameToWorld(Vector2 point) => (point - world.anchoredPosition) / world.localScale.x;
        public Vector2 CameraCenter => -world.anchoredPosition / world.localScale.x;
        public float CameraZoom => world.localScale.x;

        public Rect ActorFrameBounds(string id)
        {
            if (!TryGetActor(id, out var actor)) return default;
            var corners = new Vector3[4];
            actor.rectTransform.GetWorldCorners(corners);
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
