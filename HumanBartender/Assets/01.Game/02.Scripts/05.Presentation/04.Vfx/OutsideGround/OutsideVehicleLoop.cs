using UnityEngine;

[DisallowMultipleComponent]
public sealed class OutsideVehicleLoop : MonoBehaviour
{
    [Header("월드 X 이동")]
    [SerializeField, Tooltip("초당 이동 거리입니다. 양수는 오른쪽, 음수는 왼쪽, 0은 정지입니다.")]
    private float xSpeed = 1f;
    [SerializeField, Tooltip("왼쪽 경계의 월드 X 좌표입니다.")]
    private float leftX = -24f;
    [SerializeField, Tooltip("오른쪽 경계의 월드 X 좌표입니다. 왼쪽 경계보다 커야 합니다.")]
    private float rightX = 10f;

    private void Update()
    {
        float displacement = xSpeed * Time.deltaTime;
        if (displacement == 0f || leftX >= rightX) return;

        Vector3 position = transform.position;
        position.x += displacement;
        float width = rightX - leftX;
        if (displacement > 0f && position.x >= rightX)
            position.x = leftX + Mathf.Repeat(position.x - rightX, width);
        else if (displacement < 0f && position.x <= leftX)
            position.x = rightX - Mathf.Repeat(leftX - position.x, width);

        transform.position = position;
    }
}
