using UnityEngine;
using UnityEngine.UI;

namespace HumanBartender.CutsceneStudio
{
    // 원본 그림의 볼록한 영역을 잘라 보여 준다. 배경 가림과 크레딧 소품이 같은 원화를 공유한다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StudioIllustrationRegion : MaskableGraphic
    {
        private StudioActor actor;
        public override Texture mainTexture => actor?.RegionTexture;

        internal void Configure(StudioActor source)
        {
            actor = source;
            material = source.RegionMaterial;
            raycastTarget = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (actor?.HasRegion != true) return;
            var points = actor.Region;
            Vector2 min = points[0], max = points[0];
            foreach (var point in points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            var rect = GetPixelAdjustedRect();
            foreach (var point in points)
            {
                var position = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(min.x, max.x, point.x)),
                    Mathf.Lerp(rect.yMax, rect.yMin, Mathf.InverseLerp(min.y, max.y, point.y)));
                var uv = new Vector2(point.x / mainTexture.width, 1 - point.y / mainTexture.height);
                mesh.AddVert(position, color, uv);
            }
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(0, i, i + 1);
        }
    }
}
