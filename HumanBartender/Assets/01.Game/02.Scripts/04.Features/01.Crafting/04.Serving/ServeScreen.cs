using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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
    [SerializeField] private Animator animator;
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

    [Header("전용 클립이 없는 칵테일")]
    [Tooltip("클립이 없는 칵테일의 완성 그림을 띄우는 Image. BG 아래 drink 바로 뒤에 둡니다. 웹 serve-fallback과 같은 연출입니다.")]
    [SerializeField] private Image fallbackDrinkImage;
    [Tooltip("클립이 스프라이트를 바꾸는 drink Image. 정지 그림을 보일 때는 끕니다.")]
    [SerializeField] private Image animatedDrinkImage;
    [Tooltip("칵테일 ID → 완성 그림(01.Finished) 카탈로그.")]
    [SerializeField] private CocktailRecipeVisualCatalog visuals;
    [Tooltip("정지 그림 등장 시간(초). 끝나면 등급과 버튼을 보입니다. 웹 1.5초.")]
    [Min(0)] [SerializeField] private float fallbackDuration = 1.5f;
    [Tooltip("등장 시작 크기 비율. 1까지 커집니다. 웹 0.72.")]
    [Range(0, 1)] [SerializeField] private float fallbackStartScale = .72f;
    [Tooltip("등장 시작 불투명도. 1까지 진해집니다. 웹 0.2.")]
    [Range(0, 1)] [SerializeField] private float fallbackStartAlpha = .2f;
    [Tooltip("등장하는 동안 아래로 내려오는 거리(px). 웹 80px × 0.75.")]
    [SerializeField] private float fallbackDropY = 60f;

    // 정지 그림 참조가 없는 기존 씬·테스트에서만 쓰는 대체 클립.
    private const string FallbackId = "cosmopolitan";
    private Sequence fallbackSequence;
    private Vector2 fallbackHome;
    private bool fallbackHomeKnown;
    private bool showingFallback;
    private PlayableGraph servingGraph;
    private AnimationClipPlayable servingPlayable;
    private float animationDuration;
    private bool animationCompleted;
    private bool initialized;
    private CraftedDrink currentDrink;

    public bool IsShowing => currentDrink != null;

    void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (animator == null) animator = GetComponent<Animator>();
        if (Restart != null) Restart.onClick.AddListener(Discard);
        if (Accept != null) Accept.onClick.AddListener(Provide);
        Reset();
    }

    bool CanShowStill => fallbackDrinkImage != null && animatedDrinkImage != null && visuals != null;

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
        AnimationClip clip = FindAnimation(drink.CocktailId);
        if (clip == null && CanShowStill)
        {
            BeginShow(drink);
            PlayStill(drink.CocktailId);
            return true;
        }

        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[ServeScreen] Animator를 연결해 주세요.", this);
            return false;
        }

        if (clip == null)
        {
            Debug.LogError($"[ServeScreen] '{drink.CocktailId}' 전용 클립이 없고 정지 그림도 연결되지 않아 '{FallbackId}' 클립으로 대신합니다. " +
                           "Setup Serve Fallback Drink 메뉴를 실행하세요.", this);
            clip = FindAnimation(FallbackId);
        }
        if (clip == null)
        {
            Debug.LogError($"[ServeScreen] '{drink.CocktailId}' 또는 '{FallbackId}' 클립을 등록해 주세요.", this);
            return false;
        }

        BeginShow(drink);
        if (animatedDrinkImage != null) animatedDrinkImage.enabled = true;

        // UI Image의 스프라이트 곡선은 Legacy 변환 없이 Animator에 재생한다.
        animator.enabled = true;
        animationDuration = clip.length;
        animationCompleted = false;
        servingGraph = PlayableGraph.Create($"Serve_{drink.CocktailId}");
        servingGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        servingPlayable = AnimationClipPlayable.Create(servingGraph, clip);
        servingPlayable.SetApplyFootIK(false);
        servingPlayable.SetDuration(animationDuration);
        var output = AnimationPlayableOutput.Create(servingGraph, "Serving", animator);
        output.SetSourcePlayable(servingPlayable);
        servingGraph.Play();
        servingGraph.Evaluate(0f);
        return true;
    }

    private void BeginShow(CraftedDrink drink)
    {
        gameObject.SetActive(true);
        if (BG != null) BG.SetActive(true);
        SetControlsActive(false);
        StopAnimation();
        currentDrink = drink;
        UpdateGrade();
    }

    // 웹 serve-fallback: 완성 그림이 작고 흐린 상태에서 커지며 내려온 뒤 등급·버튼을 보인다.
    private void PlayStill(string cocktailId)
    {
        Sprite sprite = visuals.GetCocktailSprite(cocktailId);
        if (sprite == null) Debug.LogWarning($"[ServeScreen] '{cocktailId}'의 완성 그림이 카탈로그에 없어 그림 없이 진행합니다.", this);
        animatedDrinkImage.enabled = false;
        fallbackDrinkImage.sprite = sprite;
        fallbackDrinkImage.enabled = sprite != null;
        fallbackDrinkImage.gameObject.SetActive(true);
        showingFallback = true;

        RectTransform rect = fallbackDrinkImage.rectTransform;
        // 씬에 저장된 자리를 기준으로 삼는다. 연출이 끝나거나 닫히면 이 자리로 되돌린다.
        if (!fallbackHomeKnown)
        {
            fallbackHome = rect.anchoredPosition;
            fallbackHomeKnown = true;
        }
        rect.anchoredPosition = fallbackHome;
        rect.localScale = Vector3.one * fallbackStartScale;
        SetFallbackAlpha(fallbackStartAlpha);
        if (fallbackDuration <= 0f || !Application.isPlaying)
        {
            FinishStill();
            return;
        }

        fallbackSequence = DOTween.Sequence()
            .Join(rect.DOAnchorPosY(fallbackHome.y - fallbackDropY, fallbackDuration).SetEase(Ease.Linear))
            .Join(rect.DOScale(1f, fallbackDuration).SetEase(Ease.Linear))
            .Join(fallbackDrinkImage.DOFade(1f, fallbackDuration).SetEase(Ease.Linear))
            .OnComplete(FinishStill);
    }

    private void FinishStill()
    {
        fallbackSequence = null;
        RectTransform rect = fallbackDrinkImage.rectTransform;
        rect.anchoredPosition = fallbackHome + Vector2.down * fallbackDropY;
        rect.localScale = Vector3.one;
        SetFallbackAlpha(1f);
        animationCompleted = true;
        Apply();
    }

    private void SetFallbackAlpha(float alpha)
    {
        Color color = fallbackDrinkImage.color;
        color.a = alpha;
        fallbackDrinkImage.color = color;
    }

    private void HideStill()
    {
        fallbackSequence?.Kill();
        fallbackSequence = null;
        if (!showingFallback || fallbackDrinkImage == null) return;
        showingFallback = false;
        RectTransform rect = fallbackDrinkImage.rectTransform;
        rect.anchoredPosition = fallbackHome;
        rect.localScale = Vector3.one;
        SetFallbackAlpha(1f);
        fallbackDrinkImage.gameObject.SetActive(false);
        if (animatedDrinkImage != null) animatedDrinkImage.enabled = true;
    }

    private void Update()
    {
        if (!servingGraph.IsValid() || animationCompleted || !servingPlayable.IsValid()) return;
        if (servingPlayable.GetTime() < animationDuration) return;

        servingPlayable.SetTime(animationDuration);
        servingGraph.Evaluate(0f);
        servingGraph.Stop();
        animationCompleted = true;
        Apply();
    }

    public void Apply()
    {
        if (currentDrink == null) return;
        UpdateGrade();
        SetControlsActive(true);
        SetDecisionInteractable(craftFlow == null || !craftFlow.IsResolvingDiscard);
    }

    public void Discard()
    {
        if (currentDrink == null || craftFlow == null || craftFlow.IsResolvingDiscard) return;
        craftFlow.TryDiscardPendingDrink(currentDrink, retryOnDiscard);
    }

    public void Provide()
    {
        if (currentDrink == null || craftFlow == null || craftFlow.IsResolvingDiscard) return;
        if (craftFlow.TryAcceptPendingDrink(currentDrink)) Reset();
    }

    public void SetDecisionInteractable(bool interactable)
    {
        if (Restart != null) Restart.interactable = interactable;
        if (Accept != null) Accept.interactable = interactable && currentDrink?.CraftGrade.HasValue == true;
    }

    public void ResetIfShowing(CraftedDrink drink)
    {
        if (ReferenceEquals(currentDrink, drink)) Reset();
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

    private void StopAnimation()
    {
        HideStill();
        if (servingGraph.IsValid()) servingGraph.Destroy();
        servingPlayable = default;
        animationCompleted = false;
    }

    private void OnDisable() => StopAnimation();
    private void OnDestroy()
    {
        StopAnimation();
        if (Restart != null) Restart.onClick.RemoveListener(Discard);
        if (Accept != null) Accept.onClick.RemoveListener(Provide);
    }
}
