using UnityEngine;

/// <summary>주변 대화의 청취 범위만 정한다. 시작 범위, 입력, 플레이어 상태와 씬 이동은 다루지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class WorldAmbientDialogueDistanceGate : MonoBehaviour
{
    [Header("주변 대화 청취 범위")]
    [SerializeField, Min(0f), Tooltip("현재 화자의 SpeechAnchor와 플레이어 사이 가로 거리입니다. 멀어지면 말풍선·타이핑·읽기 대기를 멈추고 돌아오면 이어집니다.")]
    private float horizontalDistance = 4f;
    [SerializeField, Min(0f), Tooltip("현재 화자의 SpeechAnchor와 플레이어 사이 세로 거리입니다. 다른 층에서 대사가 지나가지 않게 합니다.")]
    private float verticalDistance = 1.6f;
    [SerializeField, Min(0f), Tooltip("듣는 중 범위를 벗어날 때만 더하는 여유입니다. 정지 후에는 기본 범위 안으로 돌아와야 재개해 경계에서 깜박이지 않습니다.")]
    private float exitMargin = .2f;

    internal bool Contains(Vector3 listener, Vector3 speaker, bool wasInRange)
    {
        float margin = wasInRange ? exitMargin : 0f;
        return Mathf.Abs(listener.x - speaker.x) <= horizontalDistance + margin &&
            Mathf.Abs(listener.y - speaker.y) <= verticalDistance + margin;
    }
}
