using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using VContainer;




/// <summary>화면 이펙트 유형을 나타내는 열거형. IEffectPlayer.PlayEffectAsync()에서 사용된다.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum EEffectType
{
    None,

    [EnumMember(Value = "fade_out")]
    FadeOut,
    [EnumMember(Value = "fade_in")]
    FadeIn,
    [EnumMember(Value = "flash_white")]
    FlashWhite,
    [EnumMember(Value = "screen_shake")]
    ScreenShake,
    [EnumMember(Value = "zoom_pulse")]
    ZoomPulse,
    [EnumMember(Value = "vignette")]
    Vignette,
    [EnumMember(Value = "dim")]
    Dim,
    [EnumMember(Value = "chromatic")]
    Chromatic
}




/// <summary>
/// 컷씬 재생과 화면 이펙트를 담당하는 메인 매니저. IEffectPlayer와 ICutScenePlayer를 구현한다.
/// json/cutscenes.json이 말하는 kind(timeline·sprite)에 맞춰 재생하고, FadeIn/FadeOut/FlashWhite/ScreenShake 등
/// 다양한 화면 이펙트를 effectOverlay Image와 DOTween으로 수행한다.
/// </summary>
public class CutSceneManager : MonoBehaviour, IEffectPlayer, ICutScenePlayer
{
    [SerializeField] NewCutSceneDataSO data;

    [SerializeField] SpriteAnimationManager spriteAnimationManager;
    [SerializeField] SpineAnimationManager spineAnimationManager;
    [SerializeField] CutSceneTimelineManager timelineManager;

    private CancellationTokenSource effectRun;


    private const string ANIM_SLOT = "SpriteAnim";


    [SerializeField] Canvas cutSceneCanvas;
    [SerializeField] RectTransform canvasRect;
    [SerializeField] Image effectOverlay;           // 화면 전체 페이드/플래시용 단일 오버레이
    [SerializeField] List<Image> images = new();

    [SerializeField] float padding_X = 0;
    [SerializeField] float padding_Y = 0;


    private CancellationTokenSource _cts;


    // ── Anchor 프리셋 ─────────────────────────────────────────────────
    readonly Dictionary<AnchorType, Vector2> anchorPreset = new()
    {
        { AnchorType.Center,       new Vector2(0.5f, 0.5f) },
        { AnchorType.Left,         new Vector2(0.0f, 0.5f) },
        { AnchorType.Right,        new Vector2(1.0f, 0.5f) },
        { AnchorType.TopLeft,     new Vector2(0.0f, 1.0f) },
        { AnchorType.TopRight,    new Vector2(1.0f, 1.0f) },
        { AnchorType.BottomLeft,  new Vector2(0.0f, 0.0f) },
        { AnchorType.BottomRight, new Vector2(1.0f, 0.0f) },
    };

    Queue<Image> imagePool = new();
    Dictionary<string, Image> activeImages = new();   // imageId → Image


    void Awake()
    {
        imagePool = new Queue<Image>(images);
        ResetImages();


        // Sprite animator is initialized lazily when its first clip is played.
        if (spineAnimationManager != null) spineAnimationManager.Initialize();
    }

    public void OnContinueTimeline()
    {
        timelineManager.OnContinueCutScene();
    }



    public void ClearCutScene()
    {
        _cts?.Cancel();
        effectRun?.Cancel();
        if (timelineManager != null) timelineManager.StopTimeline();
        ResetImages();
        spriteAnimationManager?.SetInactive();
        spriteAnimationManager?.ActiveSelf(false);
        if (spineAnimationManager != null)
        {
            spineAnimationManager.SetInactive();
            spineAnimationManager.ActiveSelf(false);
        }
    }

    void OnDisable() => ClearCutScene();
    void OnDestroy() => spriteAnimationManager?.Release();

    public async UniTask PlayCutScene(string id, UniTaskCompletionSource tcs = null, CancellationToken token = default)
    {
        if (_cts != null) throw new InvalidOperationException("A cutscene is already playing; cancel and await it first.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        _cts = source;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            if (data == null || !data.TryGet(id, out var cutScene)) throw new InvalidOperationException("Missing cutscene: " + id);
            if (cutSceneCanvas != null) cutSceneCanvas.worldCamera = Camera.main;
            switch (cutScene.Kind)
            {
                case ENewCutSceneKind.Timeline:
                    await PlayTimelineCutScene(cutScene, source.Token);
                    break;
                case ENewCutSceneKind.Sprite:
                    await PlaySpriteAnimationCutScene(cutScene, source.Token);
                    break;
                default: throw new NotSupportedException("Unsupported cutscene kind: " + cutScene.Kind + " (" + id + ")");
            }
            source.Token.ThrowIfCancellationRequested();
            tcs?.TrySetResult();
        }
        catch (OperationCanceledException) { tcs?.TrySetCanceled(source.Token); throw; }
        catch (Exception e) { tcs?.TrySetException(e); throw; }
        finally { if (ReferenceEquals(_cts, source)) _cts = null; }
    }

