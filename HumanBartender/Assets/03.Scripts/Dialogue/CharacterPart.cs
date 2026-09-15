using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;


/// <summary>
/// 항상 재생 상태를 유지하는 기본 정책. 현재는 아무 동작도 하지 않는 빈 구현체.
/// </summary>
public class AlwaysPlayback : IPlaybackPolicy
{
    public void OnDialogueEnd(AnimationPart animPart)
    {
        
    }

    public void OnDialogueStart(AnimationPart animPart)
    {
        
    }

    public void OnPlay(AnimationPart animPart)
    {
        
    }
}


/// <summary>
/// 캐릭터 파츠(얼굴, 몸통 등) 하나의 애니메이터/스프라이트 렌더러를 감싸는 추상 베이스 클래스.
/// AnimatorOverrideController를 이용해 파츠별로 클립을 동적으로 교체하는 방식으로 동작한다.
/// </summary>
[Serializable]
public abstract class AnimationPart
{
    [SerializeField] public EAnimationPart partName;
    [SerializeField] public string partCurAnim;
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    [SerializeField] protected RuntimeAnimatorController baseController;

    protected const float TARGET_LENGTH = 1f;
    protected const string CLIP_SPEED = "ClipSpeed";

    protected float curAnimSpeed = 0.3f;

    protected AnimatorOverrideController _overrideController;
    
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrides
       = new List<KeyValuePair<AnimationClip, AnimationClip>>();

    /// <summary>
    /// baseController를 기반으로 AnimatorOverrideController를 생성해 연결한다. 최초 1회 호출 필요.
    /// </summary>
    public void Initialize()
    {
        _overrideController = new AnimatorOverrideController(baseController);
        animator.runtimeAnimatorController = _overrideController;
        animator.enabled = false;
    }

    public void SetSpeed(float s) => animator.speed = s;

    /// <summary>
    /// 클립 길이(f)를 기준 길이(TARGET_LENGTH)로 나눠 재생 속도를 계산하고 애니메이터 파라미터에 반영한다.
    /// </summary>
    public void SetClipSpeed(float f)
    {
        float calculatedSpeed = f / TARGET_LENGTH;
        curAnimSpeed = calculatedSpeed;
        animator.SetFloat(CLIP_SPEED, calculatedSpeed);
    }

    /// <summary>파츠를 비활성화하고 렌더러/오버라이드 클립을 모두 비운다.</summary>
    public void SetInactive()
    {
        if (animator != null)
            animator.enabled = false;

        if (spriteRenderer != null)
            spriteRenderer.sprite = null;

        if (_overrideController != null)
            ClearAllClips();
    }
    public void SetActive() { animator.enabled = true; spriteRenderer.sprite = null; }

    /// <summary>비동기 로드 핸들 결과를 슬롯(Intro/Loop/Dialogue)에 오버라이드 클립으로 등록한다.</summary>
    public virtual void SetClip(string slot, AsyncOperationHandle<AnimationClip>? handle)
    {
        _overrideController[slot] = handle.Value.Result;
    }
    public virtual void SetClip(string slot, AnimationClip handle) => _overrideController[slot] = handle;

    /// <summary>지정된 슬롯 이름(animName)의 애니메이션을 재생한다. 파츠 종류별 재생 로직은 하위 클래스에서 구현.</summary>
    public abstract UniTask PlayAnimation(string animName, CancellationToken token);
    /// <summary>애니메이터를 끄고 정적 스프라이트를 적용한다.</summary>
    public abstract void ApplySprite(Sprite? sprite);

    /// <summary>오버라이드 컨트롤러에 등록된 모든 클립 오버라이드를 null로 초기화한다.</summary>
    public void ClearAllClips()
    {
        _overrides.Clear();
        _overrideController.GetOverrides(_overrides);

        for (int i = 0; i < _overrides.Count; i++)
        {
            _overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(_overrides[i].Key, null);
        }

        _overrideController.ApplyOverrides(_overrides);
    }
    /// <summary>오버라이드 컨트롤러를 파괴하여 리소스를 정리한다.</summary>
    public virtual void Release()
    {
        if (_overrideController != null)
        {
            UnityEngine.Object.Destroy(_overrideController);
            _overrideController = null;
        }
    }
}


/// <summary>
/// 실제 대사 씬에서 사용되는 캐릭터 파츠 구현체.
/// EAnimLoopMode(Always/Once/Special_OnDialogue 등)에 따라 재생 후 정지하거나 계속 반복하는 방식이 달라진다.
/// </summary>
[Serializable]
public class CharacterPart : AnimationPart, IFade
{
     [SerializeField] EAnimLoopMode _currentLoopMode;

