using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// <see cref="WindowRainLayer"/>가 붙은 게임 카메라에만 빗물 합성을 넣는다.
///
/// 2D 렌더러에서 AfterRenderingPostProcessing은 후처리가 끝난 최종 장면 색을 읽을 수 있고,
/// Pixel Perfect 업스케일과 최종 블릿보다 앞이며 Screen Space Overlay UI보다도 먼저다.
/// 그래서 Camera Sorting Layer Texture에 들어가지 않는 레이어까지 포함한 장면 전체가 굴절되고,
/// 대사·선택지·메뉴 UI는 빗물 아래로 가려지지 않는다.
/// </summary>
public sealed class WindowRainFeature : ScriptableRendererFeature
{
    WindowRainPass pass;

    public override void Create()
    {
        pass = new WindowRainPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        if (!WindowRainLayer.TryGet(renderingData.cameraData.camera, out var layer) || !layer.HasVisibleWater) return;
        pass.layer = layer;
        renderer.EnqueuePass(pass);
    }

    sealed class WindowRainPass : ScriptableRenderPass
    {
        internal WindowRainLayer layer;

        public WindowRainPass()
        {
            profilingSampler = new ProfilingSampler("Window Rain");
            // 줌/옵션에서 PPC나 후처리를 꺼도 읽을 장면 색을 확보한다.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var material = layer != null ? layer.CompositeMaterial : null;
            if (material == null || resources.isActiveTargetBackBuffer) return;
            TextureHandle source = resources.activeColorTexture;
            var description = renderGraph.GetTextureDesc(source);
            description.name = "_WindowRainColor";
            description.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(description);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Window Rain");
            resources.cameraColor = destination;
        }
    }
}
