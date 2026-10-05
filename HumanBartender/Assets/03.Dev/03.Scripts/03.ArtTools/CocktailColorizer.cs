using UnityEngine;

/// <summary>
/// SpriteRenderer 에 붙이고, 머티리얼을 Custom/CocktailTint 로 지정하세요.
/// MaterialPropertyBlock 을 쓰기 때문에 머티리얼 인스턴스가 새로 생기지 않고
/// 배칭도 크게 깨지지 않습니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CocktailColorizer : MonoBehaviour
{
    static readonly int LiquidID = Shader.PropertyToID("_LiquidColor");
    static readonly int GlassID  = Shader.PropertyToID("_GlassColor");

    [SerializeField] Color liquidColor = new Color(1f, 0.25f, 0.5f);
    [SerializeField] Color glassColor  = new Color(0.75f, 0.9f, 1f);

    SpriteRenderer _sr;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()  => Apply();
    void OnValidate()             // 에디터에서 값 바꾸면 바로 미리보기
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Apply();
    }

    public void SetColors(Color liquid, Color glass)
    {
        liquidColor = liquid;
        glassColor  = glass;
        Apply();
    }

    void Apply()
    {
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor(LiquidID, liquidColor);
        _mpb.SetColor(GlassID,  glassColor);
        _sr.SetPropertyBlock(_mpb);
    }
}
