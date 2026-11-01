using UnityEngine;
using UnityEngine.UI;

/// <summary>로고만 가로 띠로 나누는 uGUI 메시 효과. 공유 머티리얼과 원본 이미지는 변경하지 않는다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class TitleLogoGlitch : BaseMeshEffect
{
    [SerializeField, Min(.1f)] float period = 2.86f;
    [SerializeField, Min(0)] float displacement = 14f;
    readonly UIVertex[] quad = new UIVertex[4];
    float strength;
    double startedAt;
    protected override void OnEnable() { base.OnEnable(); startedAt = Time.unscaledTimeAsDouble; }
    protected override void OnDisable() { strength = 0; base.OnDisable(); }
    void Update()
    {
        float t = (float)((Time.unscaledTimeAsDouble - startedAt) % period);
        float next = t < .87f || t > 2.35f ? 0 : Mathf.Abs(Mathf.Sin(t * 47f)) * Mathf.Abs(Mathf.Sin(t * 19f));
        if (next == strength) return;
        strength = next;
        graphic.SetVerticesDirty();
    }
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || strength <= 0 || vh.currentVertCount != 4) return;
        UIVertex bl = default, tl = default, tr = default, br = default;
        vh.PopulateUIVertex(ref bl, 0); vh.PopulateUIVertex(ref tl, 1);
        vh.PopulateUIVertex(ref tr, 2); vh.PopulateUIVertex(ref br, 3);
        vh.Clear();
        int tick = Mathf.FloorToInt((float)(Time.unscaledTimeAsDouble - startedAt) * 30);
        for (int band = 0; band < 24; band++)
        {
            float a = band / 24f, b = (band + 1) / 24f;
            float dx = ((band * 17 + tick * 13) % 11 - 5) / 5f * displacement * strength;
            quad[0] = Interpolate(bl, tl, a, dx); quad[1] = Interpolate(bl, tl, b, dx);
            quad[2] = Interpolate(br, tr, b, dx); quad[3] = Interpolate(br, tr, a, dx);
            vh.AddUIVertexQuad(quad);
        }
    }
    static UIVertex Interpolate(UIVertex a, UIVertex b, float t, float dx)
    {
        a.position = Vector3.Lerp(a.position, b.position, t) + new Vector3(dx, 0, 0);
        a.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
        return a;
    }
}
