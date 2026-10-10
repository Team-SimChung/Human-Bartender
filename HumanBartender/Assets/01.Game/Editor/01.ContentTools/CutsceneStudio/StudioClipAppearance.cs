using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 씬 오브젝트 목록과 타임라인이 같은 원본 이미지·색상을 사용함.
    internal readonly struct StudioClipAppearance
    {
        internal readonly StudioActor Actor;
        internal readonly Sprite Sprite;
        private StudioClipAppearance(StudioActor actor, Sprite sprite)
        {
            Actor = actor;
            Sprite = sprite;
        }

        internal static StudioClipAppearance Resolve(StudioSequence sequence, StudioClip clip)
        {
            var actor = StudioTimelineLayout.Group(clip) == 1 ? sequence.FindActor(clip.ActorId) : null;
            if (clip.Kind == StudioKind.Background)
                return new StudioClipAppearance(null, clip.BackgroundSprite);
            if (actor == null)
                return default;
            if (clip.Frames != null)
                foreach (var sprite in clip.Frames)
                    if (sprite != null)
                        return new StudioClipAppearance(actor, sprite);
            return new StudioClipAppearance(actor, actor.Sprite);
        }
    }
}
