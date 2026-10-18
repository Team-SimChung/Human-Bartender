using System;
using UnityEngine;

/// <summary>색/수량의 원본은 재료 데이터에 두고, 빛과 기포의 반응만 정의한다.</summary>
[Serializable]
public struct LiquidAppearance
{
    [Range(0f, 1f)] public float defaultAlpha;
    [Range(0f, 2f)] public float absorption;
    [Range(0f, 1f)] public float turbidity;
    [Range(0f, 1f)] public float gloss;
    [Range(0f, 1f)] public float carbonation;
    [Range(0f, 2f)] public float impactCloud;
    [Min(0.05f)] public float cloudLifetime;
    [Min(0f)] public float bubbleRise;
    [Range(0f, 2f)] public float foamGain;
    [Min(0.05f)] public float foamLifetime;
    [Range(0f, 0.15f)] public float foamHeight;
    [Range(0f, 1f)] public float pulp;
    [Range(0.5f, 8f)] public float viscosityScale;
    public Color foamColor;

    public static LiquidAppearance ForIngredient(string id)
    {
        var result = new LiquidAppearance
        {
            defaultAlpha = 0.35f, absorption = 0.16f, turbidity = 0.02f,
            gloss = 0.48f, impactCloud = 0.10f, cloudLifetime = 0.25f,
            bubbleRise = 0.24f, foamLifetime = 0.4f, viscosityScale = 1f,
            foamColor = new Color(1f, 0.97f, 0.89f)
        };
        switch (id)
        {
            case "beer":
                result.defaultAlpha = 0.75f; result.absorption = 0.65f;
                result.carbonation = 0.55f; result.impactCloud = 1.2f;
                result.cloudLifetime = 1.4f; result.foamGain = 1.2f;
                result.foamLifetime = 7f; result.foamHeight = 0.10f;
                break;
            case "soda_water":
            case "champagne":
            case "ginger_ale":
            case "cola":
                result.carbonation = id == "champagne" ? 0.48f : 0.8f;
                result.impactCloud = 0.55f; result.cloudLifetime = 0.45f;
                result.foamGain = 0.5f; result.foamLifetime = 0.85f;
                result.foamHeight = 0.035f; result.bubbleRise = 0.32f;
                if (id == "ginger_ale" || id == "cola")
                {
                    result.defaultAlpha = 0.75f;
                    result.absorption = id == "cola" ? 1.1f : 0.4f;
                    result.foamColor = new Color(1f, 0.88f, 0.66f);
                }
                break;
            case "orange_juice":
            case "pineapple_juice":
            case "sour_mix":
                result.defaultAlpha = 0.75f; result.turbidity = 0.85f;
                result.absorption = 0.35f; result.gloss = 0.18f;
                result.impactCloud = 0.30f; result.cloudLifetime = 0.55f;
                result.foamGain = id == "pineapple_juice" ? 0.6f : 0.2f;
                result.foamLifetime = 1.1f; result.foamHeight = 0.025f;
                result.pulp = id == "sour_mix" ? 0f : 0.3f;
                result.viscosityScale = 1.3f;
                result.foamColor = new Color(1f, 0.88f, 0.59f);
                break;
            case "milk":
            case "cream":
            case "coconut_milk":
                result.defaultAlpha = 1f; result.turbidity = 1f;
                result.absorption = 0.1f; result.gloss = 0.18f;
                result.impactCloud = 0.12f; result.foamGain = 0.3f;
                result.foamLifetime = 2f; result.foamHeight = 0.03f;
                result.viscosityScale = id == "cream" ? 3f : 1.3f;
                break;
            case "grenadine":
                result.defaultAlpha = 0.75f; result.absorption = 0.9f;
                result.viscosityScale = 5f; result.impactCloud = 0.05f;
                result.gloss = 0.4f;
                break;
            case "rum": case "whiskey": case "brandy": case "wild_dog":
            case "red_wine": case "cranberry_juice": case "amaretto":
            case "kahlua": case "coffee": case "green_peppermint":
            case "campari": case "blue_curacao": case "peach_brandy":
            case "melon_liqueur": case "banana_liqueur": case "bitters":
                result.defaultAlpha = 0.75f; result.absorption = 0.7f;
                break;
            // 진·보드카·데킬라·맑은 리큐르 및 미등록 재료는 비탄산 기본값.
        }
        return result;
    }

    public Color ResolveColor(Color color, float? alpha, bool overrideColor)
    {
        if (!overrideColor) color.a = Mathf.Clamp01(alpha ?? defaultAlpha);
        return color;
    }
}

[Serializable]
public sealed class LiquidAppearanceOverride
{
    public string ingredientId;
    public LiquidAppearance appearance = LiquidAppearance.ForIngredient(null);
}
