using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

/// <summary>
/// 플레이어 애니메이션 전담 컴포넌트
/// PlayerMovement2D의 상태를 '읽기만' 해서 Animator 파라미터를 갱신
/// 같은 GameObject에 부착 권장
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMovement2D))]
public class PlayerAnimator2D : MonoBehaviour
{
    [SerializeField] Animator _anim;


    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    
    /*
    private static readonly int HashVelocityY = Animator.StringToHash("VelocityY");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");
    private static readonly int HashJump      = Animator.StringToHash("Jump");
    private static readonly int HashLand      = Animator.StringToHash("Land");
    

    private void OnEnable()
    {
        // 단발성 이벤트 구독
        _movement.OnJumped += HandleJumped;
        _movement.OnLanded += HandleLanded;
    }

    private void OnDisable()
    {
        _movement.OnJumped -= HandleJumped;
        _movement.OnLanded -= HandleLanded;
    }

    private void HandleJumped() => _anim.SetTrigger(HashJump);
    private void HandleLanded() => _anim.SetTrigger(HashLand);
    
    */
    
    /// <summary>이동 속도(value)의 절댓값을 Speed 파라미터에 반영해 걷기/달리기 애니메이션 블렌드를 갱신한다.</summary>
    public void Handle(float value)
    {
        _anim.SetFloat(HashSpeed, Mathf.Abs(value));
    }

    /// <summary>Speed를 0으로 만들어 정지 애니메이션으로 되돌린다.</summary>
    public void Stop()
    {
        _anim.SetFloat(HashSpeed, 0);
    }
}
