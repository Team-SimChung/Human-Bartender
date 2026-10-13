using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio
{
    [TrackColor(.55f, .72f, .38f)]
    [TrackClipType(typeof(StudioClip))]
    [TrackBindingType(typeof(StudioStage))]
    public sealed class StudioTrack : TrackAsset
    {
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("StudioRowHeight")]
        private float studioRowHeight = 56;
        public float StudioRowHeight
        {
            get { return studioRowHeight; }
            internal set { studioRowHeight = value; }
        }

        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("StudioDisplayName")]
        private string studioDisplayName;
        public string StudioDisplayName
        {
            get { return studioDisplayName; }
            internal set { studioDisplayName = value; }
        }

        // -1은 클립으로 유형을 추론하며 명시된 유형은 빈 트랙에도 적용함.
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("StudioTrackKind")]
        private int studioTrackKind = -1;
        public int StudioTrackKind
        {
            get { return studioTrackKind; }
            internal set { studioTrackKind = value; }
        }

        // 추가·드롭·붙여넣기에서 같은 트랙 유형 판정을 사용함.
        public bool Accepts(StudioKind kind)
        {
            if (StudioTrackKind >= 0 && StudioTrackKind != (int)kind)
                return false;
            foreach (var clip in GetClips())
                if (clip.asset is not StudioClip data || data.Kind != kind)
                    return false;
            return true;
        }

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<StudioMixer>.Create(graph, inputCount);
            foreach (var track in timelineAsset.GetOutputTracks())
                if (track is StudioTrack && !track.mutedInHierarchy)
                {
                    mixer.GetBehaviour().EvaluateStage = track == this;
                    break;
                }

            return mixer;
        }

        public sealed class StudioMixer : PlayableBehaviour
        {
            public bool EvaluateStage;
            public override void ProcessFrame(Playable playable, FrameData info, object playerData)
            {
                if (EvaluateStage && playerData is StudioStage stage && playable.GetGraph().GetResolver()is PlayableDirector director)
                    stage.Evaluate(director.time, stage.RevealDialogue);
            }
        }
    }
}
