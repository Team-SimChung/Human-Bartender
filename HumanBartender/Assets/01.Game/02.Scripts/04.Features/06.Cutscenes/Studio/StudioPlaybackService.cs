using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 등록 목록의 조회와 에셋 수명을 재생 데이터에서 분리함.
    public static class StudioPlaybackService
    {
        public static async UniTask<bool> TryPlayAsync(MonoBehaviour host, string id, CancellationToken token)
        {
            var catalogHandle = await ResourceLoader.TryLoadAsync<StudioCatalog>(StudioCatalog.Address, token);
            try
            {
                token.ThrowIfCancellationRequested();
                if (!catalogHandle.HasValue)
                    return false;
                var entry = catalogHandle.Value.Result.Find(id);
                if (entry == null)
                    return false;
                var sequenceHandle = await ResourceLoader.TryLoadAsync<StudioSequence>(entry.ResourceKey, token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (!sequenceHandle.HasValue)
                        throw new InvalidOperationException("등록된 컷씬 에셋을 찾을 수 없습니다: " + entry.ResourceKey);
                    var player = host.GetComponent<StudioPlayer>();
                    if (player == null)
                        player = host.gameObject.AddComponent<StudioPlayer>();
                    await player.PlayAsync(sequenceHandle.Value.Result, token);
                    return true;
                }
                finally
                {
                    ResourceLoader.ReleaseHandle<StudioSequence>(ref sequenceHandle);
                }
            }
            finally
            {
                ResourceLoader.ReleaseHandle<StudioCatalog>(ref catalogHandle);
            }
        }
    }
}
