using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 도구 id 상수와 도구↔믹스 기믹 대응.
///
/// 칵테일 데이터에는 정답 도구가 따로 없고 제조법(mix)만 있다. 둘은 1:1로 대응하므로 여기서 서로
/// 변환한다. 실제로 고른 도구로 실행할 믹스를 구할 때도, 정답 제조법으로 정답 도구를 구할 때도
/// 이 한 곳을 거치기 때문에 양쪽이 어긋날 수 없다.
/// </summary>
public static class NewToolIds
{
    public const string Shaker = "shaker";
    public const string MixingGlass = "mixing_glass";
    public const string BottleOpener = "bottle_opener";

    /// <summary>
    /// 도구 하나가 어떤 믹스 기믹을 부르는지.
    ///
    /// 도구를 고르지 않았다면(null) 믹스 기믹 자체를 만들지 않는다. 정답이 셰이크인 칵테일에서
    /// 도구를 안 골랐다면 셰이크가 스터로 바뀌는 게 아니라 아예 실행되지 않는다.
    /// </summary>
    public static ENewMixMethod ToMix(string toolId) => toolId switch
    {
        Shaker => ENewMixMethod.Shake,
        MixingGlass => ENewMixMethod.Stir,
        _ => ENewMixMethod.None,
    };

    /// <summary>
    /// 제조법에 필요한 도구의 id. 도구가 필요 없는 빌드 계열은 null이다.
    /// 실제로 고른 도구와 비교해 도구 오류를 판정할 때 쓴다.
    /// </summary>
    public static string FromMix(ENewMixMethod mix) => mix switch
    {
        ENewMixMethod.Shake => Shaker,
        ENewMixMethod.Stir => MixingGlass,
        _ => null,
    };
}

[Serializable]
public struct NewCocktailRecipeStep
{
    [field: SerializeField][JsonProperty("action")] public ENewRecipeAction Action { get; set; }
    [field: SerializeField][JsonProperty("ingredient")] public string Ingredient { get; set; }

    /// <summary>
    /// 목표 수량. 아직 정해지지 않은 줄은 null이다.
    /// null을 0이나 임의의 기본값으로 메우지 않는다 — 오차율은 목표로 나누는 계산이라
    /// 값을 지어내면 그럴듯한 가짜 점수가 나온다. 미확정 항목은 DATA_ERROR로 남겨야 한다.
    /// </summary>
    [JsonProperty("qty")] public float? Qty { get; set; }

    /// <summary>목표 수량의 단위. Qty와 짝이며 함께 비어 있다.</summary>
    [JsonProperty("unit")] public ENewUnit? Unit { get; set; }

    /// <summary>
    /// 이 칵테일의 정체성을 이루는 핵심 재료인지. 핵심 재료를 하나라도 고르지 않으면 나머지를 아무리
    /// 잘 만들어도 최종 등급이 Sewage로 고정된다.
    ///
    /// 수량을 틀린 것은 누락이 아니다 — 진을 0.5oz만 넣었다면 핵심 재료는 존재하므로 수량 오차로 처리하고,
    /// 진을 아예 고르지 않았을 때만 강제 Sewage다.
    /// </summary>
    [field: SerializeField][JsonProperty("is_core")] public bool IsCore { get; set; }

    /// <summary>
    /// 이 줄이 점수에 반영되는지. 데모에서 빠진 기믹(스퀴즈·파우더)은 자동으로 넣어 주기만 하고
    /// 채점하지 않으므로 false다. 채점 대상이 아닌 줄은 기믹 점수와 평균에서 모두 빠진다.
    /// </summary>
    [field: SerializeField][JsonProperty("scored")] public bool Scored { get; set; }

    /// <summary>
    /// 플레이어 조작 없이 시스템이 알아서 넣어 주는지. 선반에 오브젝트가 없는 재료가 여기 해당한다.
    ///
    /// action으로 유추하지 않고 데이터가 직접 말해 준다. 같은 스퀴즈라도 데모에서는 자동으로 넣고
    /// 나중에는 플레이어가 짜게 될 수 있어서, 그 전환을 데이터만 고쳐서 할 수 있어야 한다.
    /// </summary>
    [field: SerializeField][JsonProperty("auto_apply")] public bool AutoApply { get; set; }

    /// <summary>목표 수량이 확정되어 있어 수량 오차를 계산할 수 있는지.</summary>
    public bool HasTarget => Qty.HasValue && Qty.Value > 0f && Unit.HasValue;

