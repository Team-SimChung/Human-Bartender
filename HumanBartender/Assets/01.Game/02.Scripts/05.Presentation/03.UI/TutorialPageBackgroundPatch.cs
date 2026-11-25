using UnityEngine;
using UnityEngine.UI;

/// <summary>튜토리얼 이미지의 일부를 같은 스프라이트의 빈 배경으로 바꾼다.</summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class TutorialPageBackgroundPatch : BaseMeshEffect
{
    [Header("Source Sprite Pixels")]
    [Tooltip("비울 영역. 원본 스프라이트 왼쪽 위가 (0, 0)이다.")]
    [SerializeField] Rect patchPixels;
    [Tooltip("가져올 빈 배경의 픽셀 위치. 원본 스프라이트 왼쪽 위가 (0, 0)이다.")]
    [SerializeField] Vector2Int backgroundPixel;

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || mesh.currentVertCount != 4) return;

        var sprite = ((Image)graphic).overrideSprite;
        if (sprite == null) return;

        Vector2 size = sprite.rect.size;
        if (patchPixels.width <= 0 || patchPixels.height <= 0 ||
            patchPixels.xMin < 0 || patchPixels.yMin < 0 ||
            patchPixels.xMax > size.x || patchPixels.yMax > size.y ||
            backgroundPixel.x < 0 || backgroundPixel.x >= size.x ||
            backgroundPixel.y < 0 || backgroundPixel.y >= size.y) return;

        var bottomLeft = UIVertex.simpleVert;
        var topRight = UIVertex.simpleVert;
        mesh.PopulateUIVertex(ref bottomLeft, 0);
        mesh.PopulateUIVertex(ref topRight, 2);

        float left = patchPixels.xMin / size.x;
        float right = patchPixels.xMax / size.x;
        float bottom = 1f - patchPixels.yMax / size.y;
        float top = 1f - patchPixels.yMin / size.y;
        Vector2 sample = SpriteUv((backgroundPixel.x + .5f) / size.x, 1f - (backgroundPixel.y + .5f) / size.y);

        // 한 Image 안에서 영역을 나눠 페이드 중에도 투명도가 두 번 합성되지 않게 한다.
        mesh.Clear();
        AddQuad(0, 0, 1, bottom);
        AddQuad(0, top, 1, 1);
        AddQuad(0, bottom, left, top);
        AddQuad(right, bottom, 1, top);
        AddQuad(left, bottom, right, top, true);

        Vector2 SpriteUv(float x, float y) => new(
            Mathf.LerpUnclamped(bottomLeft.uv0.x, topRight.uv0.x, x),
            Mathf.LerpUnclamped(bottomLeft.uv0.y, topRight.uv0.y, y));

        void AddQuad(float xMin, float yMin, float xMax, float yMax, bool replace = false)
        {
            if (xMin >= xMax || yMin >= yMax) return;

            int first = mesh.currentVertCount;
            AddVertex(xMin, yMin, replace);
            AddVertex(xMin, yMax, replace);
            AddVertex(xMax, yMax, replace);
            AddVertex(xMax, yMin, replace);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }

        void AddVertex(float x, float y, bool replace)
        {
            var position = new Vector3(
                Mathf.LerpUnclamped(bottomLeft.position.x, topRight.position.x, x),
                Mathf.LerpUnclamped(bottomLeft.position.y, topRight.position.y, y));
            mesh.AddVert(position, bottomLeft.color, replace ? sample : SpriteUv(x, y));
        }
    }
}
