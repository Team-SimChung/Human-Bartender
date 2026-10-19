using System;
using UnityEngine;

/// <summary>
/// 색/수량의 원본은 재료 데이터(RGB, liquid_alpha)에 두고, 여기에는 화면에서의 보정과
/// 빛·기포·거품의 반응만 정의한다. 데이터 색은 바꾸지 않고 렌더러가 보정값을 적용한다.
/// </summary>
[Serializable]
public struct LiquidAppearance
{
    [Range(0f, 1f)] public float defaultAlpha;
    [Tooltip("색 존재감. 1이면 데이터 색 그대로. 휘도와 색상을 유지하며 RGB 범위 안에서만 채도를 올린다.")]
    [Range(0f, 2f)] public float saturation;
    [Tooltip("깊이에 따른 흡수. 두꺼운 곳일수록 어두워지며 색이 진해진다.")]
    [Range(0f, 2f)] public float absorption;
    [Range(0f, 1f)] public float turbidity;
    [Range(0f, 1f)] public float gloss;
    [Range(0f, 1f)] public float carbonation;
    [Header("충돌 미세기포 구름 (탄산만)")]
    [Tooltip("충돌 한 번이 남기는 미세기포 양.")]
    [Range(0f, 2f)] public float impactCloud;
    [Tooltip("충돌 지점에서 구름이 생기는 반경(물리 블롭 반지름 배수).")]
    [Range(0.1f, 2f)] public float cloudSpread;
    [Tooltip("구름의 부력 상승 속도(월드/초).")]
    [Min(0f)] public float cloudRise;
    [Tooltip("오르며 흐트러지고 퍼지는 정도.")]
    [Range(0f, 2f)] public float cloudDispersion;
    [Min(0.05f)] public float cloudLifetime;
    [Tooltip("구름이 액체 색을 옅게 하는 최대 강도. 흰 막으로 덮지 않는다.")]
    [Range(0f, 1f)] public float cloudOpacity;
    [Tooltip("탄산 기포 패턴의 상승 속도(월드/초).")]
    [Min(0f)] public float bubbleRise;
    [Header("수면 거품 (구름과 독립)")]
    [Tooltip("실제 유입 흐름과 수면 충돌에서 만드는 거품 양.")]
    [Range(0f, 3f)] public float foamGain;
    [Tooltip("유입이 멈춘 뒤 거품을 거의 그대로 유지하는 시간(초).")]
    [Min(0f)] public float foamHold;
    [Tooltip("유지 시간 이후 가라앉는 시간 상수(초).")]
    [Min(0.05f)] public float foamLifetime;
    [Tooltip("거품층 최대 높이(잔 높이 비율). 잔 입구 위로는 거의 넘지 않는다.")]
    [Range(0f, 0.3f)] public float foamHeight;
    [Tooltip("거품 윗면의 덩어리와 높낮이.")]
    [Range(0f, 1f)] public float foamLumps;
    [Range(0f, 1f)] public float pulp;
    [Range(0.5f, 8f)] public float viscosityScale;
    public Color foamColor;

