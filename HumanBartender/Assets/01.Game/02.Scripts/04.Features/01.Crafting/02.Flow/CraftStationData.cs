using System.Collections.Generic;
using UnityEngine;

/// <summary>진행 중인 제조(미니게임)의 결과 상태를 담는 데이터.</summary>
public class CraftingResult
{
    public bool isResult = false;
    public string selectMethod = "";
    public int actionFailCount = 0;
    public int limitFailCount = 0;
    public Color32 mixedColor = Color.white;
}

/// <summary>스테이션에 담긴 재료 하나와 그 투입량(value)을 나타낸다.</summary>
public class CraftIngrediantData
{
    public IngredientData data = default;
    public int value = 0;

    public CraftIngrediantData(IngredientData data, int value)
    {
        this.data = data;
        this.value = value;
    }
}

/// <summary>
/// 현재 제조대(Craft Station)에 담긴 재료 구성과 진행 상태를 보관하는 ScriptableObject.
/// 미니게임 독립 테스트 씬이 목표 칵테일을 넘겨받는 데 쓴다. 신형 제조 루프는 CraftContext로 대신한다.
/// </summary>
[CreateAssetMenu(fileName = "CraftLiquidData", menuName = "Scriptable Objects/CraftLiquidData")]
public class CraftStationData : ScriptableObject
{
    public string targetCocktailId;
    public int targetCraft_tolerance;
    public NewCocktailData targetCocktailData;

    public Dictionary<string, CraftIngrediantData> ingredientDatas = new();
    
    public CraftingResult craftingResult = new();

    /// <summary>
    /// 재료를 추가/제거한다. 신규 재료면 amount가 0 이하일 때 무시하고, 기존 재료면 누적하며
    /// 누적값이 0 이하가 되면 목록에서 제거한다.
    /// </summary>
    public void AddIngrediant(IngredientData data, int amount)
    {
        if (!ingredientDatas.ContainsKey(data.Id))
        {
            if (amount <= 0) return;

            CraftIngrediantData initial = new CraftIngrediantData(data, amount);
            ingredientDatas.Add(data.Id, initial);
        }
        else
        {
            ingredientDatas[data.Id].value += amount;

            if(ingredientDatas[data.Id].value <= 0)
                ingredientDatas.Remove(data.Id);
        }
    }

    /// <summary>담긴 재료를 모두 비운다.</summary>
    public void ResetIngrediant()
    {
        ingredientDatas.Clear();
    }

    /// <summary>재료, 목표 칵테일, 제조 결과를 모두 초기 상태로 되돌린다.</summary>
    public void ResetCraftStation()
    {
        ResetIngrediant();
        targetCocktailData = default;
        craftingResult = new();
    }
}
