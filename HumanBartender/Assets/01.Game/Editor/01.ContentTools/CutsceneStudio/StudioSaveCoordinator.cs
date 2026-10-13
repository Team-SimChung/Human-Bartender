using System;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal interface IStudioSequenceStore
    {
        void Save(StudioSequence sequence);
    }

    internal sealed class StudioUnitySequenceStore : IStudioSequenceStore
    {
        public void Save(StudioSequence sequence) { StudioEditorAssets.Save(sequence); }
    }

    // 저장 대상과 예약 시점을 관리하며 입력 제스처 중 저장을 미룸.
    internal sealed class StudioSaveCoordinator
    {
        private readonly IStudioSequenceStore store;
        private StudioSequence sequence;
        private bool pending;
        private double saveAfter;
        internal bool Pending => pending;

        internal StudioSaveCoordinator(IStudioSequenceStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        internal void Load(StudioSequence value)
        {
            if (pending && sequence != null)
                Save();
            sequence = value;
            pending = false;
        }

        internal void MarkChanged(double now)
        {
            if (sequence == null)
                return;
            pending = true;
            saveAfter = now + 1.5;
        }

        internal bool Tick(double now, bool enabled, bool editing)
        {
            if (!pending || !enabled || editing || now < saveAfter)
                return false;
            Save();
            return true;
        }

        internal void Save()
        {
            if (sequence == null)
                return;
            store.Save(sequence);
            pending = false;
        }

        internal void AcknowledgeSave() { pending = false; }
    }
}