    public static LiquidAppearance ForIngredient(string id)
    {
        var result = new LiquidAppearance
        {
            defaultAlpha = 0.35f, saturation = 1.1f, absorption = 0.16f, turbidity = 0.02f,
            gloss = 0.48f, impactCloud = 0f, cloudSpread = 0.7f, cloudRise = 0.22f,
            cloudDispersion = 0.6f, cloudLifetime = 0.25f, cloudOpacity = 0.6f,
            bubbleRise = 0.24f, foamHold = 0.3f, foamLifetime = 0.4f, foamLumps = 0.3f,
            viscosityScale = 1f, foamColor = new Color(1f, 0.97f, 0.89f)
        };
        switch (id)
        {
            case "beer":
                // 금빛·호박색 몸통. 미세기포 구름은 작게 생겨 빨리 흩어지고,
                // 거품은 유입에 바로 반응해 두껍게 솟은 뒤 멈춰도 몇 초 남았다가 천천히 꺼진다.
                result.defaultAlpha = 0.75f; result.saturation = 1.15f; result.absorption = 0.65f; result.turbidity = 0.1f;
                result.carbonation = 0.55f; result.impactCloud = 0.9f; result.cloudSpread = 0.55f;
                result.cloudRise = 0.32f; result.cloudDispersion = 0.9f;
                result.cloudLifetime = 1.0f; result.cloudOpacity = 0.5f;
                result.foamGain = 1.6f; result.foamHold = 3.5f;
                result.foamLifetime = 8f; result.foamHeight = 0.17f; result.foamLumps = 1f;
                result.foamColor = new Color(1f, 0.97f, 0.88f);
                break;
            case "soda_water":
            case "champagne":
            case "ginger_ale":
            case "cola":
                result.carbonation = id == "champagne" ? 0.48f : 0.8f;
                result.impactCloud = 0.55f; result.cloudLifetime = 0.45f; result.cloudSpread = 0.5f;
                result.cloudRise = 0.4f; result.cloudOpacity = 0.4f;
                result.foamGain = id == "champagne" ? 1.0f : 0.6f;
                result.foamHold = id == "champagne" ? 0.4f : 0.15f;
                result.foamLifetime = id == "champagne" ? 1.4f : 0.7f;
                result.foamHeight = id == "champagne" ? 0.07f : 0.035f;
                result.foamLumps = 0.4f; result.bubbleRise = 0.32f;
                if (id == "ginger_ale" || id == "cola")
                {
                    result.defaultAlpha = 0.75f;
                    result.absorption = id == "cola" ? 1.1f : 0.4f;
                    result.foamHeight = 0.05f; result.foamHold = 0.3f; result.foamLifetime = 1.1f;
                    result.foamColor = new Color(1f, 0.88f, 0.66f);
                }
                break;
            case "orange_juice":
            case "pineapple_juice":
            case "sour_mix":
                // 선명하고 탁한 과즙. 탄산 구름 없이 고유 탁도·과육·얇은 거품만 둔다.
                result.defaultAlpha = 0.75f; result.saturation = 1.2f; result.turbidity = 0.85f;
                result.absorption = 0.35f; result.gloss = 0.18f;
                result.impactCloud = 0f; result.cloudLifetime = 0.55f;
                result.foamGain = id == "pineapple_juice" ? 0.6f : 0.2f;
                result.foamHold = 0.3f; result.foamLifetime = 1.1f; result.foamHeight = 0.025f;
                result.pulp = id == "sour_mix" ? 0f : 0.3f;
                result.viscosityScale = 1.3f;
                result.foamColor = new Color(1f, 0.88f, 0.59f);
                break;
            case "milk":
            case "cream":
            case "coconut_milk":
                result.defaultAlpha = 1f; result.saturation = 1f; result.turbidity = 1f;
                result.absorption = 0.1f; result.gloss = 0.18f;
                result.impactCloud = 0f; result.foamGain = 0.3f; result.foamHold = 0.5f;
                result.foamLifetime = 2f; result.foamHeight = 0.03f;
                result.viscosityScale = id == "cream" ? 3f : 1.3f;
                break;
            case "grenadine":
                result.defaultAlpha = 0.75f; result.saturation = 1.15f; result.absorption = 0.9f;
                result.viscosityScale = 5f; result.impactCloud = 0f;
                result.gloss = 0.4f;
                break;
            case "rum": case "whiskey": case "brandy": case "wild_dog":
            case "red_wine": case "cranberry_juice": case "amaretto":
            case "kahlua": case "coffee": case "green_peppermint":
            case "campari": case "blue_curacao": case "peach_brandy":
            case "melon_liqueur": case "banana_liqueur": case "bitters":
                result.defaultAlpha = 0.75f; result.saturation = 1.2f; result.absorption = 0.7f;
                break;
            // 진·보드카·데킬라·맑은 리큐르 및 미등록 재료는 비탄산 기본값.
            // 거의 무채색이라 채도 보정이 없는 색을 만들지 않고 맑은 느낌을 유지한다.
        }
        return result;
    }

    public Color ResolveColor(Color color, float? alpha, bool overrideColor)
    {
        if (!overrideColor) color.a = Mathf.Clamp01(alpha ?? defaultAlpha);
        return color;
    }

    /// <summary>
    /// 휘도와 색상을 유지한 채 채도를 조절한다. 어떤 채널도 0~1을 벗어나지 않는 배율까지만
    /// 올리므로 채널별 클리핑으로 색상이 틀어지거나 계조가 뭉개지지 않는다.
    /// </summary>
    public static Color Saturate(Color color, float amount)
    {
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        float limit = Mathf.Max(0f, amount);
        Limit(color.r - luminance, luminance, ref limit);
        Limit(color.g - luminance, luminance, ref limit);
        Limit(color.b - luminance, luminance, ref limit);
        return new Color(luminance + (color.r - luminance) * limit,
                         luminance + (color.g - luminance) * limit,
                         luminance + (color.b - luminance) * limit, color.a);
    }

    static void Limit(float chroma, float luminance, ref float limit)
    {
        if (chroma > 0.00001f) limit = Mathf.Min(limit, (1f - luminance) / chroma);
        else if (chroma < -0.00001f) limit = Mathf.Min(limit, luminance / -chroma);
    }
}

[Serializable]
public sealed class LiquidAppearanceOverride
{
    public string ingredientId;
    public LiquidAppearance appearance = LiquidAppearance.ForIngredient(null);
}
