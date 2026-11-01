using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using UnityEngine;

/// <summary>외부/홈의 직접 재생과 CSV timeline 단계가 함께 사용하는 Studio Timeline 재생기.</summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionCutscenePlayer : MonoBehaviour
{
    private StudioPlayer player;
    private CancellationTokenSource request;
    public bool IsPlaying => request != null;
    internal StudioPlayer ActivePlayer => player;
    public void Continue() { if (player != null && player.IsWaiting) player.Continue(); }
    private void OnDisable() => request?.Cancel();

    public async UniTask PlayAsync(StudioSequence sequence, CancellationToken token)
    {
        if (sequence == null) throw new InvalidOperationException("컷씬 스튜디오의 원본 StudioSequence를 연결하세요.");
        await RunAsync(async ct => { await player.PlayAsync(sequence, ct); return true; }, token);
    }

    /// <summary>대상에 직접 연결되지 않은 사건도 기존 Addressables StudioCatalog를 사용한다.</summary>
    public UniTask<bool> TryPlayAsync(string id, CancellationToken token)
        => RunAsync(ct => StudioPlaybackService.TryPlayAsync(player, id, ct), token);

    async UniTask<bool> RunAsync(Func<CancellationToken, UniTask<bool>> play, CancellationToken token)
    {
        if (request != null) throw new InvalidOperationException("상호작용 컷씬이 이미 재생 중입니다.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        request = source;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            var host = new GameObject("Interaction Timeline");
            host.transform.SetParent(transform, false);
            player = host.AddComponent<StudioPlayer>();
            player.HandleKeyboardInput = false;
            bool played = await play(source.Token);
            source.Token.ThrowIfCancellationRequested();
            return played;
        }
        finally
        {
            if (player != null) Destroy(player.gameObject);
            player = null;
            request = null;
        }
    }
}
