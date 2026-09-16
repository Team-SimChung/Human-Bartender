using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

enum SceneOperationKind
{
    Load,
    Unload,
}

enum FadeDirection
{
    In,
    Out,
}

/// <summary>
/// 페이드와 Unity 씬 작업을 하나의 수명으로 조정하는 전환 서비스.
/// 요청 하나가 페이드 아웃부터 씬 작업, 페이드 인, 입력 복구까지 소유한다.
/// </summary>
public class SceneTransitionManager : MonoBehaviour, ISceneTransitionService
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField, Min(0f)] float fadeDuration = 1f;

    readonly SceneTransitionCoordinator coordinator = new();

    public bool IsBusy
    {
        get { return coordinator.IsBusy; }
    }

    public SceneTransitionState CurrentState
    {
        get { return coordinator.State; }
    }

    public long CurrentOperationId
    {
        get { return coordinator.CurrentOperationId; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("[SceneTransition] Fade CanvasGroup 참조가 없습니다.", this);
            return;
        }

        fadeCanvasGroup.alpha = 1f;
        FadeInAsync().Forget(ReportException);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 기존 UnityEvent 및 호출부 호환 API. 새 호출부는 Request*로 수락 여부와 완료 결과를 확인한다.
    public void LoadScene(string sceneName, LoadSceneMode sceneMode = LoadSceneMode.Single)
    {
        SceneTransitionRequest request = RequestLoadScene(sceneName, sceneMode);
        Observe(request).Forget();
    }

    public void UnLoadScene(string sceneName)
    {
        UnloadScene(sceneName);
    }

    public void UnloadScene(string sceneName)
    {
        SceneTransitionRequest request = RequestUnloadScene(sceneName);
        Observe(request).Forget();
    }

    public SceneTransitionRequest RequestLoadScene(
        string sceneName,
        LoadSceneMode sceneMode = LoadSceneMode.Single,
        CancellationToken cancellationToken = default)
    {
        return RequestSceneOperation(sceneName, sceneMode, SceneOperationKind.Load, cancellationToken);
    }

    public SceneTransitionRequest RequestUnloadScene(
        string sceneName,
        CancellationToken cancellationToken = default)
    {
        return RequestSceneOperation(
            sceneName,
            LoadSceneMode.Single,
            SceneOperationKind.Unload,
            cancellationToken);
    }

    SceneTransitionRequest RequestSceneOperation(
        string sceneName,
        LoadSceneMode sceneMode,
        SceneOperationKind operationKind,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            SceneTransitionResult canceledResult = new(
                SceneTransitionOutcome.Canceled,
                sceneName,
                "요청 전에 취소되었습니다.");
            return new SceneTransitionRequest(canceledResult);
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            SceneTransitionResult rejectedResult = new(
                SceneTransitionOutcome.Rejected,
                sceneName,
                "씬 이름이 비어 있습니다.");
            return new SceneTransitionRequest(rejectedResult);
        }

        if (operationKind == SceneOperationKind.Load && !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneTransitionResult rejectedResult = new(
                SceneTransitionOutcome.Rejected,
                sceneName,
                $"빌드에 등록되지 않은 씬입니다: {sceneName}");
            return new SceneTransitionRequest(rejectedResult);
        }

        if (operationKind == SceneOperationKind.Unload && !SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneTransitionResult rejectedResult = new(
                SceneTransitionOutcome.Rejected,
                sceneName,
                $"로드되어 있지 않은 씬입니다: {sceneName}");
            return new SceneTransitionRequest(rejectedResult);
        }

        if (fadeCanvasGroup == null)
        {
            SceneTransitionResult rejectedResult = new(
                SceneTransitionOutcome.Rejected,
                sceneName,
                "Fade CanvasGroup 참조가 없습니다.");
            return new SceneTransitionRequest(rejectedResult);
        }

        if (!coordinator.TryBegin(out long operationId))
        {
            SceneTransitionResult rejectedResult = new(
                SceneTransitionOutcome.Rejected,
                sceneName,
                $"다른 전환 작업이 진행 중입니다: #{coordinator.CurrentOperationId} {coordinator.State}");
            return new SceneTransitionRequest(rejectedResult);
        }

        return new SceneTransitionRequest(true,
            RunSceneOperationAsync(operationId, sceneName, sceneMode, operationKind, cancellationToken));
    }

    public async UniTask<SceneTransitionResult> LoadSceneAsync(
        string sceneName,
        LoadSceneMode sceneMode = LoadSceneMode.Single,
        CancellationToken cancellationToken = default)
    {
        SceneTransitionRequest request = RequestLoadScene(sceneName, sceneMode, cancellationToken);
        SceneTransitionResult result = await request.Completion;
        return result;
    }

    public async UniTask<SceneTransitionResult> UnloadSceneAsync(
        string sceneName,
        CancellationToken cancellationToken = default)
    {
        SceneTransitionRequest request = RequestUnloadScene(sceneName, cancellationToken);
        SceneTransitionResult result = await request.Completion;
        return result;
    }

    public void FadeIn(float duration = -1f)
    {
        FadeInAsync(duration).Forget(ReportException);
    }

    public void FadeOut(float duration = -1f)
    {
        FadeOutAsync(duration).Forget(ReportException);
    }

    public async UniTask FadeInAsync(float duration = -1f, CancellationToken cancellationToken = default)
    {
        await RunStandaloneFadeAsync(FadeDirection.In, duration, cancellationToken);
    }

    public async UniTask FadeOutAsync(float duration = -1f, CancellationToken cancellationToken = default)
    {
        await RunStandaloneFadeAsync(FadeDirection.Out, duration, cancellationToken);
    }

    async UniTask<SceneTransitionResult> RunSceneOperationAsync(
        long operationId,
        string sceneName,
        LoadSceneMode sceneMode,
        SceneOperationKind operationKind,
        CancellationToken cancellationToken)
    {
        float restoreAlpha = fadeCanvasGroup.alpha;
        bool nativeOperationStarted = false;

        try
        {
            using var beforeNative = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, this.GetCancellationTokenOnDestroy());

            coordinator.TrySetState(operationId, SceneTransitionState.FadingOut);
            await FadeCoreAsync(1f, fadeDuration, beforeNative.Token);
            beforeNative.Token.ThrowIfCancellationRequested();

            // Unity의 씬 작업은 시작 후 취소할 수 없다. 이 지점부터는 호출자 취소를 성공으로 가장하지
            // 않고, 실제 씬 작업과 화면 복구가 끝날 때까지 기다린 뒤 최종 결과를 반환한다.
            nativeOperationStarted = true;
            AsyncOperation sceneOperation;
            if (operationKind == SceneOperationKind.Unload)
            {
                coordinator.TrySetState(operationId, SceneTransitionState.Unloading);
                sceneOperation = SceneManager.UnloadSceneAsync(sceneName);
            }
            else
            {
                coordinator.TrySetState(operationId, SceneTransitionState.Loading);
                sceneOperation = SceneManager.LoadSceneAsync(sceneName, sceneMode);
            }
            if (sceneOperation == null)
                throw new InvalidOperationException($"Unity가 씬 작업을 만들지 못했습니다: {sceneName}");

            await sceneOperation;

            coordinator.TrySetState(operationId, SceneTransitionState.FadingIn);
            await FadeCoreAsync(0f, fadeDuration, CancellationToken.None);
            return new SceneTransitionResult(SceneTransitionOutcome.Succeeded, sceneName);
        }
        catch (OperationCanceledException) when (!nativeOperationStarted)
        {
            RestoreVisibleState(restoreAlpha);
            return new SceneTransitionResult(
                SceneTransitionOutcome.Canceled,
                sceneName,
                "Unity 씬 작업 시작 전에 취소되었습니다.");
        }
        catch (Exception error)
        {
            RestoreVisibleState(restoreAlpha);
            return new SceneTransitionResult(SceneTransitionOutcome.Failed, sceneName, error.Message, error);
        }
        finally
        {
            if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = false;
            coordinator.TryEnd(operationId);
        }
    }

    async UniTask RunStandaloneFadeAsync(
        FadeDirection direction,
        float requestedDuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fadeCanvasGroup == null) throw new InvalidOperationException("Fade CanvasGroup 참조가 없습니다.");

        float targetAlpha = 1f;
        if (direction == FadeDirection.In)
        {
            targetAlpha = 0f;
        }

        float duration = requestedDuration;
        if (duration < 0f)
        {
            duration = fadeDuration;
        }
        duration = Mathf.Max(0f, duration);

        // 기존 Fade API는 전환 중 호출되면 아무 작업도 하지 않았다. 그 호환성은 유지하되,
        // 수락 여부가 필요한 씬 전환은 Request* API를 사용한다.
        if (!coordinator.TryBegin(out long operationId)) return;

        float restoreAlpha = fadeCanvasGroup.alpha;
        try
        {
            coordinator.TrySetState(operationId, SceneTransitionState.Fading);
            using var source = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, this.GetCancellationTokenOnDestroy());
            await FadeCoreAsync(targetAlpha, duration, source.Token);
        }
        catch
        {
            RestoreVisibleState(restoreAlpha);
            throw;
        }
        finally
        {
            if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = false;
            coordinator.TryEnd(operationId);
        }
    }

    async UniTask FadeCoreAsync(float targetAlpha, float duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fadeCanvasGroup == null) throw new InvalidOperationException("Fade CanvasGroup 참조가 없습니다.");

        fadeCanvasGroup.blocksRaycasts = true;
        if (Mathf.Approximately(fadeCanvasGroup.alpha, targetAlpha) || duration <= 0f)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            return;
        }

        await fadeCanvasGroup.DOFade(targetAlpha, duration)
            .SetEase(Ease.Linear)
            .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    void RestoreVisibleState(float alpha)
    {
        if (fadeCanvasGroup == null) return;
        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.alpha = alpha;
    }

    static async UniTask Observe(SceneTransitionRequest request)
    {
        SceneTransitionResult result = await request.Completion;
        if (result.Outcome == SceneTransitionOutcome.Rejected)
            Debug.LogWarning($"[SceneTransition] 요청 거절: {result.Message}");
        else if (result.Outcome == SceneTransitionOutcome.Failed)
            Debug.LogException(result.Error);
    }

    static void ReportException(Exception error)
    {
        if (error is not OperationCanceledException) Debug.LogException(error);
    }
}
