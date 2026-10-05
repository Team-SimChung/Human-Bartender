using UnityEngine.Timeline;

/// <summary>Unity Timeline 에셋 재생/정지 인터페이스.</summary>
public interface ITimeLinePlayer
{
    public void PlayTimeline(TimelineAsset timeline);
    public void StopTimeline();
}
