using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(BoxCollider2D))]
/// <summary>
/// 2D 캐릭터 컨트롤러 물리 이동체. Rigidbody 없이 BoxCast 기반 자체 충돌 처리로 이동을 구현한다
/// (좌우 이동은 입력, 상하는 외부 델타(HandleExternalDelta)로만 발생 — 별도 중력/점프 없음).
/// </summary>
public class PlayerMovement2D : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] private float curSpeed = 6f;

    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 10f;

    [Header("충돌")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float skinWidth = 0.02f;

    [SerializeField] BoxCollider2D _col;


    private float _moveInputX;

    private float _velocityX;
    private float _velocityY;

    private Vector2 _externalDelta;

    private bool _isGrounded;
    [SerializeField] private bool _isFacingRight = true;

    public float VelocityX => _velocityX;
    public float VelocityY => _velocityY;
    public bool  IsGrounded => _isGrounded;
    public bool  IsFacingRight => _isFacingRight;
    public bool  IsMoving => Mathf.Abs(_moveInputX) > 0.01f;


    /// <summary>이동 입력(좌우)을 저장한다. X 성분만 사용.</summary>
    public void GetMoveInput(Vector2 value) => _moveInputX = value.x;
    /// <summary>엘리베이터 등 외부 요인에 의한 위치 이동량을 다음 프레임 이동에 누적 반영한다.</summary>
    public void HandleExternalDelta(Vector2 delta) => _externalDelta += delta;

    /// <summary>달리기 여부에 따라 현재 이동 속도를 걷기/달리기 속도로 전환한다.</summary>
    public void SetSpeed(bool isRun)
    {
        curSpeed = isRun ? runSpeed : walkSpeed;
    }

    /// <summary>매 프레임 호출: 입력 기반 X속도 계산 -> 충돌 처리된 이동 적용 -> 스프라이트 좌우 반전 처리.</summary>
    public void Handle()
    {
        _velocityX = _moveInputX * curSpeed;

        MoveWithCollision();
        HandleFlip();
    }

    /// <summary>
    /// BoxCast로 X/Y 각 축의 이동을 개별 검사해 충돌 시 이동 거리를 벽면까지로 제한하고, 발밑 오버랩으로
    /// 접지 여부(_isGrounded)를 갱신한 뒤 최종 이동량을 Translate로 적용한다.
    /// </summary>
    private void MoveWithCollision()
    {
        Vector2 origin = (Vector2)transform.position + _col.offset;
        Vector2 move = new Vector2(_velocityX, _velocityY) * Time.deltaTime + _externalDelta;
        Vector2 castSize = _col.size - Vector2.one * skinWidth * 2f;

        _externalDelta = Vector2.zero;

        if (move.x != 0f)
        {
            RaycastHit2D hit = Physics2D.BoxCast(origin, castSize, 0f,
                                Vector2.right * Mathf.Sign(move.x),
                                Mathf.Abs(move.x) + skinWidth, groundLayer);
            if (hit)
            {
                move.x = (hit.distance - skinWidth) * Mathf.Sign(move.x);
                _velocityX = 0f;
            }
        }

        origin.x += move.x;

        if (move.y != 0f)
        {
            RaycastHit2D hit = Physics2D.BoxCast(origin, castSize, 0f,
                                Vector2.up * Mathf.Sign(move.y),
                                Mathf.Abs(move.y) + skinWidth, groundLayer);
            if (hit)
            {
                move.y = (hit.distance - skinWidth) * Mathf.Sign(move.y);
                _velocityY = 0f;
            }
        }

        bool wasGrounded = _isGrounded;
        Vector2 footPos = (Vector2)transform.position + _col.offset
                          + Vector2.down * (_col.size.y / 2f);
        Vector2 footSize = new Vector2(_col.size.x * 0.9f, skinWidth * 2f);
        _isGrounded = Physics2D.OverlapBox(footPos, footSize, 0f, groundLayer);

        transform.Translate(move);
    }

    /// <summary>이동 방향이 바뀌면 스프라이트를 반전시킨다.</summary>
    private void HandleFlip()
    {
        if (_moveInputX > 0.01f && !_isFacingRight) Flip();
        else if (_moveInputX < -0.01f && _isFacingRight) Flip();
    }
    /// <summary>
    /// 좌우 방향을 반전시키고 spriteRenderer.flipX로 반영한다.
    /// (인수인계 메모) 아래 return 이후 localScale을 반전시키는 코드는 도달 불가능한 죽은 코드로 남아있음.
    /// </summary>
    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        spriteRenderer.flipX = _isFacingRight;

        return;

        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }



    /// <summary>에디터에서 접지 판정 박스를 시각화한다 (접지 시 초록, 아니면 빨강).</summary>
    private void OnDrawGizmosSelected()
    {
        if (_col == null) _col = GetComponent<BoxCollider2D>();
        Vector2 footPos = (Vector2)transform.position + _col.offset
                          + Vector2.down * (_col.size.y / 2f);
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(footPos, new Vector2(_col.size.x * 0.9f, skinWidth * 2f));
    }
}
