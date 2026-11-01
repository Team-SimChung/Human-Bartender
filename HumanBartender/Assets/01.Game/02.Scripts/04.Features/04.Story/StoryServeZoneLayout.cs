using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 앞 서빙 자리의 그림 배치. 코스터·완성 잔·빈 잔의 위치와 크기는 프리팹의 자식 RectTransform이 소유하고,
/// 이 컴포넌트는 어떤 그림을 켤지만 정한다. 자리의 피벗은 책상 표면 선(코스터 바닥)에 맞춘다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryServeZoneLayout : MonoBehaviour
{
    [Tooltip("손님 앞에 놓인 코스터. 바닥이 책상 표면 선에 닿게 둔다.")]
    [SerializeField] private Image coaster;

    [Tooltip("완성 잔. 피벗을 아래 가운데로 두고 코스터 위에 올라앉게 둔다.")]
    [SerializeField] private Image drink;

    [Tooltip("다 마신 빈 잔. 완성 잔보다 작은 빈 잔 그림에 맞춘 자리다.")]
    [SerializeField] private Image emptyGlass;

    private void Awake()
    {
        Require(coaster, nameof(coaster));
        Require(drink, nameof(drink));
        Require(emptyGlass, nameof(emptyGlass));
        foreach (var image in new[] { coaster, drink, emptyGlass })
        {
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
        }
    }

    /// <summary>코스터를 놓은 그림으로 바꾼다. 쟁반의 코스터와 같은 그림을 쓴다.</summary>
    public void ShowCoaster(Sprite sprite, Color color)
    {
        coaster.sprite = sprite;
        coaster.color = color;
        coaster.gameObject.SetActive(true);
    }

    /// <summary>
    /// 잔을 표시한다. 빈 잔이면 빈 잔 자리를, 아니면 완성 잔 자리를 켜고 다른 쪽은 끈다.
    /// 그림이 없으면 칵테일 색으로 자리만 표시한다.
    /// </summary>
    public void ShowGlass(Sprite sprite, Color fallbackColor, bool empty)
    {
        var shown = empty ? emptyGlass : drink;
        var hidden = empty ? drink : emptyGlass;
        hidden.gameObject.SetActive(false);
        shown.sprite = sprite;
        shown.color = sprite != null || empty ? Color.white : fallbackColor;
        shown.gameObject.SetActive(true);
    }

    private void Require(Image image, string field)
    {
        if (image == null) throw new InvalidOperationException($"[StoryServeZoneLayout] {field} 이미지를 연결하세요: {name}");
    }

    private void OnValidate()
    {
        if (coaster == null || drink == null || emptyGlass == null)
            Debug.LogWarning("[StoryServeZoneLayout] Coaster·Drink·Empty Glass 이미지를 모두 연결하세요.", this);
    }
}
