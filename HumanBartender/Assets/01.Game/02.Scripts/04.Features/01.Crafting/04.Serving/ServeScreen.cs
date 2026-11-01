using System;
using System.Collections.Generic;
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

    private const string FallbackId = "cosmopolitan";
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
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[ServeScreen] Animator를 연결해 주세요.", this);
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

    private void StopAnimation()
    {
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
