using UnityEngine;
using UnityEngine.Rendering;

/// <summary>캐릭터 본체의 접지 음영·역광면 그림자와 광원 쪽 림라이트를 조절한다.
/// 광원은 바닥 그림자와 같은 OutsideShadowLight를 써서 그림자 방향과 림 방향이 어긋나지 않는다.
/// SpriteRenderer에는 Custom/Character/SpriteLitShading 머티리얼이 있어야 하며, 다른 프로퍼티 블록 값은 보존한다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer)), DefaultExecutionOrder(11001)]
public sealed class CharacterSpriteLighting : MonoBehaviour
{
    [Tooltip("끄면 기존 Sprites-Default처럼 2D 라이트 영향 없이 원본 색을 유지하고 음영/림만 더한다.")]
    [SerializeField] bool receive2DLights = true;
    [Header("본체 그림자")]
    [SerializeField] Color shadeColor = new Color(0.32f, 0.3f, 0.45f, 1);
    [Tooltip("발 쪽이 바닥에 가려 어두워지는 정도.")]
    [SerializeField, Range(0, 1)] float groundOcclusion = 0.35f;
    [Tooltip("접지 음영이 올라오는 높이(키 비율).")]
    [SerializeField, Range(0.05f, 1)] float occlusionHeight = 0.45f;
    [Tooltip("광원 반대편 외곽 안쪽 그늘 강도.")]
    [SerializeField, Range(0, 1)] float sideShadow = 0.4f;
    [SerializeField, Range(1, 6)] float sideShadowWidth = 2;
    [Tooltip("근처에 광원이 없을 때 쓰는 하늘빛 방향과 그때의 역광면 그림자 비율.")]
    [SerializeField] Vector2 ambientDirection = new Vector2(0.35f, 1);
    [SerializeField, Range(0, 1)] float ambientSideShadow = 0.4f;
    [Header("림라이트")]
    [SerializeField, Range(0, 4)] float rimIntensity = 1.2f;
    [SerializeField, Range(1, 4)] float rimWidth = 1;
    [Tooltip("0이면 rimColor, 1이면 광원 색을 그대로 쓴다.")]
    [SerializeField, Range(0, 1)] float rimUsesLightColor = 0.75f;
    [SerializeField] Color rimColor = new Color(1, 0.85f, 0.7f, 1);
    [Tooltip("클수록 광원을 정면으로 향한 외곽에만 림이 생긴다. 1이면 비스듬한 외곽도 넓게 밝아진다.")]
    [SerializeField, Range(0.5f, 4)] float rimFocus = 2;
    [Tooltip("광원에서 먼 부분(가로등 아래의 발끝 등)일수록 림이 약해지는 정도. 0이면 몸 전체가 같은 밝기.")]
    [SerializeField, Range(0, 3)] float rimDistanceFalloff = 1.5f;
    [Tooltip("림을 더 밝히고 안쪽으로 번지는 띠를 더해 블룸이 걸리기 쉽게 한다.")]
    [SerializeField, Range(0, 2)] float rimGlow = 0.6f;
    [Tooltip("번지는 띠의 폭(원본 텍셀).")]
    [SerializeField, Range(1, 6)] float rimGlowWidth = 3;
    [Tooltip("광원 영향이 이 값 이상이면 림이 최대가 된다.")]
    [SerializeField, Min(0.01f)] float rimFullInfluence = 0.6f;
    [SerializeField, Min(0.01f)] float transitionSeconds = 0.3f;

    static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
    static readonly int BodyShadeId = Shader.PropertyToID("_BodyShade");
    static readonly int RimId = Shader.PropertyToID("_Rim");
    static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    static readonly int RimGlowId = Shader.PropertyToID("_RimGlow");
    static readonly int RimShapeId = Shader.PropertyToID("_RimShape");
    static readonly int LightPosId = Shader.PropertyToID("_LightPos");
    static readonly int AmbientDirId = Shader.PropertyToID("_AmbientDir");
    static readonly int BodyBoundsId = Shader.PropertyToID("_BodyBounds");
    static readonly int SpriteTexelId = Shader.PropertyToID("_SpriteTexel");
    public const string ShaderName = "Custom/Character/SpriteLitShading";

    SpriteRenderer source;
    OutsideCharacterShadow ground;
    MaterialPropertyBlock block;
    OutsideShadowLight selected;
    Vector2 lightPosition;
    Color litRim;
    float lightWeight;

