using TMPro;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    public enum StudioDialogueAlignment
    {
        [InspectorName("왼쪽")] Left,
        [InspectorName("가운데")] Center,
        [InspectorName("오른쪽")] Right
    }

    /// <summary>대사의 기본값 / 유효 범위 / 표시를 미리보기와 실제 재생에서 공유함.</summary>
    internal sealed class StudioDialoguePresentation
    {
        internal const float DefaultFontSize = 22;
        internal const float DefaultWidth = 620;
        internal const float DefaultHeight = 106;
        internal static readonly Vector2 DefaultPadding = new(22, 12);

        private readonly RectTransform bubble, panel;
        private readonly TextMeshProUGUI label;
        private bool measured, lastAdaptive;
        private string lastText;
        private TMP_FontAsset lastFont;
        private float lastFontSize, lastWidth;
        private Vector2 lastPadding;

        internal StudioDialoguePresentation(RectTransform bubble, RectTransform panel, TextMeshProUGUI label)
        {
            this.bubble = bubble;
            this.panel = panel;
            this.label = label;
        }

        internal static float FontSize(float value) => FiniteClamp(value, DefaultFontSize, 1, StudioSequence.Height);
        internal static TextAlignmentOptions Alignment(StudioDialogueAlignment value) => value switch
        {
            StudioDialogueAlignment.Center => TextAlignmentOptions.Midline,
            StudioDialogueAlignment.Right => TextAlignmentOptions.MidlineRight,
            _ => TextAlignmentOptions.MidlineLeft
        };
        internal static float Width(float value) => FiniteClamp(value, DefaultWidth, 1, StudioSequence.Width);
        internal static Vector2 Padding(Vector2 value, float width) => new(
            FiniteClamp(value.x, DefaultPadding.x, 0, (Width(width) - 1) * .5f),
            FiniteClamp(value.y, DefaultPadding.y, 0, StudioSequence.Height * .5f));

        private static float FiniteClamp(float value, float fallback, float min, float max) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);

        internal void Apply(StudioClip clip, TMP_FontAsset sequenceFont)
        {
            label.font = clip.Font != null ? clip.Font : sequenceFont != null ? sequenceFont : TMP_Settings.defaultFontAsset;
            label.fontSize = clip.UseDialogueStyle ? FontSize(clip.DialogueFontSize) : DefaultFontSize;
            var color = clip.UseDialogueStyle ? clip.DialogueTextColor : Color.white;
            label.color = new Color(FiniteClamp(color.r, 1, 0, 1), FiniteClamp(color.g, 1, 0, 1),
                FiniteClamp(color.b, 1, 0, 1), FiniteClamp(color.a, 1, 0, 1));
            label.alignment = clip.UseDialogueStyle ? Alignment(clip.DialogueAlignment) : TextAlignmentOptions.MidlineLeft;
            string text = clip.Text ?? string.Empty;
            if (label.text != text) label.text = text;

            float width = clip.UseBubbleLayout ? Width(clip.BubbleWidth) : DefaultWidth;
            Vector2 padding = clip.UseBubbleLayout ? Padding(clip.BubblePadding, width) : DefaultPadding;
            bool adaptive = clip.UseDialogueStyle || clip.UseBubbleLayout;
            if (measured && lastAdaptive == adaptive && lastText == text && lastFont == label.font &&
                lastFontSize == label.fontSize && lastWidth == width && lastPadding == padding) return;

            // 타이핑으로 보이는 글자 수와 관계없이 전체 본문으로 한 번 계산함.
            float height = DefaultHeight;
            float textWidth = width - padding.x * 2;
            if (adaptive && label.font != null)
            {
                int visible = label.maxVisibleCharacters;
                try
                {
                    label.maxVisibleCharacters = int.MaxValue;
                    float preferred = label.GetPreferredValues(text, textWidth, Mathf.Infinity).y;
                    if (!float.IsNaN(preferred) && !float.IsInfinity(preferred))
                        height = Mathf.Max(height, preferred + padding.y * 2);
                }
                finally { label.maxVisibleCharacters = visible; }
            }
            bubble.sizeDelta = panel.sizeDelta = new Vector2(width, height);
            label.rectTransform.sizeDelta = new Vector2(textWidth, Mathf.Max(1, height - padding.y * 2));
            measured = true;
            lastAdaptive = adaptive; lastText = text; lastFont = label.font;
            lastFontSize = label.fontSize; lastWidth = width; lastPadding = padding;
        }

        internal Vector2 ClampAnchor(Vector2 position)
        {
            // 기존 기준 범위를 유지하면서 넓거나 높은 말풍선이 가장자리에서 잘리는 것을 줄임.
            float x = Mathf.Min(160, Mathf.Max(0, (StudioSequence.Width - bubble.sizeDelta.x) * .5f));
            float y = Mathf.Min(205, Mathf.Max(0, (StudioSequence.Height - bubble.sizeDelta.y) * .5f));
            return new Vector2(Mathf.Clamp(position.x, -x, x), Mathf.Clamp(position.y, -y, y));
        }
    }
}
