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

    private EEffectType curEffect = EEffectType.None;


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


        spriteAnimationManager.Initialize();
        spineAnimationManager.Initialize();
    }

    public void OnContinueTimeline()
    {
        timelineManager.OnContinueCutScene();
    }



    public void ClearCutScene()
    {
        ResetImages();
        spriteAnimationManager.ActiveSelf(false);
        spriteAnimationManager.SetInactive();

        spineAnimationManager.SetInactive();
        spineAnimationManager.ActiveSelf(false);

        curEffect = EEffectType.None;
    }

    public async UniTask PlayCutScene(
        string id,
        UniTaskCompletionSource tcs = null)
    {
        Logger.Log($"Play CutScene : {id}");

        if (!data.TryGet(id, out NewCutSceneRefData cutScene))
        {
            Debug.LogWarning($"[CutsceneManager] 컷씬 ID를 찾을 수 없음: {id}");
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var token = CancellationTokenSource
            .CreateLinkedTokenSource(_cts.Token, this.GetCancellationTokenOnDestroy())
            .Token;

        cutSceneCanvas.worldCamera = Camera.main;

        //Enter, Exit 구조 변경.

        switch (cutScene.Kind)
        {
            case ENewCutSceneKind.Timeline:
                await PlayTimelineCutScene(cutScene, token, tcs);
                break;

            case ENewCutSceneKind.Sprite:
                await PlaySpriteAnimationCutScene(cutScene, token, tcs);
                break;

            default:
                // gif는 아직 재생기가 없다. 조용히 지나가면 연출이 빠진 것을 알 수 없어 남긴다.
                Logger.LogWarning($"[CutsceneManager] 재생기가 없는 컷씬 종류입니다: {cutScene.Kind} ({id})");
                tcs?.TrySetResult();
                break;
        }
    }


    /// <summary>
    /// 타임라인 컷씬을 재생하고 길이만큼 기다린다.
    ///
    /// 어드레서블 키는 컷씬 id가 아니라 resource_key다. 둘은 다르다 — tl_intro_lab의 실제 키는
    /// "Intro lab"이다. 예전에는 id를 그대로 키로 썼는데, 그래서 이름을 바꾸면 로드가 끊겼다.
    /// </summary>
    public async UniTask PlayTimelineCutScene(
        NewCutSceneRefData data,
        CancellationToken token,
        UniTaskCompletionSource tcs = null)
    {
        var handle = await ResourceLoader.TryLoadAsync<TimelineAsset>(data.ResourceKey, token);

        if (handle.HasValue)
        {
            timelineManager.PlayTimelineCutScene(handle.Value.Result);
            await UniTask.WaitForSeconds((float)handle.Value.Result.duration);
        }
        else
        {
            Logger.LogWarning($"[CutsceneManager] 타임라인을 찾지 못했습니다: {data.Id} (resource_key={data.ResourceKey})");
            await UniTask.WaitForSeconds(1f);
        }

        if (tcs != null)
            tcs.TrySetResult();

        ResourceLoader.ReleaseHandle<TimelineAsset>(ref handle);
    }



    /// <summary>
    /// 추후 다른 값들에 대한 처리, position, loop, blocking 등
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="tcs"></param>
    /// <returns></returns>
    private async UniTask PlaySpriteAnimationCutScene(
        NewCutSceneRefData data,
        CancellationToken token,
        UniTaskCompletionSource tcs = null)
    {
        var handle = await ResourceLoader.TryLoadAsync<AnimationClip>(data.ResourceKey, token);

        if (handle.HasValue)
        {
            spriteAnimationManager.ActiveSelf(true);
            spriteAnimationManager.SetClip(ANIM_SLOT, handle);
            spriteAnimationManager.PlayAnimation(ANIM_SLOT, token);
            await PlayEffectAsync(EEffectType.FadeOut, 1f);
            await UniTask.WaitForSeconds(handle.Value.Result.length);
            await PlayEffectAsync(EEffectType.FadeOut, 1f);
        }
        else
        {
            Logger.LogWarning($"[CutsceneManager] 스프라이트 애니메이션을 찾지 못했습니다: {data.Id} (resource_key={data.ResourceKey})");
            await UniTask.WaitForSeconds(1f);
        }

        if (tcs != null)
            tcs.TrySetResult();

        ResourceLoader.ReleaseHandle<AnimationClip>(ref handle);
    }



    public async UniTask PlayEffectAsync(EEffectType type, float duration, float intensity = 0f)
    {
        await ExecuteEffect(type, duration, intensity);
    }
    async UniTask ExecuteEffect(EEffectType type, float duration = 1f, float intensity = 0)
    {
        //추후 타입 따라 처리를 다륵 ㅔ할 것인지, 지금은 현재와 동일 한거라면 return
        if (curEffect == type) return;

        curEffect = type;

        switch (type)
        {
            case EEffectType.FadeOut:
                effectOverlay.color = new Color(1, 1, 1, 1);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(0f, duration).ToUniTask();
                effectOverlay.gameObject.SetActive(false);
                break;

            case EEffectType.FadeIn:
                effectOverlay.color = new Color(1, 1, 1, 0);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(1f, duration).ToUniTask();
                break;

            case EEffectType.FlashWhite:
                effectOverlay.color = Color.white;
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(0f, duration).ToUniTask();
                effectOverlay.gameObject.SetActive(false);
                break;

            case EEffectType.ScreenShake:
                await canvasRect.DOShakeAnchorPos(duration, intensity, 20, 90, false, true)
                                .ToUniTask();
                break;

            case EEffectType.ZoomPulse:

                await canvasRect.DOScale(intensity, duration * 0.5f)
                                .SetEase(Ease.OutQuad)
                                .ToUniTask();
                await canvasRect.DOScale(1f, duration * 0.5f)
                                .SetEase(Ease.InQuad)
                                .ToUniTask();
                break;

            case EEffectType.Vignette:
                effectOverlay.gameObject.SetActive(true);
                effectOverlay.color = new Color(0, 0, 0, 0);
                await effectOverlay.DOFade(0.7f, duration).ToUniTask();
                break;

            case EEffectType.Dim:
                effectOverlay.color = new Color(0, 0, 0, 0);
                effectOverlay.gameObject.SetActive(true);
                await effectOverlay.DOFade(intensity, duration).ToUniTask();
                break;

            case EEffectType.Chromatic:
                Debug.Log("[CutsceneManager] chromatic — 현재 오버레이 근사치 사용 중");
                await UniTask.Delay(TimeSpan.FromSeconds(duration));
                break;

            default:
                Debug.LogWarning($"[CutsceneManager] 알 수 없는 effect_type: {type}");
                break;
        }
    }


    public EEffectType ConvertStringToEffect(string input)
    {
        return input.ToLower() switch
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
            kvp.Value.gameObject.SetActive(false);
            imagePool.Enqueue(kvp.Value);
        }

        effectOverlay.gameObject.SetActive(false);
        activeImages.Clear();
    }
}
