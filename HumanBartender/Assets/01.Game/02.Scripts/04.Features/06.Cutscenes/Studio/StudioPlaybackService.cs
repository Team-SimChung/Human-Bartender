using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HumanBartender.CutsceneStudio
{
    // 등록 목록의 조회와 에셋 수명을 재생 데이터에서 분리함.
    public static class StudioPlaybackService
    {
        /// <summary>선택창을 여는 동안 검사한 원본을 재생 종료까지 유지한다.</summary>
        public sealed class PreparedSequence : IDisposable
        {
            private AsyncOperationHandle<StudioSequence>? handle;
            public StudioSequence Sequence => handle?.Result;
            internal PreparedSequence(AsyncOperationHandle<StudioSequence> value) => handle = value;
            public void Dispose() => ResourceLoader.ReleaseHandle(ref handle);
        }

        public static bool IsPrepared(StudioSequence sequence) => sequence != null &&
            sequence.Timeline != null && sequence.Duration > 0 && sequence.Validate().Count == 0 &&
            StudioEvaluation.Clips(sequence, false).Any();

        /// <summary>미등록·누락·미완성 방송은 선택 불가로 표시한다. 재생 전에 리소스를 검증한다.</summary>
        public static async UniTask<PreparedSequence> PrepareAsync(string id, CancellationToken token)
        {
            AsyncOperationHandle<StudioCatalog>? catalog = null;
            AsyncOperationHandle<StudioSequence>? sequence = null;
            try
            {
                catalog = await ResourceLoader.TryLoadAsync<StudioCatalog>(StudioCatalog.Address, token);
                token.ThrowIfCancellationRequested();
                var entry = catalog?.Result.Find(id);
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey)) return null;
                sequence = await ResourceLoader.TryLoadAsync<StudioSequence>(entry.ResourceKey, token);
                token.ThrowIfCancellationRequested();
                if (!sequence.HasValue || sequence.Value.Result == null) return null;
                if (!IsPrepared(sequence.Value.Result))
                {
                    Debug.LogWarning("[CutsceneStudio] 준비되지 않은 방송: " + id + " · " +
                        string.Join(" / ", sequence.Value.Result.Validate()));
                    return null;
                }
                var prepared = new PreparedSequence(sequence.Value);
                sequence = null;
                return prepared;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                Debug.LogWarning("[CutsceneStudio] 방송 준비 실패: " + id + " · " + error.Message);
                return null;
            }
            finally
            {
                ResourceLoader.ReleaseHandle(ref sequence);
                ResourceLoader.ReleaseHandle(ref catalog);
            }
        }

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
