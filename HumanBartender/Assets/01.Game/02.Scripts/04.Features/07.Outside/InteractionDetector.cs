using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 플레이어 주변의 상호작용 가능 대상(IInteractable)을 원형 범위로 감지하고, 거리·우선순위 기반으로
/// 가장 적절한 대상을 현재 타겟으로 유지하며 포커스 진입/이탈 및 타겟 변경 이벤트를 발생시킨다.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private InteractorEvent interactPressedChannel;
    [SerializeField] private InteractableEvent OnInteractedTargetChannel;

    [Header("Event")]
    [SerializeField] private InteractableEvent OnTargetChanged;
    [SerializeField] private VoidEvent PlayerStopEvent;

    [Header("References")]
    [SerializeField] private Transform origin;
    [SerializeField] private Player _player;

    [Header("Detection")]
    [Tooltip("감지 반경")]
    [SerializeField] private float detectionRadius = 1.5f;

    [Tooltip("감지 오프셋")]
    [SerializeField] private Vector2 detectionOffset = Vector2.zero;
    [SerializeField] private LayerMask interactableLayer = ~0;

    [Tooltip("트리거 콜라이더도 감지 대상에 포함할지 여부")]
    [SerializeField] private bool detectTriggers = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    

    private readonly Collider2D[] hitBuffer = new Collider2D[16];
    private readonly HashSet<IInteractable> currentFrameCandidates = new();
    private IInteractable currentTarget;
    private ContactFilter2D contactFilter;

    public IInteractable CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null;

    private void Awake()
    {
        if (origin == null) origin = transform;
        RebuildContactFilter();
    }

    private void OnValidate()
    {
        RebuildContactFilter();
    }

    /// <summary>인스펙터 값(레이어/트리거 감지 여부) 변경에 맞춰 ContactFilter2D를 다시 구성한다.</summary>
    private void RebuildContactFilter()
    {
        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = interactableLayer,
            useTriggers = detectTriggers,
        };
        contactFilter.useDepth = false;
    }

    private void OnEnable()
    {
        if (interactPressedChannel != null)
            interactPressedChannel.OnRaised += HandleInteractInput;

        if (OnInteractedTargetChannel != null)
            OnInteractedTargetChannel.OnRaised += InteractedTarget;
    }

    private void OnDisable()
    {
        if (interactPressedChannel != null)
            interactPressedChannel.OnRaised -= HandleInteractInput;

        if (OnInteractedTargetChannel != null)
            OnInteractedTargetChannel.OnRaised -= InteractedTarget;

        if (currentTarget != null)
        {
            currentTarget.OnFocusExit();
            currentTarget = null;
            OnTargetChanged?.Raise(null);
        }
        currentFrameCandidates.Clear();
    }

    /// <summary>매 프레임 외부(Player 컨트롤러 등)에서 호출: 후보 스캔 -> 최적 타겟 갱신 순으로 처리한다.</summary>
    public void Handle()
    {
        ScanCandidates();
        UpdateCurrentTarget();

    }

    /// <summary>감지 범위 내 콜라이더를 원형으로 검사해 사용 가능한 IInteractable 후보 목록을 갱신한다.</summary>
    private void ScanCandidates()
    {
        currentFrameCandidates.Clear();

        Vector2 center = (Vector2)origin.position + detectionOffset;
        int hitCount = Physics2D.OverlapCircle(center, detectionRadius, contactFilter, hitBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            var col = hitBuffer[i];
            if (col == null) continue;

            if (!col.TryGetComponent<IInteractable>(out var interactable))
            {
                interactable = col.GetComponentInParent<IInteractable>();
            }
            
            if (interactable == null) continue;
            if (!interactable.IsAvaliable) continue;
            if (!interactable.isInteract) continue;
            if (col.GetComponentInParent<InteractiveEntity>().ActivationMode == EActivationMode.Proximity)
            {
                col.GetComponentInParent<InteractiveEntity>().Interact(_player);
                continue;
            }
            currentFrameCandidates.Add(interactable);
        }
    }
    /// <summary>
    /// 후보 중 (거리 - 우선순위*0.01)이 가장 작은 대상을 최적 타겟으로 선택한다.
    /// 타겟이 바뀌면 이전 타겟의 OnFocusExit, 새 타겟의 OnFocusEnter를 호출하고 변경 이벤트를 발생시킨다.
    /// </summary>
    private void UpdateCurrentTarget()
    {
        IInteractable best = null;
        float bestScore = float.PositiveInfinity;
        Vector2 originPos = origin.position;

        foreach (var c in currentFrameCandidates)
        {
            Vector2 toTarget = (Vector2)c.Transform.position - originPos;
            float distance = toTarget.magnitude;

            float score = distance;

            score -= c.Priority * 0.01f;

            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (!ReferenceEquals(best, currentTarget))
        {
            if (best != null && !best.IsAvaliable) return;

            currentTarget?.OnFocusExit();
            currentTarget = best;
            currentTarget?.OnFocusEnter();
            OnTargetChanged?.Raise(currentTarget);
        }
    }

    /// <summary>
    /// Interaction 중 하이라이트 및 버튼 숨기기
    /// </summary>
    /// <param name="target"></param>
    public void InteractedTarget(IInteractable target = null)
    {
        target?.OnFocusExit();
        currentTarget = null;

        OnTargetChanged?.Raise(currentTarget);
    }

    /// <summary>상호작용 입력 이벤트 콜백. 현재 타겟이 유효하면 플레이어를 멈추고 상호작용을 실행한다.</summary>
    private void HandleInteractInput(IInteractor interactor)
    {
        if (currentTarget == null || !currentTarget.IsAvaliable) return;
        PlayerStopEvent?.Raise(new Void());
        currentTarget.Interact(interactor);
    }
}
