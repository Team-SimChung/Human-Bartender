using System;

namespace HumanBartender.CutsceneStudio
{
    // 프레임 표시, 이동, 스냅에서 Timeline에 저장된 FPS를 함께 사용함.
    public static class StudioTiming
    {
        public const double DefaultFrameRate = 30;
        public static double FrameRate(StudioSequence sequence)
        {
            double rate = sequence != null && sequence.Timeline != null ? sequence.Timeline.editorSettings.frameRate : DefaultFrameRate;
            return double.IsFinite(rate) && rate > 0 ? rate : DefaultFrameRate;
        }

        public static double FrameDuration(StudioSequence sequence)
        {
            return 1 / FrameRate(sequence);
        }

        public static double Snap(StudioSequence sequence, double time)
        {
            double rate = FrameRate(sequence);
            return Math.Round(time * rate) / rate;
        }
    }
}
