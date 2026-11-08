using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>테라스 진입 이동을 마친 뒤 대사를 시작하고 종료 시 실내 카메라를 복원함.</summary>
[DisallowMultipleComponent]
public sealed class HomeTerracePresentation : MonoBehaviour
{
    [Header("테라스 화면 연결")]
    [SerializeField, Tooltip("테라스를 비추는 가상 카메라입니다.")] private CinemachineCamera terraceCamera;
    [SerializeField, Tooltip("검은 화면에서 카메라 전환을 적용할 Brain입니다.")] private CinemachineBrain cameraBrain;
    [SerializeField, Tooltip("테라스 대사의 화자 기준점입니다.")] private WorldDialogueSpeakers speakers;
    [SerializeField, Tooltip("테라스에서 루나의 대사를 표시할 기준점입니다.")] private Transform lunaAnchor;
    [SerializeField, Tooltip("다음 날 실내에서 깨어날 위치입니다.")] private Transform wakeUpSpawn;
    [SerializeField, Min(0), Tooltip("테라스 진입·복귀 페이드 시간(초)입니다.")] private float fadeDuration = .35f;

    [Header("대사 전 테라스 진입 이동")]
    [SerializeField, Tooltip("Terrace_BG_City 바로 아래의 Terrace 루트입니다. 이 Transform과 모든 하위 오브젝트를 함께 이동합니다.")] private Transform terraceForeground;
    [SerializeField, Tooltip("페이드 인 전에 배치할 부모 기준 로컬 X 좌표입니다. Y/Z는 유지합니다.")] private float entranceStartX = -1.8f;
    [SerializeField, Tooltip("진입 완료·퇴장 시의 부모 기준 로컬 X 좌표입니다.")] private float entranceEndX = 0;
    [SerializeField, Min(0), Tooltip("화면이 밝아진 뒤 이동할 시간(초)입니다. 0이면 즉시 도착합니다.")] private float entranceDuration = 3f;
    [SerializeField, Tooltip("진입 이동의 가속·감속 곡선입니다.")] private Ease entranceEase = Ease.InOutSine;
    [SerializeField, Tooltip("진입 이동을 마친 뒤 재생할 테라스 이야기 ID입니다.")] private string[] storyIds = { "night0", "night1", "night2", "ending" };
    private bool previousCameraEnabled;
    public bool IsActive { get; private set; }
    public WorldDialogueSpeakers Speakers => speakers;
    public Transform LunaAnchor => lunaAnchor;

    public bool ContainsStory(string id) => Array.IndexOf(storyIds ?? Array.Empty<string>(), id) >= 0;

    /// <summary>취침 페이드의 검은 화면에서 다음 날 위치와 실내 카메라를 준비함.</summary>
    public void PrepareMorning(Player player)
    {
        if (player == null || wakeUpSpawn == null)
            throw new InvalidOperationException("[HomeTerrace] Home의 다음 날 소파 앞 기상 위치를 연결하세요.");
        player.PrepareWorldInteraction();
        player.transform.position = wakeUpSpawn.position;
        RestoreCamera();
    }

    public async UniTask EnterAsync(ISceneFadeService fades, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsActive) return;
        if (terraceCamera == null || cameraBrain == null || speakers == null || lunaAnchor == null || terraceForeground == null || fades == null)
            throw new InvalidOperationException("[HomeTerrace] 테라스 카메라·화자 기준점·Terrace·페이드 서비스를 연결하세요.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        token = lifetime.Token;
        previousCameraEnabled = terraceCamera.enabled;
        await fades.FadeOutAsync(fadeDuration, token);
        try
        {
            IsActive = true;
            SetForegroundX(entranceStartX);
            terraceCamera.PreviousStateIsValid = false;
            terraceCamera.enabled = true;
            cameraBrain.ResetState(); // 검은 화면에서 즉시 전환하고 기본 카메라 블렌드는 유지함.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            await fades.FadeInAsync(fadeDuration, token);
            if (entranceDuration > 0)
                await terraceForeground.DOLocalMoveX(entranceEndX, entranceDuration)
                    .SetEase(entranceEase).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
            token.ThrowIfCancellationRequested();
            SetForegroundX(entranceEndX);
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
        SetForegroundX(entranceEndX);
        if (terraceCamera != null) terraceCamera.enabled = previousCameraEnabled;
        if (cameraBrain != null) cameraBrain.ResetState();
        IsActive = false;
    }

    private void SetForegroundX(float x)
    {
        if (terraceForeground == null) return;
        var position = terraceForeground.localPosition;
        position.x = x;
        terraceForeground.localPosition = position;
    }

    private void OnDestroy() => RestoreCamera();
}
