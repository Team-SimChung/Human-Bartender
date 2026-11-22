using System.Threading;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using UnityEngine;

[RequireComponent(typeof(StudioPlayer))]
public class EndingCredits : MonoBehaviour
{
    private void Start() => PlayEndingAsync(this.GetCancellationTokenOnDestroy()).Forget();

    private async UniTask PlayEndingAsync(CancellationToken token)
    {
        var transition = SceneTransitionManager.Instance;
        await UniTask.WaitUntil(() => !transition.IsBusy || transition.CurrentState == SceneTransitionState.FadingIn,
            cancellationToken: token);
        var player = GetComponent<StudioPlayer>();
        await player.PlayAsync(player.Sequence, token);
        if (!token.IsCancellationRequested) transition.LoadScene("Main");
    }
}
