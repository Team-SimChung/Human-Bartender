using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 하단 트레이에 놓인 완성 잔 하나. 코스터와 같은 방식으로 uGUI 화면 좌표를 따라 끌리며,
/// 손님 앞 드롭존(CoasterDropZone) 위에 놓여 서빙이 받아들여지면 잔은 손님에게 넘어가 사라지고,
/// 그 외의 곳에 놓으면 트레이의 원래 자리로 되돌아간다.
///
/// 코스터와 달리 놓인 자리에 남지 않는다. 코스터는 손님 앞에 깔아 두는 물건이지만 잔은 손님이
/// 받아 가는 물건이라, 서빙이 성사된 잔은 트레이에서도 화면에서도 없어져야 한다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class DrinkDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform rect;
    CanvasGroup canvasGroup;
    Canvas rootCanvas; // 드래그 중 다른 UI 위로 그려지도록 옮겨갈 최상위 Canvas

    Transform trayParent;
    int trayIndex;
    bool served;

    /// <summary>이 아이콘이 들고 있는 잔.</summary>
    public CraftedDrink Drink { get; private set; }

    /// <summary>서빙이 받아들여져 잔이 트레이를 떠날 때 발생한다. 트레이가 목록에서 지우는 데 쓴다.</summary>
    public event Action<DrinkDragItem> Served;

    void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>트레이가 아이콘을 만든 직후 한 번 호출한다.</summary>
    public void Initialize(CraftedDrink drink, Canvas rootCanvas)
    {
        Drink = drink;
        this.rootCanvas = rootCanvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        served = false;

        // 트레이는 레이아웃 그룹이 자리를 잡아 주므로 좌표가 아니라 부모와 순번을 기억한다.
        trayParent = rect.parent;
        trayIndex = rect.GetSiblingIndex();

        rect.SetParent(rootCanvas.transform, true);
        rect.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false; // 드롭존이 자기 자신에게 가려지지 않도록
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(false);
        canvasGroup.blocksRaycasts = true;

        if (served)
        {
            Destroy(gameObject);
            return;
        }

        rect.SetParent(trayParent, false);
        rect.SetSiblingIndex(trayIndex);
    }

    /// <summary>
    /// 손님이 잔을 받아들였을 때 드롭존에서 호출한다.
    ///
    /// 나갔다는 사실은 여기서 바로 알린다. OnEndDrag까지 미루면, 잔을 놓는 순간 트레이가 놓인 화면이
    /// 꺼지는 경우(2부 서빙) 그 이벤트가 오지 않아 트레이는 낸 잔을 아직 들고 있는 것으로 셈하고,
    /// "아직 내지 않은 잔"이 다음 제조를 영영 막는다.
    ///
    /// 오브젝트를 치우는 것만 OnEndDrag에 남긴다 — 드래그가 끝나기 전에 지우면 그 뒤 이벤트가
    /// 사라진 대상에게 간다.
    /// </summary>
    public void MarkServed()
    {
        if (served) return;

        served = true;
        Served?.Invoke(this);
    }
}
