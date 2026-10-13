using System;
using HumanBartender.CutsceneStudio;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    public sealed class StudioPreview : IDisposable
    {
        private readonly PreviewRenderUtility renderer;
        private RenderTexture displayTexture;
        public StudioStage Stage { get; }

        public StudioPreview(StudioSequence sequence)
        {
            renderer = new PreviewRenderUtility();
            var camera = renderer.camera;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cameraType = CameraType.Game;
            var root = new GameObject("Cutscene Studio Preview", typeof(RectTransform), typeof(Canvas));
            renderer.AddSingleGO(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            ((RectTransform)root.transform).sizeDelta = new Vector2(StudioSequence.Width, StudioSequence.Height);
            Stage = StudioStage.Rect("Stage", root.transform, new Vector2(StudioSequence.Width, StudioSequence.Height)).gameObject.AddComponent<StudioStage>();
            Stage.Build(sequence);
        }

        public Texture Draw(Rect rect, double time, bool reveal)
        {
            Stage.Evaluate(time, reveal);
            renderer.camera.aspect = rect.width / Mathf.Max(1, rect.height);
            renderer.camera.orthographicSize = Mathf.Max(StudioSequence.Height / 2, StudioSequence.Width / (2 * renderer.camera.aspect));
            renderer.BeginPreview(rect, GUIStyle.none);
            Texture rendered;
            try
            {
                var target = renderer.camera.targetTexture;
                renderer.camera.pixelRect = new Rect(0, 0, target.width, target.height);
                Canvas.ForceUpdateCanvases();
                renderer.Render(true);
            }
            finally
            {
                rendered = renderer.EndPreview();
            }

            if (displayTexture == null || displayTexture.width != rendered.width || displayTexture.height != rendered.height)
            {
                if (displayTexture != null)
                    UnityEngine.Object.DestroyImmediate(displayTexture);
                displayTexture = new RenderTexture(rendered.width, rendered.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // 미리보기 버퍼를 게임 화면과 같은 색상 인코딩으로 변환함.
            var previousTarget = RenderTexture.active;
            bool previousWrite = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(rendered, displayTexture);
            }
            finally
            {
                GL.sRGBWrite = previousWrite;
                RenderTexture.active = previousTarget;
            }

            return displayTexture;
        }

        public void Dispose()
        {
            if (displayTexture != null)
                UnityEngine.Object.DestroyImmediate(displayTexture);
            renderer.Cleanup();
        }
    }
}
