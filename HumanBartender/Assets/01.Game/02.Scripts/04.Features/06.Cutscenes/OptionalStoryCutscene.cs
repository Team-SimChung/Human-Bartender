using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Unimplemented story cinematics may be skipped without losing the following dialogue.</summary>
public static class OptionalStoryCutscene
{
    public static async UniTask<bool> PlayAsync(ICutScenePlayer player, string id, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (player == null || string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            await player.PlayCutScene(id, token: token);
            token.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Debug.LogWarning($"[Story] 컷신 '{id}'를 건너뛰고 다음 대화로 진행합니다: {error.Message}");
            return false;
        }
        finally
        {
            if (player is not UnityEngine.Object obj || obj != null)
                try { player.ClearCutScene(); }
                catch (Exception error) { Debug.LogWarning($"[Story] 컷신 화면 정리 실패: {error.Message}"); }
        }
    }
}
