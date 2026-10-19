using System;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioTimelineMode { Clips, Keyframes, Dialogue }

    // 동시에 둘 이상의 탭이 활성화되지 않도록 표시 모드를 한 값으로 보관함.
    [Serializable]
    internal sealed class StudioViewState
    {
        [SerializeField] private StudioTimelineMode mode;
        internal StudioTimelineMode Mode => mode;
        internal bool Keyframes
        {
            get { return mode == StudioTimelineMode.Keyframes; }
            set { SetMode(StudioTimelineMode.Keyframes, value); }
        }
        internal bool Dialogue
        {
            get { return mode == StudioTimelineMode.Dialogue; }
            set { SetMode(StudioTimelineMode.Dialogue, value); }
        }
        private void SetMode(StudioTimelineMode target, bool enabled)
        {
            if (enabled) mode = target;
            else if (mode == target) mode = StudioTimelineMode.Clips;
        }
    }
}