    /// <summary>루프 모드를 변경하고 기존 스프라이트를 초기화한다.</summary>
    public void SetLoopMode(EAnimLoopMode loopMode)
    {
        spriteRenderer.sprite = null;
        _currentLoopMode = loopMode;
    }

    /// <summary>
    /// 현재 루프 모드에 따라 애니메이션을 재생한다.
    /// Always 계열은 반복 재생, Once는 마지막 프레임에서 정지, Special_OnDialogue는 재생 후 대기한다.
    /// </summary>
    public override async UniTask PlayAnimation(string animName, CancellationToken token)
    {
        var Clip = _overrideController[animName];

        //TargetDuration을 State 별로 두기
        SetClipSpeed(Clip.length);

        animator.enabled = true;
        switch (_currentLoopMode)
        {
            case EAnimLoopMode.Always_OnDialogue:
            case EAnimLoopMode.Always:
                animator.speed = 1f;
                animator.Play(animName, 0, 0f);
                break;

            case EAnimLoopMode.Special_OnDialogue:
                animator.speed = 1f;
                animator.Play(animName, 0, 0f);
                await WaitAndDelayAsync(animName, token);
                break;

            case EAnimLoopMode.Once:
                animator.speed = 1f;
                animator.Play(animName, 0, 0f);
                await WaitAndFreezeAsync(animName, token);
                break;
        }

        return;
    }


    /// <summary>애니메이션이 마지막 프레임(normalizedTime >= 0.99)에 도달할 때까지 기다린 뒤 정지시킨다. (Once 모드용)</summary>
    private async UniTask WaitAndFreezeAsync(string animName, CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        
        await UniTask.WaitUntil(() =>
        {
            if (animator == null) return true;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(animName) && stateInfo.normalizedTime >= 0.99f;
        }, cancellationToken: token);

        if (animator != null) animator.speed = 0f;
    }

    /// <summary>애니메이션 재생이 끝날 때까지 기다린 뒤 "Loop" 상태로 전환한다. (Special_OnDialogue 모드용)</summary>
    private async UniTask WaitAndDelayAsync(string animName, CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        await UniTask.WaitUntil(() =>
        {
            if (animator == null) return true;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(animName) && stateInfo.normalizedTime >= 0.99f;
        }, cancellationToken: token);

        animator.Play("Loop", 0, 0f);
    }

    public override void ApplySprite(Sprite sprite)
    {
        animator.enabled = false;
        spriteRenderer.sprite = sprite;
    }

    /// <summary>알파값 0에서 1(불투명)로 서서히 전환한다.</summary>
    public async UniTask FadeIn(CancellationToken token)
    {
        spriteRenderer.color = new Color(0, 0, 0, 1);
        await spriteRenderer.DOColor(Color.white, 1f).ToUniTask();
    }
    /// <summary>알파값 1에서 0(투명)으로 서서히 전환한다.</summary>
    public async UniTask FadeOut(CancellationToken token)
    {
        spriteRenderer.color = new Color(1, 1, 1, 1);
        await spriteRenderer.DOColor(new Color(0, 0, 0, 0), 1f).ToUniTask();
    }



    //지금 보니 이걸 여기서 체크하는건 모순인데

    /// <summary>
    /// 대사 시작 시 호출. Once 모드는 무시하고, Lower_Face/Etc가 아닌 파츠는 Loop 프레임에서 정지시켜
    /// 입 모양 애니메이션(Etc/Lower_Face)만 별도로 재생되도록 한다.
    /// </summary>
    public void OnDialogueStart()
    {
        if (_currentLoopMode == EAnimLoopMode.Once) return;

        if (partName != EAnimationPart.Lower_Face)
        {
            if (partName != EAnimationPart.Etc)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                animator.Play("Loop", 0, 0f);
                animator.Update(0f);
                animator.speed = 0;
            }

            return;
        }

        animator.Play("Dialogue", 0, 0f);

    }

    /// <summary>대사 종료 시 호출. Once 모드는 무시하고, 나머지는 Loop 애니메이션 재생을 재개한다.</summary>
    public void OnDialogueEnd()
    {
        if (_currentLoopMode == EAnimLoopMode.Once) return;

        animator.speed = 1;
        animator.Play("Loop", 0, 0f);
    }

    public void OnAnimation()
    {
        animator.enabled = true;
        animator.speed = 1f;
    }

    /// <summary>파괴 시 오버라이드 컨트롤러 정리.</summary>
    public override void Release()//파괴 시
    {
        if (_overrideController != null)
        {
            UnityEngine.Object.Destroy(_overrideController);
            _overrideController = null;
        }
    }
}
