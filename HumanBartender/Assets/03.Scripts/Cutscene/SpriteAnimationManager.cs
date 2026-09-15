using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;




[Serializable]
/// <summary>
/// AnimationPart를 상속받아 스프라이트 애니메이션 클립을 Animator로 재생하는 컴포넌트.
/// ApplySprite()로 단일 스프라이트 표시, PlayAnimation()으로 애니메이션 클립을 재생한다.
/// </summary>
public class SpriteAnimationManager : AnimationPart
{
    public override void ApplySprite(Sprite sprite)
    {
        animator.enabled = false;
        spriteRenderer.sprite = sprite;
    }

    public void EnsureInitialized()
    {
        if (animator == null || baseController == null) throw new InvalidOperationException("Sprite cutscene animator/controller is missing.");
        if (_overrideController == null) Initialize();
    }

    public override async UniTask PlayAnimation(string animName, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (animator == null || !animator.HasState(0, Animator.StringToHash(animName)))
            throw new InvalidOperationException("Sprite cutscene animator state is missing: " + animName);
        if (Camera.main != null)
        {
            Vector3 position = Camera.main.transform.position;
            position.z = 0;
            animator.transform.position = position;
        }
        animator.enabled = true;
        animator.speed = 1f;
        animator.Play(animName, 0, 0f);
        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (animator == null || !animator.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Sprite cutscene animator became unavailable.");
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName(animName) && state.normalizedTime >= 1f) break;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        finally { if (animator != null) animator.enabled = false; }
    }
    public void ActiveSelf(bool active)
    {
        if (animator != null) animator.gameObject.SetActive(active);
    }
}
