using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum NpcIdleFacing
{
    [InspectorName("배치한 방향 유지")] Authored,
    [InspectorName("플레이어 바라보기")] Player,
    [InspectorName("연결한 대상 바라보기")] Target
}

/// <summary>NPC의 일시적인 행동과 Animator를 제어한다. 등장 조건과 영속 결과는 사건 담당자가 결정한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcMovement2D), typeof(Animator))]
public sealed class NpcActor : MonoBehaviour
{
    [Header("NPC 연결")]
    [SerializeField] private NpcMovement2D movement;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Header("그림 방향")]
    [Tooltip("반전하지 않은 원본 그림이 오른쪽을 바라보면 켭니다.")]
    [SerializeField] private bool artworkFacesRight = true;
    [Header("평소·대화 시선")]
    [SerializeField] private NpcIdleFacing idleFacing;
    [SerializeField] private Transform idleFacingTarget;
    [SerializeField] private bool facePlayerDuringDialogue;
    private Transform scenePlayer;
    private bool conversation;
    private bool keepMotionFacing;
    private Transform conversationFacing;

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private CancellationTokenSource action;
    private PoseSnapshot? pendingPose;
    private bool visible = true;
    private bool appearanceCaptured;
    private float restingAlpha = 1;

    public bool IsBusy => action != null;
    public bool IsVisible => visible;

    /// <summary>연출 시작 자세. 표시 여부와 게임 진행 상태는 포함하지 않는다.</summary>
    public readonly struct PoseSnapshot
    {
        internal readonly NpcActor Owner;
        internal readonly Vector3 Position, RootScale, VisualPosition, VisualScale;
        internal readonly Quaternion VisualRotation;
        internal readonly Color Color;
        internal readonly bool Flip, FacingRight;

        internal PoseSnapshot(NpcActor owner)
        {
            Owner = owner;
            Position = owner.transform.position;
            RootScale = owner.transform.localScale;
            var visual = owner.spriteRenderer.transform;
            VisualPosition = visual.localPosition;
            VisualRotation = visual.localRotation;
            VisualScale = visual.localScale;
            Color = owner.spriteRenderer.color;
            Flip = owner.spriteRenderer.flipX;
            FacingRight = owner.movement.FacingRight;
        }
    }

    internal void Configure(NpcMovement2D actorMovement, Animator actorAnimator, SpriteRenderer renderer)
    {
        if (IsBusy) throw new InvalidOperationException("NPC 행동 중에는 연결을 바꿀 수 없습니다.");
        movement = actorMovement;
        animator = actorAnimator;
        spriteRenderer = renderer;
        appearanceCaptured = false;
        CacheComponents();
        ValidateReferences();
    }

    private void Awake() => CacheComponents();

    private void Reset()
    {
        movement = GetComponent<NpcMovement2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheComponents()
    {
        if (movement == null) movement = GetComponent<NpcMovement2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer != null && !appearanceCaptured)
        {
            restingAlpha = spriteRenderer.color.a;
            appearanceCaptured = true;
        }
    }

    private void ValidateReferences()
    {
        if (movement == null || animator == null || spriteRenderer == null ||
            movement.transform != transform || animator.transform != transform ||
            !spriteRenderer.transform.IsChildOf(transform))
            throw new InvalidOperationException("NPC의 이동체·Animator·하위 SpriteRenderer 연결을 확인하세요: " + name);
    }

    private void Update()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(SpeedParameter, movement != null ? movement.Speed : 0);
        if (!keepMotionFacing && spriteRenderer != null && movement != null && movement.Speed > .0001f)
            spriteRenderer.flipX = artworkFacesRight ? !movement.FacingRight : movement.FacingRight;
    }

    internal void SetConversation(Transform player, bool active)
    {
        scenePlayer = player;
        conversation = active;
        conversationFacing = null;
    }

    internal void FaceDuringConversation(Transform target)
    {
        FaceTowards(target.position);
        if (conversation) conversationFacing = target;
    }

    private void LateUpdate()
    {
        if (!visible || IsBusy || movement == null || movement.IsMoving) return;
        var target = conversation && conversationFacing != null ? conversationFacing :
            conversation && facePlayerDuringDialogue ? scenePlayer : idleFacing switch
        {
            NpcIdleFacing.Player => scenePlayer,
            NpcIdleFacing.Target => idleFacingTarget,
            _ => null
        };
        if (target != null && animator != null && animator.runtimeAnimatorController != null)
            FaceTowards(target.position);
    }

    /// <summary>게임 진행 조건은 호출자가 판단한다. 숨길 때 진행 중인 행동도 취소한다.</summary>
    public void SetVisible(bool value)
    {
        CacheComponents();
        if (!value) CancelAction();
        ApplyVisibility(value);
        if (value && spriteRenderer != null && !IsBusy)
        {
            var color = spriteRenderer.color;
            color.a = restingAlpha;
            spriteRenderer.color = color;
        }
    }

    private void ApplyVisibility(bool value)
    {
        visible = value;
        // 클립을 정지시켜 숨긴 뒤 Animator가 표시 속성을 다시 쓰지 못하게 한다.
        if (animator != null) animator.enabled = value;
        if (spriteRenderer != null) spriteRenderer.enabled = value;
    }

    public PoseSnapshot CapturePose()
    {
        if (IsBusy) throw new InvalidOperationException("행동 중에는 NPC의 시작 자세를 저장할 수 없습니다.");
        CacheComponents();
        ValidateReferences();
        return new PoseSnapshot(this);
    }

    /// <summary>취소 정리가 끝난 뒤 시작 자세로 복구한다. 표시 여부는 호출자가 결정한다.</summary>
    public void RestorePose(PoseSnapshot pose)
    {
        if (pose.Owner != this) throw new ArgumentException("다른 NPC의 자세는 복구할 수 없습니다.", nameof(pose));
        pendingPose = pose;
        CancelAction();
        if (!IsBusy) ApplyPendingPose();
    }

    private void ApplyPendingPose()
    {
        if (!pendingPose.HasValue) return;
        var pose = pendingPose.Value;
        pendingPose = null;
        if (this == null) return;
        transform.position = pose.Position;
        if (spriteRenderer != null)
        {
            RestoreAnimationPose(pose);
            spriteRenderer.color = pose.Color;
            spriteRenderer.flipX = pose.Flip;
        }
        if (movement != null) movement.Face(pose.FacingRight);
    }

    private void RestoreAnimationPose(PoseSnapshot pose)
    {
        if (this != null) transform.localScale = pose.RootScale;
        if (spriteRenderer == null) return;
        var visual = spriteRenderer.transform;
        visual.localPosition = pose.VisualPosition;
        visual.localRotation = pose.VisualRotation;
        visual.localScale = pose.VisualScale;
    }

    /// <summary>숨겨진 배우의 입장 준비도 허용한다. 행동 중인 배우의 좌표는 덮어쓰지 않는다.</summary>
    public void SetPosition(Vector3 position)
    {
        CacheComponents();
        ValidateReferences();
        if (!isActiveAndEnabled || IsBusy || movement.IsMoving)
            throw new InvalidOperationException("비활성 또는 행동 중인 NPC는 순간 배치할 수 없습니다.");
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
            throw new ArgumentOutOfRangeException(nameof(position));
        transform.position = position;
    }

    public void FaceTowards(Vector3 position)
    {
        ValidateActionStart();
        if (!float.IsFinite(position.x)) throw new ArgumentOutOfRangeException(nameof(position));
        float delta = position.x - transform.position.x;
        if (Mathf.Abs(delta) <= .0001f) return;
        movement.Face(delta > 0);
        spriteRenderer.flipX = artworkFacesRight ? !movement.FacingRight : movement.FacingRight;
    }

    /// <summary>비반복 Animator 상태를 지정 시간 동안 한 번 재생하고 복귀 상태로 돌아간다.</summary>
    public async UniTask PlayAnimationAsync(string stateName, string returnStateName, float duration, CancellationToken token)
    {
        ValidateActionStart();
        if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        int state = ResolveState(stateName);
        int returnState = ResolveState(returnStateName);
        if (state == 0 || returnState == 0)
            throw new InvalidOperationException("NPC의 재생·복귀 Animator 상태를 확인하세요: " + stateName + " / " + returnStateName);
        token.ThrowIfCancellationRequested();
        var pose = new PoseSnapshot(this);
        float previousSpeed = animator.speed;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        action = source;
        try
        {
            animator.SetFloat(SpeedParameter, 0);
            animator.Play(state, 0, 0);
            animator.Update(0);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.loop || !float.IsFinite(info.length) || info.length <= 0)
                throw new InvalidOperationException("NPC 단발 연출에는 길이가 있는 비반복 클립을 연결하세요: " + stateName);
            animator.speed = info.length / duration;
            float elapsed = 0;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                elapsed += Time.deltaTime;
            }
            source.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (animator != null)
            {
                animator.speed = previousSpeed;
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Play(returnState, 0, 0);
                    if (animator.isActiveAndEnabled) animator.Update(0);
                }
            }
            RestoreAnimationPose(pose);
            FinishAction(source);
        }
    }

    public UniTask MoveToAsync(Vector3 destination, CancellationToken token)
        => MoveAsync(destination, false, 0, null, token);

    /// <summary>기존 연출의 고정 이동 시간을 유지하며 작성된 이동 속도는 보존한다.</summary>
    public UniTask MoveToAsync(Vector3 destination, float duration, CancellationToken token)
        => MoveAsync(destination, false, 0, duration, token);

    /// <summary>지정 시간과 보간 방식으로 이동한다. 기존 속도 기반 설정은 바꾸지 않는다.</summary>
    public UniTask MoveToAsync(Vector3 destination, float duration, NpcMotionEase ease, CancellationToken token)
        => MoveAsync(destination, false, 0, duration, token, ease);

    public UniTask MoveToAsync(Vector3 destination, float duration, NpcMotionEase ease, bool keepFacing, CancellationToken token)
        => MoveAsync(destination, false, 0, duration, token, ease, 0, keepFacing);

    public UniTask MoveArcToAsync(Vector3 destination, float duration, float height, CancellationToken token)
        => MoveAsync(destination, false, 0, duration, token, NpcMotionEase.Linear, height, true);

    /// <summary>현재 위치에서 페이드한 뒤 숨긴다. 등장 여부는 호출자가 관리한다.</summary>
    public UniTask FadeOutAsync(float duration, CancellationToken token)
        => MoveAsync(transform.position, true, duration, null, token);

    /// <summary>호출자가 지정한 월드 X/Y 이동량과 페이드 시간으로 퇴장한다.</summary>
    public UniTask DepartAsync(Vector2 offset, float fadeDuration, CancellationToken token)
    {
        // 목적지는 한 번만 계산한다. 이동 중인 자신의 위치를 계속 기준으로 삼지 않는다.
        var destination = transform.position + new Vector3(offset.x, offset.y, 0f);
        return MoveAsync(destination, true, fadeDuration, null, token);
    }

    /// <summary>전체 이동량을 일정하게 이동하며, 마지막 페이드 구간에서도 이동을 계속한다.</summary>
    public async UniTask DepartWhileMovingAsync(Vector2 totalOffset, float walkDuration, float fadeDuration,
        NpcMotionEase fadeEase, CancellationToken token)
    {
        ValidateActionStart();
        if (!float.IsFinite(totalOffset.x) || !float.IsFinite(totalOffset.y))
            throw new ArgumentOutOfRangeException(nameof(totalOffset));
        if (!float.IsFinite(walkDuration) || walkDuration < 0) throw new ArgumentOutOfRangeException(nameof(walkDuration));
        if (!float.IsFinite(fadeDuration) || fadeDuration < 0) throw new ArgumentOutOfRangeException(nameof(fadeDuration));
        float duration = walkDuration + fadeDuration;
        if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(walkDuration));
        if (!Enum.IsDefined(typeof(NpcMotionEase), fadeEase)) throw new ArgumentOutOfRangeException(nameof(fadeEase));
        token.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        action = source;
        var start = transform.position;
        var destination = start + new Vector3(totalOffset.x, totalOffset.y);
        var startColor = spriteRenderer.color;
        bool startFlip = spriteRenderer.flipX;
        bool completed = false;
        try
        {
            bool facingRight = destination.x >= start.x;
            spriteRenderer.flipX = artworkFacesRight ? !facingRight : facingRight;
            await movement.MoveToAsync(destination, duration, NpcMotionEase.Linear, elapsed =>
            {
                source.Token.ThrowIfCancellationRequested();
                float progress = fadeDuration > 0 ? (elapsed - walkDuration) / fadeDuration : elapsed >= walkDuration ? 1 : 0;
                var color = startColor;
                color.a *= 1 - NpcMotionCurves.Evaluate(fadeEase, progress);
                spriteRenderer.color = color;
            }, source.Token);
            source.Token.ThrowIfCancellationRequested();
            var hiddenColor = startColor;
            hiddenColor.a = 0;
            spriteRenderer.color = hiddenColor;
            ApplyVisibility(false);
            completed = true;
        }
        finally
        {
            if (!completed && this != null)
            {
                transform.position = start;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = startColor;
                    spriteRenderer.flipX = startFlip;
                }
            }
            FinishAction(source);
        }
    }

    private async UniTask MoveAsync(Vector3 destination, bool depart, float fadeDuration, float? moveDuration,
        CancellationToken token, NpcMotionEase ease = NpcMotionEase.Linear, float arcHeight = 0, bool keepFacing = false)
    {
        ValidateActionStart();
        if (!float.IsFinite(fadeDuration) || fadeDuration < 0) throw new ArgumentOutOfRangeException(nameof(fadeDuration));
        if (moveDuration.HasValue && (!float.IsFinite(moveDuration.Value) || moveDuration.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(moveDuration));
        if (!float.IsFinite(arcHeight) || arcHeight < 0) throw new ArgumentOutOfRangeException(nameof(arcHeight));
        if (!Enum.IsDefined(typeof(NpcMotionEase), ease)) throw new ArgumentOutOfRangeException(nameof(ease));
        token.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        action = source;
        var start = transform.position;
        var startColor = spriteRenderer.color;
        bool startFlip = spriteRenderer.flipX;
        bool completed = false;
        keepMotionFacing = keepFacing;
        try
        {
            if (moveDuration.HasValue)
            {
                bool facingRight = destination.x >= start.x;
                if (!keepFacing) spriteRenderer.flipX = artworkFacesRight ? !facingRight : facingRight;
                if (arcHeight > 0) await movement.MoveArcToAsync(destination, moveDuration.Value, arcHeight, source.Token);
                else await movement.MoveToAsync(destination, moveDuration.Value, ease, source.Token);
            }
            else await movement.MoveToAsync(destination, source.Token);
            if (depart)
            {
                float elapsed = 0;
                while (elapsed < fadeDuration)
                {
                    source.Token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var color = startColor;
                    color.a *= 1 - Mathf.Clamp01(elapsed / fadeDuration);
                    spriteRenderer.color = color;
                    await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                }
                source.Token.ThrowIfCancellationRequested();
                ApplyVisibility(false);
            }
            source.Token.ThrowIfCancellationRequested();
            completed = true;
        }
        finally
        {
            keepMotionFacing = false;
            if (!completed && this != null)
            {
                transform.position = start;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = startColor;
                    spriteRenderer.flipX = startFlip;
                }
            }
            FinishAction(source);
        }
    }

    private void ValidateActionStart()
    {
        if (!isActiveAndEnabled || !visible || IsBusy)
            throw new InvalidOperationException("NPC가 비활성·숨김 상태이거나 이미 행동 중입니다.");
        CacheComponents();
        ValidateReferences();
        if (animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("NPC의 Animator Controller를 연결하세요: " + name);
    }

    private int ResolveState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return 0;
        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(0, hash)) return hash;
        hash = Animator.StringToHash(animator.GetLayerName(0) + "." + stateName);
        return animator.HasState(0, hash) ? hash : 0;
    }

    private void FinishAction(CancellationTokenSource source)
    {
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            animator.SetFloat(SpeedParameter, 0);
        if (!ReferenceEquals(action, source)) return;
        action = null;
        ApplyPendingPose();
    }

    public void CancelAction()
    {
        action?.Cancel();
        if (movement != null) movement.Stop();
    }

    private void OnDisable() => CancelAction();
}