    /// <summary>Retains the loaded asset until the director has stopped and cleared its bindings.</summary>
    public async UniTask PlayTimelineCutScene(NewCutSceneRefData data, CancellationToken token, UniTaskCompletionSource tcs = null)
    {
        var outside = FindFirstObjectByType<OustideTimelineManager>();
        if (outside != null && outside.TryGetSceneTimeline(data.ResourceKey, out _))
        {
            try { await outside.PlayTimelineCutSceneAsync(data.Id, token); tcs?.TrySetResult(); }
            catch (OperationCanceledException) { tcs?.TrySetCanceled(token); throw; }
            catch (Exception e) { tcs?.TrySetException(e); throw; }
            return;
        }
        var handle = await ResourceLoader.TryLoadAsync<TimelineAsset>(data.ResourceKey, token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!handle.HasValue || timelineManager == null)
                throw new InvalidOperationException($"Cannot play timeline {data.Id} (resource_key={data.ResourceKey}).");
            await timelineManager.PlayTimelineCutSceneAsync(handle.Value.Result, token);
            tcs?.TrySetResult();
        }
        catch (OperationCanceledException) { tcs?.TrySetCanceled(token); throw; }
        catch (Exception e) { tcs?.TrySetException(e); throw; }
        finally { ResourceLoader.ReleaseHandle<TimelineAsset>(ref handle); }
    }

    async UniTask PlaySpriteAnimationCutScene(NewCutSceneRefData data, CancellationToken token)
    {
        var handle = await ResourceLoader.TryLoadAsync<AnimationClip>(data.ResourceKey, token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!handle.HasValue || spriteAnimationManager == null)
                throw new InvalidOperationException($"Cannot play sprite cutscene {data.Id} (resource_key={data.ResourceKey}).");
            spriteAnimationManager.EnsureInitialized();
            spriteAnimationManager.ActiveSelf(true);
            spriteAnimationManager.SetClip(ANIM_SLOT, handle);
            await spriteAnimationManager.PlayAnimation(ANIM_SLOT, token);
        }
        finally
        {
            spriteAnimationManager?.SetInactive();
            spriteAnimationManager?.ActiveSelf(false);
            ResourceLoader.ReleaseHandle<AnimationClip>(ref handle);
        }
    }

    public async UniTask PlayEffectAsync(EEffectType type, float duration, float intensity = 0f, CancellationToken token = default)
    {
        if (effectRun != null) throw new InvalidOperationException("A cutscene effect is already playing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        effectRun = source;
        var position = canvasRect != null ? canvasRect.anchoredPosition : Vector2.zero;
        var scale = canvasRect != null ? canvasRect.localScale : Vector3.one;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            await ExecuteEffect(type, duration, intensity, source.Token);
        }
        finally
        {
            if (canvasRect != null) { canvasRect.anchoredPosition = position; canvasRect.localScale = scale; }
            if (source.IsCancellationRequested && effectOverlay != null) effectOverlay.gameObject.SetActive(false);
            if (ReferenceEquals(effectRun, source)) effectRun = null;
        }
    }

    async UniTask ExecuteEffect(EEffectType type, float duration, float intensity, CancellationToken token)
    {
        switch (type)
        {
            case EEffectType.FadeOut:
                effectOverlay.color = new Color(1, 1, 1, 1);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(0f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                effectOverlay.gameObject.SetActive(false);
                break;

            case EEffectType.FadeIn:
                effectOverlay.color = new Color(1, 1, 1, 0);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(1f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEffectType.FlashWhite:
                effectOverlay.color = Color.white;
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(0f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                effectOverlay.gameObject.SetActive(false);
                break;

            case EEffectType.ScreenShake:
                await canvasRect.DOShakeAnchorPos(duration, intensity, 20, 90, false, true)
                                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEffectType.ZoomPulse:

                await canvasRect.DOScale(intensity, duration * 0.5f)
                                .SetEase(Ease.OutQuad)
                                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                await canvasRect.DOScale(1f, duration * 0.5f)
                                .SetEase(Ease.InQuad)
                                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEffectType.Vignette:
                effectOverlay.gameObject.SetActive(true);
                effectOverlay.color = new Color(0, 0, 0, 0);
                await effectOverlay.DOFade(0.7f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEffectType.Dim:
                effectOverlay.color = new Color(0, 0, 0, 0);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(intensity, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEffectType.Chromatic:
                Debug.Log("[CutsceneManager] chromatic — 현재 오버레이 근사치 사용 중");
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
                break;

            default:
                Debug.LogWarning($"[CutsceneManager] 알 수 없는 effect_type: {type}");
                break;
        }
    }


    public EEffectType ConvertStringToEffect(string input)
    {
        return input?.ToLowerInvariant() switch
        {
            "fade_in" => EEffectType.FadeIn,
            "fade_out" => EEffectType.FadeOut,
            "flash_white" => EEffectType.FlashWhite,
            "screen_shake" => EEffectType.ScreenShake,
            "zoom_pulse" => EEffectType.ZoomPulse,
            "vignette" => EEffectType.Vignette,
            "dim" => EEffectType.Dim,
            "chromatic" => EEffectType.Chromatic,
            _ => EEffectType.None 
        };
    }


    // ── Image 풀 관리 ─────────────────────────────────────────────────

    Image GetPooledImage()
    {
        if (imagePool.Count == 0)
        {
            Debug.LogWarning("[CutsceneManager] 이미지 풀이 비어있음");
            return null;
        }
        return imagePool.Dequeue();
    }
    void ReturnToPool(string imageId, Image img)
    {
        img.gameObject.SetActive(false);
        img.color = Color.white;
        img.GetComponent<RectTransform>().localScale = Vector3.one;
        activeImages.Remove(imageId);
        imagePool.Enqueue(img);
    }
    public void ResetImages()
    {
        foreach (var kvp in activeImages)
        {
            if (kvp.Value == null) continue;
            kvp.Value.gameObject.SetActive(false);
            imagePool.Enqueue(kvp.Value);
        }

        if (effectOverlay != null) effectOverlay.gameObject.SetActive(false);
        activeImages.Clear();
    }
}
