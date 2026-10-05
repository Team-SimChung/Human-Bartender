using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 2부 주문자 앞에 잠깐 생기는 서빙 자리.
///
/// 1부의 CoasterDropZone과 하는 일은 같지만 붙는 대상이 다르다. 그쪽은 씬에 미리 놓인 손님 슬롯에
/// 매여 있고, 이쪽은 대본이 주문을 만든 순간 그 좌석 앞에 생겼다가 잔이 나가면 사라진다 —
/// 2부의 좌석은 씬에 고정된 손님 자리가 아니라 대본이 그때그때 채우는 자리이기 때문이다.
///
/// 받아들일 잔인지는 판단하지 않는다. 여기 놓였다는 것 자체가 주문자에게 냈다는 뜻이다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class StoryServeDropTarget : MonoBehaviour, IDropHandler
{
    static readonly HashSet<StoryServeDropTarget> activeTargets = new();

    Func<CraftedDrink, bool> onServed;
    Image targetGraphic;

    void Awake()
    {
        targetGraphic = GetComponent<Image>();
        targetGraphic.raycastTarget = false;
    }

    void OnEnable()
    {
        // 플레이 중 스크립트 리로드 뒤에는 비직렬화 필드가 비어 있을 수 있으므로 다시 연결한다.
        if (targetGraphic == null) targetGraphic = GetComponent<Image>();
        targetGraphic.raycastTarget = false;
        activeTargets.Add(this);
    }

    void OnDisable()
    {
        activeTargets.Remove(this);
        if (targetGraphic != null) targetGraphic.raycastTarget = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetActiveTargets() => activeTargets.Clear();

    /// <summary>
    /// 완성 잔을 집은 동안에만 활성 서빙 자리가 포인터를 받는다.
    /// 평소에도 켜 두면 잔 위에 겹친 서빙 자리가 OnBeginDrag를 가로챈다.
    /// </summary>
    public static void SetRaycastEnabledWhileDragging(bool enabled)
    {
        foreach (StoryServeDropTarget target in activeTargets)
        {
            if (target != null && target.targetGraphic != null)
                target.targetGraphic.raycastTarget = enabled;
        }
    }

    /// <summary>잔이 놓였을 때 부를 곳을 건다. 한 번 놓이면 스스로 연결을 끊는다.</summary>
    public void Bind(Func<CraftedDrink, bool> handler)
    {
        onServed = handler;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (onServed == null) return;

        GameObject dragged = eventData.pointerDrag;
        if (dragged == null || !dragged.TryGetComponent(out DrinkDragItem item)) return;

        // 주문 담당이 서빙을 확정한 경우에만 소비한다. 실패하면 트레이로 돌아간다.
        // 잔 오브젝트 제거는 기존 OnEndDrag에서 수행한다.
        if (!onServed(item.Drink)) return;
        item.MarkServed();
        onServed = null;
    }
}
