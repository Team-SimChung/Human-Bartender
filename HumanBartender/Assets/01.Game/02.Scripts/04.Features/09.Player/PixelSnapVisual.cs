using UnityEngine;

/// <summary>
/// 부모(실제 이동 오브젝트)의 위치를 픽셀당 유닛(ppu) 격자에 맞춰 스냅시켜, 픽셀아트가 서브픽셀 위치에서
/// 흔들리지 않도록 보정하는 비주얼 자식 오브젝트용 컴포넌트.
/// </summary>
public class PixelSnapVisual : MonoBehaviour
{
    [SerializeField] private float ppu = 100f;

    private void LateUpdate()
    {
        Vector3 worldPos = transform.parent.position;
        Vector3 snapped = new Vector3(
            Mathf.Round(worldPos.x * ppu) / ppu,
            Mathf.Round(worldPos.y * ppu) / ppu,
            worldPos.z
        );
        // 부모 위치와의 차이를 localPosition으로 보정
        transform.localPosition = snapped - worldPos;
    }
}