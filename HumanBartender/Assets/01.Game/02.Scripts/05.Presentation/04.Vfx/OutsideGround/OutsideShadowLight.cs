using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>기존 Light2D의 밝기/활성 상태와 별도로 지정한 실제 발광 위치를 그림자에 제공한다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class OutsideShadowLight : MonoBehaviour
{
    [SerializeField] Light2D source;
    [SerializeField] Transform emissionAnchor;
    [SerializeField] Vector2 emissionOffset;
    [Tooltip("Freeform 라이트는 피벗 대신 빛 모양의 가장 높은 변(램프 머리 쪽)을 발광점으로 쓴다. 끄면 피벗 + emissionOffset.")]
    [SerializeField] bool useShapeTop = true;
    [SerializeField, Min(0.1f)] float radius = 3;
    [SerializeField, Min(0)] float priority = 1;
    [Tooltip("배경 이미지에 그려진 조명용 기준점. 실제 Light2D가 없는 씬에서만 명시적으로 사용한다.")]
    [SerializeField] bool bakedSource;
    [SerializeField, Min(0)] float bakedIntensity = 1;
    [SerializeField] OutsideGroundReceiver bakedGround;
    [Tooltip("baked 기준점의 빛 색. 림라이트 색에 쓰인다.")]
    [SerializeField] Color bakedColor = new Color(1, 0.86f, 0.68f, 1);
    static readonly List<OutsideShadowLight> Active = new List<OutsideShadowLight>();
    public Vector3 Position
    {
        get
        {
            if (emissionAnchor != null) return emissionAnchor.position;
            if (useShapeTop && !bakedSource && source != null && source.lightType == Light2D.LightType.Freeform && ShapeTop(source.shapePath, out Vector2 top))
                return source.transform.TransformPoint(top);
            return transform.TransformPoint(emissionOffset);
        }
    }

    /// <summary>모양 경로에서 가장 높은 두 점의 중점. 원뿔형 가로등이면 램프 머리, 바닥 글로우면 바닥 근처가 된다.</summary>
    public static bool ShapeTop(Vector3[] path, out Vector2 top)
    {
        top = default;
        if (path == null || path.Length < 2) return false;
        int first = 0, second = -1;
        for (int i = 1; i < path.Length; i++)
        {
            if (path[i].y > path[first].y) { second = first; first = i; }
            else if (second < 0 || path[i].y > path[second].y) second = i;
        }
        top = (path[first] + path[second]) * .5f;
        return true;
    }
    public Color Color => !bakedSource && source != null ? source.color : bakedColor;

    public void Configure(Light2D light, Vector2 offset, float range)
    {
        source = light; bakedSource = false; bakedGround = null; emissionOffset = offset; radius = Mathf.Max(0.1f, range);
    }
    public void ConfigureBaked(float range, float intensity = 1, OutsideGroundReceiver ground = null)
    {
        source = null; bakedSource = true; bakedGround = ground; radius = Mathf.Max(0.1f, range); bakedIntensity = Mathf.Max(0, intensity);
    }
    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() => Active.Remove(this);

    public float Influence(Vector3 foot, SpriteRenderer caster)
    {
        if (!isActiveAndEnabled ||
            caster == null || caster.gameObject.scene != gameObject.scene) return 0;
        float strength;
        if (bakedSource)
        {
            if (bakedGround != null && OutsideGroundReceiver.Find(foot, caster.gameObject.scene) != bakedGround) return 0;
            strength = bakedIntensity;
        }
        else
        {
            if (source == null || !source.isActiveAndEnabled || source.lightType == Light2D.LightType.Global || source.intensity <= 0) return 0;
            bool affectsLayer = false;
            var layers = source.targetSortingLayers;
            if (layers != null)
                foreach (int layer in layers) if (layer == caster.sortingLayerID) { affectsLayer = true; break; }
            if (!affectsLayer) return 0;
            strength = Mathf.Clamp(source.intensity * source.color.grayscale, 0, 4);
        }
        float falloff = Mathf.Clamp01(1 - Vector2.Distance(foot, Position) / Mathf.Max(radius, 0.1f));
        return priority * strength * falloff * falloff;
    }

    public static OutsideShadowLight Select(Vector3 foot, SpriteRenderer caster, OutsideShadowLight current)
    {
        OutsideShadowLight best = current != null && current.Influence(foot, caster) > 0 ? current : null;
        // 광원 경계의 작은 움직임으로 선택이 왕복하지 않도록 현재 광원을 약간 우대한다.
        float score = best != null ? best.Influence(foot, caster) * 1.15f : 0;
        foreach (var light in Active)
        {
            if (light == null) continue;
            float candidate = light.Influence(foot, caster);
            if (candidate > score) { best = light; score = candidate; }
        }
        return best;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Position, 0.06f);
        Gizmos.DrawWireSphere(Position, radius);
    }
}
