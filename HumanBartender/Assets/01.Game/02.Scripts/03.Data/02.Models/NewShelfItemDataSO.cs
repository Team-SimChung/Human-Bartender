using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewShelfItemData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("kind")] public ENewShelfKind Kind { get; set; }
    [field: SerializeField][JsonProperty("name")] public LocalizedText Name { get; set; }
    [JsonProperty("category")] public ENewIngredientCategory? Category { get; set; }

    /// <summary>
    /// 이 재료를 기본적으로 어떤 동작으로 다루는지. 재료(kind=ingredient)에만 있고 잔·도구·가니시는 null이다.
    ///
    /// 플레이어가 고른 재료가 선택한 칵테일의 레시피에 없을 때(오선택) 어떤 기믹으로 실행할지를 이 값이
    /// 정한다. 레시피에 있는 재료는 그 줄의 action을 따르므로, 두 값은 서로 어긋나지 않아야 한다.
    /// </summary>
    [JsonProperty("default_action")] public ENewRecipeAction? DefaultAction { get; set; }

    /// <summary>
    /// 이 재료를 진열할 선반. 자동으로 넣어 주는 재료는 선반에 오브젝트가 없어 null이다.
    /// </summary>
    [JsonProperty("shelf_group")] public ENewShelfGroup? ShelfGroup { get; set; }

    /// <summary>
    /// 따르기 전에 먼저 해야 하는 손질. 손질이 필요 없으면 null이다.
    /// 현재 로스터에서는 병맥주 하나만 뚜껑을 딴다.
    /// </summary>
    [JsonProperty("prep_action")] public ENewPrepAction? PrepAction { get; set; }

    /// <summary>
    /// 레시피에 목표가 없을 때 쓰는 기본 목표 수량. 오선택 재료에도 화면에 얼마쯤 넣으면 되는지
    /// 보여줄 수 있게 하려고 재료마다 들고 있다. 정답 목표가 있으면 언제나 레시피 쪽이 이긴다.
    /// </summary>
    [JsonProperty("default_target_qty")] public float? DefaultTargetQty { get; set; }

    [JsonProperty("default_target_unit")] public ENewUnit? DefaultTargetUnit { get; set; }

    /// <summary>액체 색상. "R,G,B" 형태의 문자열이다. TryGetLiquidColor로 읽는다.</summary>
    [field: SerializeField][JsonProperty("color")] public string Color { get; set; }
    [field: SerializeField][JsonProperty("sprite")] public string Sprite { get; set; }

    /// <summary>따를 때 그리는 액체의 불투명도. 값이 없으면 재료마다 정해진 기본값을 부르는 쪽이 정한다.</summary>
    [JsonProperty("liquid_alpha")] public float? LiquidAlpha { get; set; }
    [field: SerializeField][JsonProperty("unlock_day")] public int UnlockDay { get; set; }
    [field: SerializeField][JsonProperty("unlock_when")] public string UnlockWhen { get; set; }
    [JsonProperty("shop_price")] public int? ShopPrice { get; set; }
    [field: SerializeField][JsonProperty("desc")] public LocalizedText Desc { get; set; }

    public bool IsIngredient => Kind == ENewShelfKind.Ingredient;

    /// <summary>따르기 전에 뚜껑을 따야 하는 재료인지.</summary>
    public bool RequiresOpen => PrepAction == ENewPrepAction.Open;

    /// <summary>
    /// 플레이어가 선반에서 직접 고르는 재료인지. 스퀴즈·파우더 재료는 레시피를 보고 시스템이 자동으로
    /// 불러오므로 선반에 오브젝트 자체가 없고, 따라서 가이드 점등과 준비 완료 판정에서도 빠진다.
    /// </summary>
    public bool IsSelectable =>
        IsIngredient && (DefaultAction == ENewRecipeAction.Pour || DefaultAction == ENewRecipeAction.FillUp);

    /// <summary>선택한 칵테일의 레시피를 보고 시스템이 자동으로 불러오는 재료인지(레몬·라임·설탕).</summary>
    public bool IsAutoCalled =>
        IsIngredient && (DefaultAction == ENewRecipeAction.Squeeze || DefaultAction == ENewRecipeAction.Powder);

    /// <summary>
    /// "R,G,B" 문자열을 색으로 바꾼다. 아직 색이 정해지지 않은 재료는 값이 비어 있어 false를 반환한다 —
    /// 그런 재료에 임의의 색을 채워 넣지 않고, 부르는 쪽에서 결정하게 둔다.
    /// </summary>
    public bool TryGetLiquidColor(out Color32 color)
    {
        color = new Color32(255, 255, 255, 255);

        // this.Color는 UnityEngine.Color와 이름이 겹친다. 지역 변수로 받아 헷갈릴 여지를 없앤다.
        string raw = this.Color;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string[] parts = raw.Split(',');
        if (parts.Length != 3) return false;

        if (!byte.TryParse(parts[0].Trim(), out byte r)) return false;
        if (!byte.TryParse(parts[1].Trim(), out byte g)) return false;
        if (!byte.TryParse(parts[2].Trim(), out byte b)) return false;

        color = new Color32(r, g, b, 255);
        return true;
    }
}

/// <summary>StreamingAssets/json/shelf_items.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewShelfItemDataSO", menuName = "Data/New/ShelfItemDataSO")]
public class NewShelfItemDataSO : ScriptableObject
{
    public NewShelfItemData[] shelfItemData;

    /// <summary>id로 선반 아이템을 찾는다. 없으면 found=false로 반환한다.</summary>
    public bool TryGet(string id, out NewShelfItemData item)
    {
        item = default;
        if (string.IsNullOrEmpty(id) || shelfItemData == null) return false;

        foreach (var candidate in shelfItemData)
        {
            if (candidate.Id != id) continue;

            item = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 재료의 기본 동작. 재료가 아니거나 데이터에 없으면 null이고, 그런 재료는 기믹을 만들 수 없다.
    /// </summary>
    public ENewRecipeAction? GetDefaultAction(string ingredientId)
    {
        return TryGet(ingredientId, out var item) ? item.DefaultAction : null;
    }

    /// <summary>따르기 전에 뚜껑을 따야 하는 재료인지.</summary>
    public bool RequiresOpen(string ingredientId)
    {
        return TryGet(ingredientId, out var item) && item.RequiresOpen;
    }

    /// <summary>재료에 지정된 기본 목표 수량. 레시피에 목표가 없을 때만 쓴다.</summary>
    public (float? qty, ENewUnit? unit) GetDefaultTarget(string ingredientId)
    {
        return TryGet(ingredientId, out var item)
            ? (item.DefaultTargetQty, item.DefaultTargetUnit)
            : (null, null);
    }
}
