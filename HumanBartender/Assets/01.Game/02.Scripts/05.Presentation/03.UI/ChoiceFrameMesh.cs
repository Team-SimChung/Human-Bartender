using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>서로 다른 스프라이트 슬라이스 여백을 원본의 공통 버튼 사각형에 맞춘다.</summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class ChoiceFrameMesh : BaseMeshEffect
{
    [Tooltip("PNG 좌표의 실제 테두리 영역. 일반/선택 상태의 글로우 여백은 이 영역 바깥에 표시한다.")]
    public Rect frameInTexture = new Rect(37, 38, 2028, 156);
    public Vector2 sourceTextureSize = new Vector2(2103, 228);

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || frameInTexture.width <= 0 || frameInTexture.height <= 0) return;
        var image = (Image)graphic;
        var sprite = image.overrideSprite;
        if (sprite == null) return;
        Rect source = sprite.rect, area = graphic.rectTransform.rect;
        // TextureImporter의 Max Size로 줄어든 크기도 원본 PNG의 공통 프레임 좌표에 반영한다.
        Vector2 importScale = new Vector2(sprite.texture.width / sourceTextureSize.x, sprite.texture.height / sourceTextureSize.y);
        Rect frame = new Rect(Vector2.Scale(frameInTexture.position, importScale), Vector2.Scale(frameInTexture.size, importScale));
        Vector4 padding = DataUtility.GetPadding(sprite);
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        float left = area.xMin + (source.xMin + padding.x - frame.xMin) / frame.width * area.width;
        float right = area.xMin + (source.xMax - padding.z - frame.xMin) / frame.width * area.width;
        float bottom = area.yMin + (source.yMin + padding.y - frame.yMin) / frame.height * area.height;
        float top = area.yMin + (source.yMax - padding.w - frame.yMin) / frame.height * area.height;
        mesh.Clear();
        mesh.AddVert(new Vector3(left, bottom), image.color, new Vector2(uv.x, uv.y));
        mesh.AddVert(new Vector3(left, top), image.color, new Vector2(uv.x, uv.w));
        mesh.AddVert(new Vector3(right, top), image.color, new Vector2(uv.z, uv.w));
        mesh.AddVert(new Vector3(right, bottom), image.color, new Vector2(uv.z, uv.y));
        mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
    }
}
