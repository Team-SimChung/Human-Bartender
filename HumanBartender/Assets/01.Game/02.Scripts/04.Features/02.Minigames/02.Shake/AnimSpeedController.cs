using Cysharp.Threading.Tasks;
using Spine;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 쉐이킹 미니게임 캐릭터 Animator의 재생 속도를 제어하는 컴포넌트.
/// SetSpeed()로 클립 길이 기반 배속을 설정하고, RatioAnimSpeed()로 일시적으로 2배 → 원래 속도로 복귀한다.
/// </summary>
public class AnimSpeedController : MonoBehaviour
{
    [SerializeField] Animator anim;
    [SerializeField] AnimationClip clip;
    [SerializeField] float curAnimSpeed = 1f;
    [SerializeField] float targetDuration = 1f;

    const string PLAYCLIP = "Play";
    const string CLIP_SPEED = "ClipSpeed";

    
    void Start()
    {
        Logger.Log(clip.length);
        
    }

    public void Init()
    {
        SetSpeed(clip.length);
        //anim.Play(PLAYCLIP, 0, 0);
    }

    int animNum = 0;
    public void PlayAnim()
    {
        anim.Play(PLAYCLIP+animNum, 0, 0);
        animNum++;
        animNum %= 2;
    }

    public async UniTaskVoid RatioAnimSpeed()
    {
        curAnimSpeed = curAnimSpeed * 2f;
        anim.SetFloat(CLIP_SPEED, curAnimSpeed);

        await UniTask.WaitForSeconds(targetDuration / 2f);

        curAnimSpeed = curAnimSpeed / 2f;
        anim.SetFloat(CLIP_SPEED, curAnimSpeed);
    }

    public void SetSpeed(float f)
    {
        float calculatedSpeed = f / targetDuration;
        curAnimSpeed = calculatedSpeed;
        anim.SetFloat(CLIP_SPEED, calculatedSpeed);
    }
}
