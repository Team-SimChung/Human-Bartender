using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>배치된 테라스 화면으로 페이드 전환하고 종료 시 실내 카메라를 복원함.</summary>
[DisallowMultipleComponent]
public sealed class HomeTerracePresentation : MonoBehaviour
{
    [SerializeField] private CinemachineCamera terraceCamera;
    [SerializeField] private CinemachineBrain cameraBrain;
    [SerializeField] private WorldDialogueSpeakers speakers;
    [SerializeField] private Transform lunaAnchor;
    [SerializeField] private Transform wakeUpSpawn;
    [SerializeField, Min(0)] private float fadeDuration = .35f;
    [SerializeField] private string[] storyIds = { "night0", "night1", "night2", "ending" };
    private bool previousCameraEnabled;
    public bool IsActive { get; private set; }
    public WorldDialogueSpeakers Speakers => speakers;
    public Transform LunaAnchor => lunaAnchor;

    public bool ContainsStory(string id) => Array.IndexOf(storyIds ?? Array.Empty<string>(), id) >= 0;

    /// <summary>취침 페이드의 검은 화면에서 다음 날 위치와 실내 카메라를 준비함.</summary>
    public void PrepareMorning(Player player)
    {
        if (player == null || wakeUpSpawn == null)
            throw new InvalidOperationException("Home의 다음 날 침대 시작 위치를 연결하세요.");
        player.PrepareWorldInteraction();
        player.transform.position = wakeUpSpawn.position;
        RestoreCamera();
    }

    public async UniTask EnterAsync(ISceneFadeService fades, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsActive) return;
        if (terraceCamera == null || cameraBrain == null || speakers == null || lunaAnchor == null || fades == null)
            throw new InvalidOperationException("테라스 카메라·화자 기준점·페이드 서비스를 연결하세요.");
        previousCameraEnabled = terraceCamera.enabled;
        await fades.FadeOutAsync(fadeDuration, token);
        try
        {
            IsActive = true;
            terraceCamera.PreviousStateIsValid = false;
            terraceCamera.enabled = true;
            cameraBrain.ResetState(); // 검은 화면에서 즉시 전환하고 기본 카메라 블렌드는 유지함.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            await fades.FadeInAsync(fadeDuration, token);
        }
        catch
        {
            await ExitAsync(fades, CancellationToken.None);
            throw;
        }
    }

    public async UniTask ExitAsync(ISceneFadeService fades, CancellationToken token)
    {
        if (!IsActive) return;
        try { await fades.FadeOutAsync(fadeDuration, token); }
        finally
        {
            RestoreCamera();
            // 취소돼도 검은 화면과 테라스 카메라가 남지 않게 끝까지 복원함.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            await fades.FadeInAsync(fadeDuration, CancellationToken.None);
        }
    }

    private void RestoreCamera()
    {
        if (!IsActive) return;
        if (terraceCamera != null) terraceCamera.enabled = previousCameraEnabled;
        if (cameraBrain != null) cameraBrain.ResetState();
        IsActive = false;
    }

    private void OnDestroy() => RestoreCamera();
}
