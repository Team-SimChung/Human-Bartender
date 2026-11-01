using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    // 기존 말풍선의 레이아웃은 재사용하고 컷씬의 시간 / 스타일만 전달함.
    internal sealed class StudioDialoguePrefabPresentation
    {
        internal GameObject Source { get; }
        internal RectTransform Root { get; }
        internal CanvasGroup Visibility { get; }
        internal TextMeshProUGUI Label { get; }
        private readonly DynamicSpeechBubble view;
        private readonly DialogueInputPrompt prompt;
        private readonly TMP_FontAsset font;
        private readonly Color color;
        private readonly TextAlignmentOptions alignment;
        private readonly float fontSize, minFontSize;
        private readonly Vector2 minSize, maxSize, originalSize;
        private readonly Vector4 padding;
        private readonly DynamicSpeechBubble.BubbleSizeMode mode;
        private string lastText;
        private int lastSettings;
        private bool prepared;

        internal static bool IsCompatible(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<RectTransform>() == null) return false;
            var bubble = prefab.GetComponentInChildren<DynamicSpeechBubble>(true);
            return bubble != null && bubble.bubble != null && bubble.textLabel is TextMeshProUGUI &&
                bubble.bubble.IsChildOf(prefab.transform) && bubble.textLabel.transform.IsChildOf(prefab.transform);
        }

        internal StudioDialoguePrefabPresentation(GameObject prefab, Transform parent)
        {
            if (!IsCompatible(prefab)) throw new ArgumentException("DynamicSpeechBubble과 본문 TMP가 연결된 UI 프리팹이 필요합니다.");
            Source = prefab;
            Root = StudioStage.Rect("Prefab Dialogue", parent, Vector2.zero);
            Root.gameObject.SetActive(false);
            Visibility = Root.gameObject.AddComponent<CanvasGroup>();
            var instance = UnityEngine.Object.Instantiate(prefab, Root, false);
            instance.SetActive(true);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            view = instance.GetComponentInChildren<DynamicSpeechBubble>(true);
            Label = (TextMeshProUGUI)view.textLabel;
            prompt = view.GetComponent<DialogueInputPrompt>();
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            font = Label.font; color = Label.color; alignment = Label.alignment;
            fontSize = view.baseFontSize; minFontSize = view.minFontSize;
            minSize = view.minSize; maxSize = view.maxSize; originalSize = view.bubble.sizeDelta;
            padding = new Vector4(view.paddingLeft, view.paddingBottom, view.paddingRight, view.paddingTop);
            mode = view.sizeMode;
        }

        internal void Apply(StudioClip clip, TMP_FontAsset sequenceFont, string speaker, int visible, bool waiting)
        {
            Root.gameObject.SetActive(true);
            var resolvedFont = clip.Font != null ? clip.Font : font != null ? font : sequenceFont != null ? sequenceFont : TMP_Settings.defaultFontAsset;
            int settings = HashCode.Combine(resolvedFont, clip.UseDialogueStyle, clip.DialogueFontSize, clip.DialogueTextColor,
                clip.DialogueAlignment, clip.UseBubbleLayout, clip.BubbleWidth, clip.BubblePadding);
            string text = clip.Text ?? string.Empty;
            if (!prepared || lastText != text || lastSettings != settings)
            {
                Label.font = resolvedFont;
                Label.color = clip.UseDialogueStyle ? clip.DialogueTextColor : color;
                Label.alignment = clip.UseDialogueStyle ? StudioDialoguePresentation.Alignment(clip.DialogueAlignment) : alignment;
                view.baseFontSize = clip.UseDialogueStyle ? StudioDialoguePresentation.FontSize(clip.DialogueFontSize) : fontSize;
                view.minFontSize = clip.UseDialogueStyle ? view.baseFontSize : minFontSize;
                view.minSize = minSize; view.maxSize = maxSize; view.bubble.sizeDelta = originalSize;
                view.paddingLeft = padding.x; view.paddingBottom = padding.y; view.paddingRight = padding.z; view.paddingTop = padding.w;
                if (clip.UseBubbleLayout)
                {
                    float width = StudioDialoguePresentation.Width(clip.BubbleWidth);
                    var inset = StudioDialoguePresentation.Padding(clip.BubblePadding, width);
                    view.paddingLeft = view.paddingRight = inset.x; view.paddingTop = view.paddingBottom = inset.y;
                    view.minSize = new Vector2(width, Mathf.Max(minSize.y, inset.y * 2 + 1));
                    view.maxSize = new Vector2(width, Mathf.Max(StudioSequence.Height, view.minSize.y));
                    view.bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }
                view.PrepareForText(text, clip.UseBubbleLayout ? DynamicSpeechBubble.BubbleSizeMode.Fixed : mode);
                prepared = true; lastText = text; lastSettings = settings;
            }
            if (view.nameLabel != null)
            {
                view.nameLabel.text = speaker ?? string.Empty;
                var nameRoot = view.nameLabel.transform.parent == view.transform ? view.nameLabel.gameObject : view.nameLabel.transform.parent.gameObject;
                nameRoot.SetActive(!string.IsNullOrEmpty(speaker));
            }
            if (view.centerArrow != null) view.centerArrow.SetActive(!string.IsNullOrEmpty(speaker));
            if (view.leftArrow != null) view.leftArrow.SetActive(false);
            if (view.rightArrow != null) view.rightArrow.SetActive(false);
            view.UpdateForVisible(visible);
            Label.maxVisibleCharacters = visible;
            prompt?.SetWaiting(waiting);
        }

        internal void Hide()
        {
            // 비활성화 / 재활성화 때 레이아웃과 입력 표시가 초기화되지 않게 표시만 숨김.
            Visibility.alpha = 0;
        }
        internal void StopWaiting() => prompt?.SetWaiting(false);

        internal Vector2 ClampAnchor(Vector2 position)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Root, view.bubble);
            var half = new Vector2(StudioSequence.Width, StudioSequence.Height) * .5f;
            float minX = -half.x - bounds.min.x, maxX = half.x - bounds.max.x;
            float minY = -half.y - bounds.min.y, maxY = half.y - bounds.max.y;
            return new Vector2(minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : 0,
                minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : 0);
        }

        internal void Destroy()
        {
            Root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root.gameObject);
            else UnityEngine.Object.DestroyImmediate(Root.gameObject);
        }
    }
}
