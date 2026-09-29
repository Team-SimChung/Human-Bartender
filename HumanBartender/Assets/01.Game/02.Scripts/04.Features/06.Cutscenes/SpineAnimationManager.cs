using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 컷씬에서 Spine 애니메이션을 재생/관리하는 매니저.
/// UI(Canvas) 위에서 동작해야 하므로 SkeletonGraphic 기반으로 구현되어 있다.
/// SpriteAnimationManager 와 동일한 호출 흐름(Initialize / ActiveSelf / SetInactive)을 따른다.
/// </summary>
public class SpineAnimationManager : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    CancellationTokenSource playback;
    void OnDisable() => playback?.Cancel();

    // 컷씬 애니메이션은 보통 단일 트랙으로 충분
    private const int DEFAULT_TRACK = 0;

    /// <summary>
    /// CutSceneManager.Awake 에서 한 번 호출. 레퍼런스 보정 + 비활성화.
    /// </summary>
    public void Initialize()
    {
        if (skeletonGraphic == null)
            skeletonGraphic = GetComponentInChildren<SkeletonGraphic>(true);

        if (skeletonGraphic == null)
        {
            Debug.LogError("[SpineAnimationManager] SkeletonGraphic 레퍼런스가 없습니다. 인스펙터에서 할당해주세요.");
            return;
        }

        ActiveSelf(false);
    }

    public void ActiveSelf(bool active)
    {
        if (skeletonGraphic != null)
            skeletonGraphic.gameObject.SetActive(active);
    }

    /// <summary>
    /// 트랙을 비우고 셋업 포즈로 되돌린다. (컷씬 정리 시 호출)
    /// </summary>
    public void SetInactive()
    {
        playback?.Cancel();
        if (skeletonGraphic == null) return;

        if (skeletonGraphic.AnimationState != null)
            skeletonGraphic.AnimationState.ClearTracks();

        if (skeletonGraphic.Skeleton != null)
            skeletonGraphic.Skeleton.SetToSetupPose();
    }

    /// <summary>
    /// 런타임에 SkeletonDataAsset 을 교체하고 재초기화한다.
    /// </summary>
    /// <returns>교체/초기화 성공 여부</returns>
    public bool SetSkeletonData(SkeletonDataAsset dataAsset)
    {
        playback?.Cancel();
        if (skeletonGraphic == null)
        {
            Debug.LogError("[SpineAnimationManager] SkeletonGraphic 가 할당되지 않았습니다.");
            return false;
        }

        if (dataAsset == null)
        {
            Debug.LogWarning("[SpineAnimationManager] SkeletonDataAsset 이 null 입니다.");
            return false;
        }

        // 데이터 교체 후 overwrite=true 로 강제 재빌드
        skeletonGraphic.skeletonDataAsset = dataAsset;
        skeletonGraphic.Initialize(true);

        if (skeletonGraphic.Skeleton == null)
        {
            Debug.LogError($"[SpineAnimationManager] Skeleton 빌드 실패: {dataAsset.name}");
            return false;
        }

        skeletonGraphic.Skeleton.SetToSetupPose();
        return true;
    }

    /// <summary>
    /// SkeletonData 의 첫 번째 애니메이션 이름을 반환. (데이터에 별도 애니메이션 키가 없을 때의 기본값)
    /// </summary>
    public string GetFirstAnimationName()
    {
        if (skeletonGraphic == null || skeletonGraphic.Skeleton == null)
            return null;

        var animations = skeletonGraphic.Skeleton.Data.Animations;
        if (animations.Count == 0)
        {
            Debug.LogWarning("[SpineAnimationManager] 등록된 애니메이션이 없습니다.");
            return null;
        }

        return animations.Items[0].Name;
    }

    /// <summary>
    /// 애니메이션을 재생한다.
    /// loop == false 인 경우 완료(Complete) 시점까지 await 한다.
    /// loop == true 인 경우 완료 시점이 없으므로 즉시 반환한다.
    /// 취소는 호출자에게 전달하며, 반복 재생도 토큰 취소 시 트랙을 정리한다.
    /// </summary>
    public async UniTask PlayAnimation(string animationName, bool loop, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (skeletonGraphic == null || skeletonGraphic.AnimationState == null)
        {
            throw new InvalidOperationException("Spine is not initialized.");
        }

        if (string.IsNullOrEmpty(animationName) ||
            skeletonGraphic.Skeleton.Data.FindAnimation(animationName) == null)
        {
            throw new InvalidOperationException("Missing Spine animation: " + animationName);
        }

        playback?.Cancel();
        TrackEntry entry = skeletonGraphic.AnimationState.SetAnimation(DEFAULT_TRACK, animationName, loop);
        var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        playback = source;

        if (loop)
        {
            ObserveLoopAsync(entry, source).Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
            return;
        }

        var completionSource = new UniTaskCompletionSource();

        void OnComplete(TrackEntry trackEntry) => completionSource.TrySetResult();
        void OnEnd(TrackEntry trackEntry) => completionSource.TrySetCanceled(source.Token);

        entry.Complete += OnComplete;
        entry.End += OnEnd;

        try
        {
            await completionSource.Task
                .AttachExternalCancellation(source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            entry.Complete -= OnComplete;
            entry.End -= OnEnd;
            FinishPlayback(entry, source);
        }
    }

    async UniTask ObserveLoopAsync(TrackEntry entry, CancellationTokenSource source)
    {
        try
        {
            await UniTask.WaitUntil(() => skeletonGraphic == null ||
                skeletonGraphic.AnimationState.GetCurrent(DEFAULT_TRACK) != entry, cancellationToken: source.Token);
        }
        finally { FinishPlayback(entry, source); }
    }

    void FinishPlayback(TrackEntry entry, CancellationTokenSource source)
    {
        if (ReferenceEquals(playback, source))
        {
            playback = null;
            if (source.IsCancellationRequested && skeletonGraphic != null &&
                skeletonGraphic.AnimationState.GetCurrent(DEFAULT_TRACK) == entry)
                skeletonGraphic.AnimationState.ClearTrack(DEFAULT_TRACK);
        }
        source.Dispose();
    }
}
