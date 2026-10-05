using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(100)] // ScrollRect 이후에 실행
/// <summary>
/// Scrollbar 핸들 크기를 고정 비율로 강제하는 컴포넌트.
/// ScrollRect 계산 이후(DefaultExecutionOrder 100) LateUpdate에서 sb.size를 덮어쓴다.
/// </summary>
public class FixedHandleSize : MonoBehaviour
{
    [Range(0.01f, 1f)] public float handleSize = 0.1f;

    Scrollbar sb;
    void Awake() => sb = GetComponent<Scrollbar>();
    void LateUpdate()
    {
        if (!Mathf.Approximately(sb.size, handleSize))
            sb.size = handleSize;
    }
}