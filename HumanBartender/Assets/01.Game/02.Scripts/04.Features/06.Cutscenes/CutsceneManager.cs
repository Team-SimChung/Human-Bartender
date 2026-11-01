using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using HumanBartender.CutsceneStudio;

/// <summary>IEffectPlayer.PlayEffectAsync()에서 사용하는 화면 이펙트 유형을 정의함.</summary>
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
/// 컷씬 재생과 화면 이펙트를 담당하며 IEffectPlayer와 ICutScenePlayer를 구현함.
/// 등록된 Cutscene Studio 컷씬을 재생함.
/// 화면 이펙트는 effectOverlay와 DOTween으로 처리함.
/// </summary>
public class CutSceneManager : MonoBehaviour, IEffectPlayer, ICutScenePlayer
{

    private CancellationTokenSource effectRun;

    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Image effectOverlay;           // 화면 전체 페이드와 플래시에 쓰임.
    [SerializeField] private List<Image> images = new();

    private CancellationTokenSource _cts;

    // 앵커 유형별 기준 좌표를 보관함.
    private readonly Dictionary<AnchorType, Vector2> anchorPreset = new()
    {
        { AnchorType.Center,       new Vector2(0.5f, 0.5f) },
        { AnchorType.Left,         new Vector2(0.0f, 0.5f) },
        { AnchorType.Right,        new Vector2(1.0f, 0.5f) },
        { AnchorType.TopLeft,     new Vector2(0.0f, 1.0f) },
        { AnchorType.TopRight,    new Vector2(1.0f, 1.0f) },
        { AnchorType.BottomLeft,  new Vector2(0.0f, 0.0f) },
        { AnchorType.BottomRight, new Vector2(1.0f, 0.0f) },
    };

    private Queue<Image> imagePool = new();
    private Dictionary<string, Image> activeImages = new();   // 이미지 ID로 사용 중인 이미지를 조회함.

    private void Awake()
    {
        imagePool = new Queue<Image>(images);
        ResetImages();

    }

    public void OnContinueTimeline()
    {
        var studioPlayer = GetComponent<StudioPlayer>();
        if (studioPlayer != null && studioPlayer.IsPlaying) studioPlayer.Continue();
    }

    public void ClearCutScene()
    {
        _cts?.Cancel();
        GetComponent<StudioPlayer>()?.Stop();
        effectRun?.Cancel();
        ResetImages();
    }

    private void OnDisable() => ClearCutScene();

    public async UniTask PlayCutScene(string id, UniTaskCompletionSource tcs = null, CancellationToken token = default)
    {
        if (_cts != null) throw new InvalidOperationException("A cutscene is already playing; cancel and await it first.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        _cts = source;
        using var audioScope = GameAudioContext.Cutscene(GameAudioRules.SuppressesMusic(id));
        try
        {
            source.Token.ThrowIfCancellationRequested();
            if (!await StudioPlaybackService.TryPlayAsync(this, id, source.Token))
                throw new InvalidOperationException("Missing Cutscene Studio sequence: " + id);
            source.Token.ThrowIfCancellationRequested();
            tcs?.TrySetResult();
        }
        catch (OperationCanceledException) { tcs?.TrySetCanceled(source.Token); throw; }
        catch (Exception e) { tcs?.TrySetException(e); throw; }
        finally { if (ReferenceEquals(_cts, source)) _cts = null; }
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

    private async UniTask ExecuteEffect(EEffectType type, float duration, float intensity, CancellationToken token)
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

    // 이미지 풀에서 사용한 이미지를 반환하고 다시 꺼내 쓰는 데 쓰임.

    private Image GetPooledImage()
    {
        if (imagePool.Count == 0)
        {
            Debug.LogWarning("[CutsceneManager] 이미지 풀이 비어있음");
            return null;
        }
        return imagePool.Dequeue();
    }
    private void ReturnToPool(string imageId, Image img)
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
