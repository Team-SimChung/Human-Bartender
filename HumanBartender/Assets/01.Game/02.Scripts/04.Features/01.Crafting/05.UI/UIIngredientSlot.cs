using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>재료 선택 패널의 슬롯 하나. 좌/우클릭으로 수량을 증감하며, 선택 수량(count)에 따라 강조 표시를 전환한다.</summary>
public class UIIngredientSlot : MonoBehaviour, IPointerClickHandler,
     IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI countText;
    [SerializeField] Image ingredientImage;

    [SerializeField] Image slotImage;
    [SerializeField] Image slotSelectImage;

    IngredientData data;
    public event Action<IngredientData> OnLeftClick;
    public event Action<IngredientData> OnRightClick;

    int count = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        count = 0;
        countText.text = "0";

        slotImage.enabled = true;
        slotSelectImage.enabled = false ;
    }

    public void SetData(IngredientData d)
    {
        data = d;
    }


    public void SetImage(Sprite sprite)
    {
        ingredientImage.sprite = sprite;
    }
    public void SetNameText(string text)
    {
        nameText.text = text;
    }
    /// <summary>선택 수량을 0으로 되돌리고 UI를 갱신한다.</summary>
    public void ResetCount()
    {
        count = 0;
        SetUI();
    }
    /// <summary>수량 텍스트와 강조 표시(slotImage/slotSelectImage)를 현재 count에 맞춰 갱신한다.</summary>
    public void SetUI()
    {
        countText.text = count.ToString();

        slotImage.enabled = count <= 0;
        slotSelectImage.enabled = count > 0;
    }
    /// <summary>선택 수량을 v만큼 증감시킨다 (0 미만으로는 내려가지 않음).</summary>
    public void IncreaseCount(int v)
    {
        count += v;

        if (count < 0) count = 0;

        SetUI();
    }


    public void OnPointerClick(PointerEventData e)
    {
        switch (e.button)
        {
            case PointerEventData.InputButton.Left:
                OnLeftClick?.Invoke(data);
                break;
            case PointerEventData.InputButton.Right:
                OnRightClick?.Invoke(data);
                break;
        }
    }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        slotImage.enabled = false;
        slotSelectImage.enabled = true;
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        SetUI();
    }
}