    /// <summary>플레이어가 선반에서 직접 골라야 하는 줄인지. 자동으로 넣어 주는 줄은 고를 수 없다.</summary>
    public bool IsSelectable => !AutoApply;
}

/// <summary>
/// 칵테일에 붙는 맛·느낌 태그 하나.
///
/// ko/en만 담는 LocalizedText로 받고 있었는데, 데이터에는 id와 category(taste/feel)가 함께 있어서
/// 그 둘이 조용히 버려지고 있었다. 태그로 거르거나 묶으려면 category가 필요하다.
/// </summary>
[Serializable]
public struct NewCocktailTag
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("ko")] public string Ko { get; set; }
    [field: SerializeField][JsonProperty("en")] public string En { get; set; }

    /// <summary>태그의 갈래. 현재 데이터는 taste와 feel을 쓴다.</summary>
    [field: SerializeField][JsonProperty("category")] public string Category { get; set; }
}

[Serializable]
public struct NewCocktailData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("name")] public LocalizedText Name { get; set; }

    /// <summary>이 칵테일의 데이터가 확정됐는지. tbd면 아직 채우는 중이라 정상 플레이에 쓸 수 없다.</summary>
    [field: SerializeField][JsonProperty("status")] public ENewDataStatus Status { get; set; }

    [field: SerializeField][JsonProperty("price")] public int Price { get; set; }
    [field: SerializeField][JsonProperty("abv")] public float Abv { get; set; }

    /// <summary>정답 잔의 id(shelf_items.json에서 kind=glass). 실제로 고른 잔과 달라도 감점일 뿐 제조는 진행된다.</summary>
    [field: SerializeField][JsonProperty("glass")] public string Glass { get; set; }

    /// <summary>
    /// 정답 제조법. 필요한 도구는 여기서 유도한다(TargetToolId).
    /// 실제로 실행되는 믹스 기믹은 이 값이 아니라 플레이어가 실제로 고른 도구로 정해진다.
    /// </summary>
    [field: SerializeField][JsonProperty("mix")] public ENewMixMethod Mix { get; set; }

    /// <summary>
    /// 판정에 쓰는 정답 제조법. 지금은 mix와 항상 같은 값이지만, 이름이 가리키는 대로 정답 쪽을
    /// 대표하는 값이라 도구를 구할 때 이쪽을 먼저 본다. 둘이 갈라지면 검증기가 잡는다.
    /// </summary>
    [field: SerializeField][JsonProperty("target_mix_method")] public ENewMixMethod TargetMixMethod { get; set; }

    /// <summary>
    /// 이 칵테일에 병 손질이 필요한지. 실제 기믹은 재료 쪽 prep_action으로 만들고,
    /// 이 값은 그 둘이 어긋나지 않는지 확인하는 데 쓴다.
    /// </summary>
    [JsonProperty("target_prep_action")] public ENewPrepAction? TargetPrepAction { get; set; }

    /// <summary>병 손질 표기(문자열). 판정에는 위의 target_prep_action을 쓴다.</summary>
    [field: SerializeField][JsonProperty("prep")] public string Prep { get; set; }

    [field: SerializeField][JsonProperty("garnish")] public string Garnish { get; set; }
    [field: SerializeField][JsonProperty("color")] public string Color { get; set; }
    /// <summary>그라데이션 칵테일의 두 번째 색. 단색이면 null이다.</summary>
    [field: SerializeField][JsonProperty("color2")] public string Color2 { get; set; }
    /// <summary>믹싱에 쓰는 얼음 종류.</summary>
    [field: SerializeField][JsonProperty("mixing_ice")] public string MixingIce { get; set; }
    /// <summary>완성 잔에 담기는 얼음 종류.</summary>
    [field: SerializeField][JsonProperty("serving_ice")] public string ServingIce { get; set; }

    // tags는 문자열 배열이 아니라 {ko, en, category} 객체의 배열이다. string[]으로 두면 cocktails.json
    // 전체가 JsonReaderException으로 터지고, NewDataLoadManager의 로드 순서상 그 뒤 데이터가 전부 안 들어온다.
    [field: SerializeField][JsonProperty("tags")] public NewCocktailTag[] Tags { get; set; }
    [field: SerializeField][JsonProperty("flavor")] public LocalizedText Flavor { get; set; }
    [field: SerializeField][JsonProperty("recipe_desc")] public LocalizedText RecipeDesc { get; set; }
    [field: SerializeField][JsonProperty("unlock_day")] public int UnlockDay { get; set; }
    [field: SerializeField][JsonProperty("unlock_when")] public string UnlockWhen { get; set; }

    /// <summary>
    /// 한 잔 전체의 제조 제한시간. 기믹마다 따로 재지 않고, 첫 기믹이 시작된 순간부터 마지막 기믹이
    /// 끝날 때까지 누적한 시간 하나를 이 값과 비교한다.
    /// </summary>
    [field: SerializeField][JsonProperty("time_limit_sec")] public float TimeLimitSec { get; set; }

    /// <summary>
    /// 정답 레시피. 배열 순서가 곧 정답 항목의 순서다(seq를 따로 두지 않는다 — 두 곳에 순서가 있으면
    /// 한쪽만 고쳐져 어긋난다). 실제 기믹 실행 순서는 이 순서가 아니라 기믹 종류의 고정 우선순위를 따른다.
    /// </summary>
    [field: SerializeField][JsonProperty("recipe")] public NewCocktailRecipeStep[] Recipe { get; set; }

    /// <summary>완성 컷씬에 쓰는 스프라이트 이름.</summary>
    [field: SerializeField][JsonProperty("sprite")] public string Sprite { get; set; }
    /// <summary>서빙 컷씬에 쓰는 스프라이트 이름.</summary>
    [field: SerializeField][JsonProperty("serve_sprite")] public string ServeSprite { get; set; }

    /// <summary>
    /// 이 칵테일이 요구하는 도구의 id. 빌드 계열은 null이다.
    ///
    /// 정답 제조법 하나만 본다. mix로 물러서는 폴백을 두지 않는 이유는, 이 열거형의 기본값이
    /// None이 아니라 Build여서 값이 비어 있어도 "빌드"로 읽히기 때문이다 — 그러면 비어 있다는 걸
    /// 알아챌 수 없이 믹스 기믹만 조용히 사라진다. 값이 비었는지는 검증기가 잡는다.
    /// </summary>
    public string TargetToolId => NewToolIds.FromMix(TargetMixMethod);

    /// <summary>재료 하나가 이 칵테일의 핵심 재료인지. 강제 Sewage 판정에 쓴다.</summary>
    public bool IsCoreIngredient(string ingredientId)
    {
        if (Recipe == null) return false;

        foreach (var step in Recipe)
        {
            if (step.Ingredient == ingredientId && step.IsCore) return true;
        }

        return false;
    }

    /// <summary>핵심 재료 id를 모은다. 하나라도 실제 선택 목록에 없으면 강제 Sewage다.</summary>
    public List<string> GetCoreIngredientIds()
    {
        var ids = new List<string>();
        if (Recipe == null) return ids;

        foreach (var step in Recipe)
        {
            if (step.IsCore && !ids.Contains(step.Ingredient)) ids.Add(step.Ingredient);
        }

        return ids;
    }

    /// <summary>
    /// 플레이어가 선반에서 직접 골라야 하는 정답 재료 id를 모은다.
    /// 레시피 노트의 재료 목록, 가이드 점등 대상, 자동 준비 완료 판정이 모두 이 목록을 쓴다 —
    /// 자동으로 넣어 주는 재료는 고를 수 없으므로 어디에도 끼지 않는다.
    /// </summary>
    public List<string> GetSelectableIngredientIds()
    {
        var ids = new List<string>();
        if (Recipe == null) return ids;

        foreach (var step in Recipe)
        {
            if (step.IsSelectable && !ids.Contains(step.Ingredient)) ids.Add(step.Ingredient);
        }

        return ids;
    }
}

/// <summary>StreamingAssets/json/cocktails.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewCocktailDataSO", menuName = "Data/New/CocktailDataSO")]
public class NewCocktailDataSO : ScriptableObject
{
    public NewCocktailData[] cocktailData;

    /// <summary>id로 칵테일을 찾는다. 없으면 found=false로 반환한다.</summary>
    public bool TryGet(string id, out NewCocktailData cocktail)
    {
        cocktail = default;
        if (string.IsNullOrEmpty(id) || cocktailData == null) return false;

        foreach (var candidate in cocktailData)
        {
            if (candidate.Id != id) continue;

            cocktail = candidate;
            return true;
        }

        return false;
    }
}
