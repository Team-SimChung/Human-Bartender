using UnityEngine;

// 캐릭터의 SpriteRenderer에 함께 붙여 둔다.
// AnimationClip이 매 프레임 sprite를 바꿔도, 현재 스프라이트의 로컬 Y bounds를
// 셰이더(_SpriteBounds)에 주입해 reveal 경계가 프레임에 흔들리지 않게 한다.
// _Progress 값 자체는 건드리지 않으므로 타임라인/외부에서 설정한 값이 그대로 유지된다.
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class SpriteRevealBinder : MonoBehaviour
{
    static readonly int BoundsId = Shader.PropertyToID("_SpriteBounds");

    SpriteRenderer sr;
    MaterialPropertyBlock block;
    Sprite lastSprite;

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        block = new MaterialPropertyBlock();
        lastSprite = null;
        Apply();
    }

    void LateUpdate()
    {
        // 스프라이트가 바뀐 프레임에만 갱신 (매 프레임 SetPropertyBlock 비용 회피)
        if (sr.sprite != lastSprite)
            Apply();
    }

    void Apply()
    {
        lastSprite = sr.sprite;
        if (sr.sprite == null) return;

        // sprite.bounds는 Pivot 기준 로컬 공간. y축 최소값과 높이를 넘긴다.
        Bounds b = sr.sprite.bounds;
        float minY   = b.min.y;
        float height = b.size.y;

        sr.GetPropertyBlock(block);
        block.SetVector(BoundsId, new Vector4(minY, height, 0f, 0f));
        sr.SetPropertyBlock(block);
    }
}
