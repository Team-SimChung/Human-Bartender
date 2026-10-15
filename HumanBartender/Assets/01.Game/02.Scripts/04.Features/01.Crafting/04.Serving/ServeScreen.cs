using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServeScreen : MonoBehaviour
{
    [Serializable]
    private struct AnimationEntry
    {
        public string id;
        public AnimationClip clip;
    }

    [SerializeField] private List<AnimationEntry> _animations = new();
    [SerializeField] private Animation ani;
    [SerializeField] private CraftFlowController craftFlow;
    [Tooltip("켜면 버리기 후 이전 잔·도구·재료 선택을 유지하고 첫 기믹부터 다시 제작한다.")]
    [SerializeField] private bool retryOnDiscard = true;
    [SerializeField]
    private Button Restart;
    [SerializeField]
    private Button Accept;
    [SerializeField]
    private TextMeshProUGUI Grade;

    [SerializeField] private GameObject BG;

    private const string AnimationName = "Serve";
    private const string FallbackId = "cosmopolitan";
    private AnimationClip runtimeClip;
    private bool initialized;
    private CraftedDrink currentDrink;

    public bool IsShowing => currentDrink != null;

    void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (ani == null) ani = GetComponent<Animation>();
        if (ani != null) ani.playAutomatically = false;
        if (Restart != null) Restart.onClick.AddListener(Discard);
        if (Accept != null) Accept.onClick.AddListener(Provide);
        Reset();
    }

    public void Play(CraftedDrink drink)
    {
        TryPlay(drink);
    }

    public bool TryPlay(CraftedDrink drink)
    {
        if (drink == null)
        {
            Debug.LogError("[ServeScreen] 재생할 칵테일 데이터가 없습니다.", this);
            return false;
        }

        Initialize();
        if (ani == null) ani = GetComponent<Animation>();
        if (ani == null)
        {
            Debug.LogError("[ServeScreen] Ani에 Animation 컴포넌트를 연결해 주세요.", this);
            return false;
        }

        AnimationClip clip = FindAnimation(drink.CocktailId) ?? FindAnimation(FallbackId);
        if (clip == null)
        {
            Debug.LogError($"[ServeScreen] '{drink.CocktailId}' 또는 '{FallbackId}' 클립을 등록해 주세요.", this);
            return false;
        }

        gameObject.SetActive(true);
        if (BG != null) BG.SetActive(true);
        SetControlsActive(false);
        StopAnimation();
        currentDrink = drink;
        UpdateGrade();

        // Animation은 Legacy 클립을 사용하므로 원본 에셋 대신 복사본을 변환한다.
        runtimeClip = Instantiate(clip);
        runtimeClip.legacy = true;
        runtimeClip.wrapMode = WrapMode.ClampForever;
        EnsureCompletionEvent(runtimeClip);

        ani.enabled = true;
        ani.playAutomatically = false;
        ani.AddClip(runtimeClip, AnimationName);
        ani.clip = runtimeClip;
        if (!ani.Play(AnimationName))
        {
            Debug.LogError("[ServeScreen] 서빙 애니메이션을 재생할 수 없습니다.", this);
            Reset();
            return false;
        }
        return true;
    }

    public void Apply()
    {
        if (currentDrink == null) return;
        UpdateGrade();
        SetControlsActive(true);
        if (Accept != null) Accept.interactable = currentDrink.CraftGrade.HasValue;
    }

    public void Discard()
    {
        if (currentDrink == null || craftFlow == null) return;
        var drink = currentDrink;
        if (craftFlow.TryDiscardPendingDrink(drink, retryOnDiscard) && ReferenceEquals(currentDrink, drink))
            Reset();
    }

    public void Provide()
    {
        if (currentDrink == null || craftFlow == null) return;
        if (craftFlow.TryAcceptPendingDrink(currentDrink)) Reset();
    }

    public void Reset()
    {
        StopAnimation();
        currentDrink = null;
        SetControlsActive(false);
        if (BG != null) BG.SetActive(false);
    }

    private AnimationClip FindAnimation(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _animations == null) return null;

        foreach (var entry in _animations)
        {
            if (entry.id == id && entry.clip != null)
                return entry.clip;
        }

        return null;
    }

    private void SetControlsActive(bool active)
    {
        if (Restart != null) Restart.gameObject.SetActive(active);
        if (Accept != null) Accept.gameObject.SetActive(active);
        if (Grade != null) Grade.gameObject.SetActive(active);
    }

    private void UpdateGrade()
    {
        if (Grade == null) return;
        Grade.text = currentDrink?.CraftGrade?.ToString().ToUpperInvariant() ?? "판정 불가";
        Grade.color = currentDrink?.CraftGrade switch
        {
            ENewGrade.Excellent => new Color32(246, 220, 149, 255),
            ENewGrade.Good => new Color32(188, 222, 185, 255),
            ENewGrade.Decent => new Color32(230, 216, 175, 255),
            ENewGrade.Poor => new Color32(231, 166, 120, 255),
            ENewGrade.Sewage => new Color32(190, 136, 163, 255),
            _ => new Color32(210, 210, 210, 255)
        };
    }

    private static void EnsureCompletionEvent(AnimationClip clip)
    {
        var events = new List<AnimationEvent>();
        bool hasCompletion = false;
        foreach (var animationEvent in clip.events)
        {
            // Animation 창에서 아직 함수를 고르지 않은 빈 이벤트는 재생 오류를 내므로 제외한다.
            if (string.IsNullOrEmpty(animationEvent.functionName)) continue;
            events.Add(animationEvent);
            if (animationEvent.functionName == nameof(Apply)) hasCompletion = true;
        }
        if (!hasCompletion)
            events.Add(new AnimationEvent { time = clip.length, functionName = nameof(Apply) });
        clip.events = events.ToArray();
    }

    private void StopAnimation()
    {
        if (runtimeClip == null) return;

        if (ani != null)
        {
            ani.Stop();
            ani.RemoveClip(AnimationName);
            if (ani.clip == runtimeClip) ani.clip = null;
        }

        if (Application.isPlaying) Destroy(runtimeClip);
        else DestroyImmediate(runtimeClip);
        runtimeClip = null;
    }

    private void OnDisable() => StopAnimation();
    private void OnDestroy()
    {
        StopAnimation();
        if (Restart != null) Restart.onClick.RemoveListener(Discard);
        if (Accept != null) Accept.onClick.RemoveListener(Provide);
    }
}
