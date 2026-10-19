using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum DrinkRemovalReason { Served, Discarded }

/// <summary>완성 잔의 드래그와 소비 상태. UI는 반환하고 제조 데이터는 재사용하지 않는다.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class DrinkDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image secondBodyImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private CocktailRecipeVisualCatalog visuals;
    [SerializeField] private Image finishedImage;
    [Tooltip("완성 그림을 사용할 때 감출 임시 잔 도형.")]
    [SerializeField] private GameObject[] placeholderGraphics;
    [Tooltip("집어 든 칵테일 이미지만 표시할 크기. 제공 카드와 별도로 조절한다.")]
    [SerializeField] private Vector2 heldImageSize = new Vector2(67.5f, 105f);
    [SerializeField, Min(1f)] private float maximumCardWidth = 255f;

    RectTransform rect;
    CanvasGroup canvasGroup;
    Canvas rootCanvas;
    Image heldImage;
    bool consumed;
    bool dragging;
    bool released;
    Action<DrinkDragItem> release;

    public CraftedDrink Drink { get; private set; }
    public event Action<DrinkDragItem> Served;
    public event Action<DrinkDragItem, DrinkRemovalReason> Removed;

    void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(CraftedDrink drink, Canvas canvas, Action<DrinkDragItem> returnToPool = null)
    {
        if (rect == null) rect = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        Drink = drink;
        rootCanvas = canvas;
        release = returnToPool;
        consumed = dragging = released = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;

        Sprite sprite = visuals != null ? visuals.GetTableCocktailSprite(drink.CocktailId) : null;
        bool hasFinishedSprite = finishedImage != null && sprite != null;
        if (finishedImage != null)
        {
            finishedImage.sprite = sprite;
            finishedImage.color = Color.white;
            finishedImage.preserveAspect = true;
            finishedImage.gameObject.SetActive(hasFinishedSprite);
        }
        if (placeholderGraphics != null)
            foreach (GameObject graphic in placeholderGraphics)
                if (graphic != null) graphic.SetActive(!hasFinishedSprite);

        if (bodyImage != null) bodyImage.color = drink.Color;
        if (secondBodyImage != null)
        {
            secondBodyImage.color = drink.Color2 ?? Color.clear;
            secondBodyImage.gameObject.SetActive(!hasFinishedSprite && drink.Color2.HasValue);
        }
        if (nameText != null)
        {
            nameText.text = drink.DisplayName;
            var layout = GetComponent<LayoutElement>();
            if (layout != null && layout.minWidth > 0f)
            {
                float horizontalPadding = rect.rect.width - nameText.rectTransform.rect.width;
                float preferredWidth = nameText.GetPreferredValues(drink.DisplayName,
                    Mathf.Infinity, Mathf.Infinity).x + Mathf.Max(0f, horizontalPadding);
                float width = Mathf.Clamp(preferredWidth, layout.minWidth,
                    Mathf.Max(layout.minWidth, maximumCardWidth));
                layout.preferredWidth = width;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Drink == null || consumed || released || dragging || rootCanvas == null) return;
        dragging = true;
        var heldObject = new GameObject("Held Cocktail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        heldObject.transform.SetParent(rootCanvas.transform, false);
        heldObject.transform.SetAsLastSibling();
        heldImage = heldObject.GetComponent<Image>();
        bool hasFinishedSprite = finishedImage != null && finishedImage.sprite != null;
        Image source = hasFinishedSprite ? finishedImage : bodyImage;
        heldImage.sprite = source != null ? source.sprite : null;
        heldImage.color = hasFinishedSprite ? Color.white : Drink.Color;
        heldImage.preserveAspect = true;
        heldImage.raycastTarget = false;
        heldImage.rectTransform.sizeDelta = heldImageSize;
        heldImage.rectTransform.position = source != null ? source.rectTransform.position : rect.position;
        MoveHeldImage(eventData);

        // 카드는 레이아웃 안에서 감추고, 칵테일 이미지만 포인터를 따라가게 한다.
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging && !consumed) MoveHeldImage(eventData);
    }

    void MoveHeldImage(PointerEventData eventData)
    {
        if (heldImage == null || eventData == null) return;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rootCanvas.transform, eventData.position, eventCamera, out Vector3 worldPoint))
            heldImage.rectTransform.position = worldPoint;
    }

    public void OnEndDrag(PointerEventData eventData) => EndDrag();

    void OnDisable()
    {
        if (dragging) EndDrag();
    }

    void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        ClearHeldImage();
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(false);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        if (consumed) Release();
    }

    void ClearHeldImage()
    {
        if (heldImage == null) return;
        GameObject heldObject = heldImage.gameObject;
        heldImage = null;
        heldObject.SetActive(false);
        if (Application.isPlaying) Destroy(heldObject);
        else DestroyImmediate(heldObject);
    }

    void OnDestroy() => ClearHeldImage();

    public void MarkServed() => Remove(DrinkRemovalReason.Served);
    public void MarkDiscarded() => Remove(DrinkRemovalReason.Discarded);

    void Remove(DrinkRemovalReason reason)
    {
        if (Drink == null || consumed || released) return;
        consumed = true;
        if (reason == DrinkRemovalReason.Discarded) Drink.Session.Discard();
        try
        {
            Removed?.Invoke(this, reason);
            if (reason == DrinkRemovalReason.Served) Served?.Invoke(this);
        }
        finally
        {
            if (!dragging) Release();
        }
    }

    void Release()
    {
        if (released) return;
        released = true;
        var returnToPool = release;
        release = null;
        Drink = null;
        Served = null;
        Removed = null;
        if (returnToPool != null) returnToPool(this);
        else Destroy(gameObject);
    }
}
