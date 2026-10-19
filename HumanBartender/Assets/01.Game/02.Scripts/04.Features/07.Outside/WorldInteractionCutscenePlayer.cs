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
        if (request != null) throw new InvalidOperationException("상호작용 컷씬이 이미 재생 중입니다.");
        if (sequence == null) throw new InvalidOperationException("컷씬 스튜디오의 원본 StudioSequence를 연결하세요.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        request = source;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            var host = new GameObject("Interaction Timeline");
            host.transform.SetParent(transform, false);
            player = host.AddComponent<StudioPlayer>();
            player.HandleKeyboardInput = false;
            await player.PlayAsync(sequence, source.Token);
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (player != null) Destroy(player.gameObject);
            player = null;
            request = null;
        }
    }
}