    public OutsideShadowLight CurrentLight => selected;
    public float RimStrength => lightWeight;
    public Vector2 LightPosition => lightPosition;

    void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        ground = GetComponent<OutsideCharacterShadow>();
        block ??= new MaterialPropertyBlock();
        lightWeight = 0;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        ClearEffects();
    }

    // 컴포넌트나 설정을 끄면 기존 스프라이트와 같은 결과가 되도록 효과만 0으로 되돌린다.
    void ClearEffects()
    {
        if (source == null) return;
        source.GetPropertyBlock(block);
        block.SetVector(BodyShadeId, Vector4.zero);
        block.SetVector(RimId, new Vector4(0, 1, receive2DLights ? 1 : 0, 0));
        block.SetColor(RimColorId, Color.clear);
        source.SetPropertyBlock(block);
    }

    Vector2 Ambient() => ambientDirection.sqrMagnitude > 0.0001f ? ambientDirection.normalized : Vector2.up;
    Vector2 Foot() => ground != null ? (Vector2)ground.FootPosition : new Vector2(source.bounds.center.x, source.bounds.min.y);

    void LateUpdate() => Refresh();

    /// <summary>현재 발 위치 기준으로 광원을 다시 고르고 림/그늘 방향을 보간한다.</summary>
    public void Refresh()
    {
        if (source == null) return;
        Vector2 foot = Foot();
        selected = OutsideShadowLight.Select(foot, source, selected);
        float influence = selected != null ? selected.Influence(foot, source) : 0;
        float target = Mathf.Clamp01(influence / rimFullInfluence);
        float step = Application.isPlaying ? Time.deltaTime / Mathf.Max(0.01f, transitionSeconds) : 1;
        lightWeight = Mathf.MoveTowards(lightWeight, target, step);
        if (selected == null) return; // 광원을 벗어나면 마지막 방향을 유지한 채 세기만 줄어든다.
        Color targetColor = Color.Lerp(rimColor, selected.Color, rimUsesLightColor);
        // 림이 꺼져 있던 상태에서 새 광원에 들어오면 이전 광원 위치에서 끌려오지 않고 바로 그 방향을 쓴다.
        float follow = !Application.isPlaying || lightWeight <= step ? 1 : Mathf.Clamp01(step * 2);
        lightPosition = Vector2.Lerp(lightPosition, selected.Position, follow);
        litRim = Color.Lerp(litRim, targetColor, follow);
    }

    // Animator가 스프라이트를 바꾼 뒤 렌더 직전에 현재 프레임 텍스처 기준으로 갱신한다.
    void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (source == null || source.sprite == null) return;
        if (!GraphicsEffectOptions.CharacterLighting) { ClearEffects(); return; }
        var texture = source.sprite.texture;
        Vector2 foot = Foot();
        float height = Mathf.Max(0.05f, source.bounds.max.y - foot.y);
        float sideWeight = Mathf.Lerp(ambientSideShadow, 1, lightWeight);
        source.GetPropertyBlock(block);
        block.SetColor(ShadeColorId, shadeColor);
        block.SetVector(BodyShadeId, new Vector4(groundOcclusion, occlusionHeight, sideShadow, sideShadowWidth));
        block.SetVector(RimId, new Vector4(rimIntensity, rimWidth, receive2DLights ? 1 : 0, 0));
        block.SetColor(RimColorId, litRim * lightWeight);
        block.SetVector(RimGlowId, new Vector4(rimGlow, rimGlowWidth, 0, 0));
        // 거리 감쇠 기준: 광원에서 스프라이트 영역까지 가장 가까운 거리. 그 지점이 최대 밝기다.
        var bounds = source.bounds;
        float nearest = Vector2.Distance(lightPosition, bounds.ClosestPoint(new Vector3(lightPosition.x, lightPosition.y, bounds.center.z)));
        block.SetVector(RimShapeId, new Vector4(rimFocus, rimDistanceFalloff, Mathf.Max(0.05f, nearest), 0));
        block.SetVector(LightPosId, new Vector4(lightPosition.x, lightPosition.y, sideWeight, lightWeight));
        Vector2 ambient = Ambient();
        block.SetVector(AmbientDirId, new Vector4(ambient.x, ambient.y, 0, 0));
        block.SetVector(BodyBoundsId, new Vector4(foot.y, height, 0, 0));
        block.SetVector(SpriteTexelId, new Vector4(1f / texture.width, 1f / texture.height, 0, 0));
        source.SetPropertyBlock(block);
    }
}
