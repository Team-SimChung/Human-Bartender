using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    // 매 시점에서 레이어를 다시 평가하므로 역방향 탐색도 재생과 같은 화면을 만듦.
    internal sealed class StudioBackgroundPresentation
    {
        private readonly RectTransform root;
        private readonly List<Image> layers = new();
        private int used;
        internal Image Current => used > 0 ? layers[used - 1] : layers[0];

        internal StudioBackgroundPresentation(Transform parent)
        {
            root = StudioStage.Rect("Backgrounds", parent, new Vector2(StudioSequence.Width, StudioSequence.Height));
            AddLayer();
        }
        private Image AddLayer()
        {
            var image = StudioStage.Rect("Background", root, root.sizeDelta).gameObject.AddComponent<Image>();
            image.raycastTarget = false; image.color = Color.clear;
            layers.Add(image); return image;
        }
        internal void Reset()
        {
            used = 0;
            foreach (var layer in layers)
            {
                layer.color = Color.clear; layer.sprite = null;
                layer.rectTransform.anchoredPosition = Vector2.zero;
                layer.rectTransform.localScale = Vector3.one;
                layer.rectTransform.localRotation = Quaternion.identity;
            }
        }
        internal void Apply(StudioEntry entry, double time, StudioPose pose)
        {
            float duration = Mathf.Min((float)entry.Clip.duration, StudioExtensionValues.NonNegative(entry.Asset.BackgroundDissolve));
            float progress = duration > 0 ? Mathf.Clamp01((float)(time - entry.Clip.start) / duration) : 1;
            if (progress >= 1) Reset();
            float incomingAlpha = entry.Asset.BackgroundSprite != null ? Mathf.Clamp01(pose.Opacity) * progress : 0;
            FadePreviousLayers(progress, incomingAlpha);
            var image = used < layers.Count ? layers[used] : AddLayer();
            used++;
            image.sprite = entry.Asset.BackgroundSprite;
            image.rectTransform.anchoredPosition = pose.Position;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, pose.Rotation);
            image.rectTransform.localScale = new Vector3(entry.Asset.FlipX ? -pose.Scale : pose.Scale, pose.Scale, 1);
            image.color = new Color(1, 1, 1, incomingAlpha);
        }

        // 알파 합성 이후에도 이전 화면과 새 화면의 기여도가 각각 1-t / t가 되게 함.
        // 반투명 또는 빈 배경에서도 종료 순간에 이전 이미지가 갑자기 사라지지 않음.
        private void FadePreviousLayers(float progress, float incomingAlpha)
        {
            float weight = incomingAlpha < 1 ? (1 - progress) / (1 - incomingAlpha) : 0;
            float beforeTransmission = 1, afterTransmission = 1;
            for (int i = used - 1; i >= 0; i--)
            {
                var color = layers[i].color;
                float before = color.a;
                color.a = afterTransmission > 0 ? Mathf.Clamp01(before * beforeTransmission * weight / afterTransmission) : 0;
                layers[i].color = color;
                beforeTransmission *= 1 - before;
                afterTransmission *= 1 - color.a;
            }
        }
    }
}
