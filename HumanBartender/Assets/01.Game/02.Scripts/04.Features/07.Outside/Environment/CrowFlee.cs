using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CrowFlee : MonoBehaviour
{
    [Header("연결할 대상")]
    public Transform player;
    public Camera gameCamera;

    [Header("접근 감지")]
    [Min(0.01f)]
    public float detectionRadius = 2f;

    [Header("비행 조절")]
    [Min(0f)]
    public float startHorizontalSpeed = 1f;

    [Min(0f)]
    public float finalHorizontalSpeed = 5f;

    [Min(0f)]
    public float startUpwardSpeed = 4f;

    [Min(0.1f)]
    public float finalUpwardSpeed = 1.5f;

    [Min(0.01f)]
    public float transitionDuration = 0.6f;

    [Header("나중에 넣을 새 그림의 기본 방향")]
    public bool spriteFacesRight = true;

    private SpriteRenderer spriteRenderer;
    private readonly Plane[] cameraPlanes = new Plane[6];

    private bool isFlying;
    private float flightTime;
    private float horizontalDirection;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 카메라를 직접 연결하지 않았다면 MainCamera를 찾아봅니다.
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (player == null || gameCamera == null)
        {
            Debug.LogError(
                "CrowFlee: Player와 Game Camera를 연결해주세요.",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (player == null || gameCamera == null)
            return;

        if (!isFlying)
        {
            // Z축을 제외한 2D 거리로 접근을 감지합니다.
            float distance = Vector2.Distance(
                transform.position,
                player.position
            );

            if (distance <= detectionRadius)
                BeginFlight();

            return;
        }

        flightTime += Time.deltaTime;

        // 이륙 속도에서 일반 비행 속도로 부드럽게 바뀝니다.
        float progress = Mathf.Clamp01(
            flightTime / Mathf.Max(0.01f, transitionDuration)
        );

        float blend = Mathf.SmoothStep(0f, 1f, progress);

        float horizontalSpeed = Mathf.Lerp(
            startHorizontalSpeed,
            finalHorizontalSpeed,
            blend
        );

        float upwardSpeed = Mathf.Lerp(
            startUpwardSpeed,
            finalUpwardSpeed,
            blend
        );

        Vector3 velocity = new Vector3(
            horizontalDirection * horizontalSpeed,
            upwardSpeed,
            0f
        );

        transform.position += velocity * Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (!isFlying || gameCamera == null)
            return;

        // 이륙 직후에는 화면 밖 판정을 잠깐 미룹니다.
        if (flightTime < 0.2f)
            return;

        GeometryUtility.CalculateFrustumPlanes(
            gameCamera,
            cameraPlanes
        );

        // 그림 전체가 카메라 범위 밖으로 나가면 비활성화합니다.
        if (!GeometryUtility.TestPlanesAABB(
            cameraPlanes,
            spriteRenderer.bounds
        ))
        {
            gameObject.SetActive(false);
        }
    }

    private void BeginFlight()
    {
        isFlying = true;
        flightTime = 0f;

        // 플레이어가 왼쪽이면 오른쪽으로,
        // 플레이어가 오른쪽이면 왼쪽으로 날아갑니다.
        horizontalDirection =
            transform.position.x >= player.position.x ? 1f : -1f;

        spriteRenderer.flipX = spriteFacesRight
            ? horizontalDirection < 0f
            : horizontalDirection > 0f;
    }

    private void OnDrawGizmosSelected()
    {
        // 새를 선택했을 때 접근 감지 범위를 표시합니다.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );
    }
}