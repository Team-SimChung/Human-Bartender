using System;
using UnityEditor;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 프리뷰 자원과 재생 시간을 소유하며 창과 패널은 같은 시계를 조회함.
    internal sealed class StudioPreviewController : IDisposable
    {
        private readonly StudioEditSession session;
        private readonly Action repaint;
        private StudioPreview preview;
        private bool pending, playing;
        private double lastUpdate;
        internal StudioClock Clock { get; } = new();
        internal StudioPreview Preview => preview;
        internal bool RebuildPending => pending;
        internal bool Playing { get { return playing; } set { playing = value; lastUpdate = EditorApplication.timeSinceStartup; } }

        internal StudioPreviewController(StudioEditSession session, Action repaint)
        {
            this.session = session;
            this.repaint = repaint;
            lastUpdate = EditorApplication.timeSinceStartup;
        }

        internal void Tick(double now, float speed, bool applyInputWait)
        {
            double elapsed = now - lastUpdate;
            lastUpdate = now;
            if (EditorApplication.isPlaying)
            {
                playing = false;
                ReleasePreview();
                return;
            }
            if (!playing || session.Sequence == null)
                return;
            if (!applyInputWait && Clock.Waiting.HasValue)
                Clock.Advance();
            Clock.Tick(session.Sequence, elapsed * speed, applyInputWait);
            if (Clock.Finished)
                playing = false;
            repaint();
        }

        internal void Seek(double time)
        {
            playing = false;
            Clock.Seek(Math.Max(0, Math.Min(time, session.Sequence?.Duration ?? 0)));
            repaint();
        }

        internal void Toggle()
        {
            if (session.Sequence == null)
                return;
            if (Clock.Finished || Clock.Time >= session.Sequence.Duration)
                Clock.Seek(0);
            Playing = !playing;
        }

        internal void Rebuild(bool defer)
        {
            if (defer)
            {
                QueueRebuild();
                return;
            }
            EditorApplication.delayCall -= RebuildDeferred;
            pending = false;
            ReleasePreview();
            if (session.Sequence != null && session.Sequence.Timeline != null && !EditorApplication.isPlaying)
                preview = new StudioPreview(session.Sequence);
        }

        internal void QueueRebuild()
        {
            if (pending)
                return;
            pending = true;
            EditorApplication.delayCall += RebuildDeferred;
        }

        private void RebuildDeferred()
        {
            Rebuild(false);
            repaint();
        }

        private void ReleasePreview()
        {
            preview?.Dispose();
            preview = null;
        }

        public void Dispose()
        {
            EditorApplication.delayCall -= RebuildDeferred;
            pending = false;
            playing = false;
            ReleasePreview();
        }
    }
}
