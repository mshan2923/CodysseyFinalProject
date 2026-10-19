using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class OutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [SerializeField] private RenderingLayerMask layerMask;
    [SerializeField] private Color color = new(1f, 0.6f, 0f, 1f);
    [SerializeField, Range(1f, 8f)] private float thickness = 2f;

    private Material material;
    private OutlinePass pass;

    public override void Create()
    {
        if (shader == null) return;
        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new OutlinePass(material)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (pass == null || layerMask.value == 0) return;

        CameraType type = data.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection) return;

        pass.Setup(layerMask.value, color, thickness);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId[] Tags =
        {
            new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit")
        };
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

        private readonly Material material;
        private uint layerMask;

        public OutlinePass(Material material) => this.material = material;

        public void Setup(uint mask, Color color, float thickness)
        {
            layerMask = mask;
            material.SetColor(ColorId, color);
            material.SetFloat(ThicknessId, thickness);
        }

        private class MaskData { public RendererListHandle List; }
        private class CompositeData { public TextureHandle Mask; public Material Mat; }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var resources = frame.Get<UniversalResourceData>();
            var camera = frame.Get<UniversalCameraData>();
            var rendering = frame.Get<UniversalRenderingData>();

            RenderTextureDescriptor desc = camera.cameraTargetDescriptor;
            desc.msaaSamples = 1;

            desc.colorFormat = RenderTextureFormat.R8;
            desc.depthBufferBits = 0;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                graph, desc, "_OutlineMask", true);

            desc.colorFormat = RenderTextureFormat.Depth;
            desc.depthBufferBits = 32;
            TextureHandle depth = UniversalRenderer.CreateRenderGraphTexture(
                graph, desc, "_OutlineMaskDepth", true);

            // 1) 대상 렌더러만 마스크에 그림
            using (var builder = graph.AddRasterRenderPass<MaskData>("Outline Mask", out var data))
            {
                var listDesc = new RendererListDesc(Tags, rendering.cullResults, camera.camera)
                {
                    renderQueueRange = RenderQueueRange.all,
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    layerMask = -1,
                    renderingLayerMask = layerMask,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = 0
                };
                data.List = graph.CreateRendererList(listDesc);

                builder.UseRendererList(data.List);
                builder.SetRenderAttachment(mask, 0);
                builder.SetRenderAttachmentDepth(depth);
                builder.SetRenderFunc(static (MaskData d, RasterGraphContext ctx) =>
                    ctx.cmd.DrawRendererList(d.List));
            }

            // 2) 팽창 - 원본 = 외곽선만 카메라 컬러 위에 블렌딩
            using (var builder = graph.AddRasterRenderPass<CompositeData>("Outline Composite", out var data))
            {
                data.Mask = mask;
                data.Mat = material;

                builder.UseTexture(mask);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.Mask, new Vector4(1, 1, 0, 0), d.Mat, 1));
            }
        }
    }
}