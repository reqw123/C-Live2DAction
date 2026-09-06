using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.VFX.Rendering
{
    // 2026-09-06, user request: the URP Full Screen Pass that draws the "Boss 支配領域" screen-edge
    // effect for the yuanpei_LogoSky fight. Added ONCE to Live2DAction_Renderer.asset (the only
    // pipeline-wide change, done by BossDomainScreenVFXSetup). Lives in the Live2DAction.Runtime
    // assembly (not Assembly-CSharp) so BossDomainScreenVFX.cs can call SetMaterial().
    //
    // This feature owns NO resources and NO serialized material. It borrows a runtime material that
    // a live BossDomainScreenVFX controller registers via SetMaterial(); the rest of the time
    // s_Material is null and AddRenderPasses returns immediately - a single null check per camera
    // per frame, nothing enqueued, no blit, no GC (§7 "Boss戰外完全停用Pass").
    //
    // Camera rules (§6): Game cameras only (no Scene view, no preview/reflection), Base cameras
    // only (never an Overlay in a stack - the project has none today, guard is cheap). HUD is
    // Screen-Space-Overlay so it always renders after the whole pipeline, on top of this.
    //
    // The blit copies active-colour -> temp, then a fullscreen-triangle raster pass runs the
    // material and writes back over the camera colour - the same pattern URP's own
    // FullScreenPassRendererFeature uses, so it is RenderGraph-native for this URP version.
    public class BossDomainScreenVFXRendererFeature : ScriptableRendererFeature
    {
        static Material s_Material;

        /// <summary>Registered by a live BossDomainScreenVFX controller. Null = feature is inert.</summary>
        public static void SetMaterial(Material material) => s_Material = material;

        /// <summary>Cleared by the controller on EndDomain / OnDestroy (only if it still owns the slot).</summary>
        public static void ClearMaterial(Material material)
        {
            if (s_Material == material) s_Material = null;
        }

        [Tooltip("BeforeRenderingPostProcessing lets the green emission feed Bloom if the project ever adds one.")]
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        DomainPass m_Pass;

        public override void Create()
        {
            m_Pass = new DomainPass { renderPassEvent = injectionPoint };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (s_Material == null) return;

            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game) return;          // no Scene view / preview / reflection
            if (cameraData.renderType == CameraRenderType.Overlay) return; // main 3D camera only

            renderer.EnqueuePass(m_Pass);
        }

        class DomainPass : ScriptableRenderPass
        {
            static readonly int s_BlitTexture = Shader.PropertyToID("_BlitTexture");
            static readonly int s_BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();
            static readonly ProfilingSampler s_Sampler = new ProfilingSampler("BossDomainScreenVFX");

            class CopyData { internal TextureHandle source; }
            class MainData { internal TextureHandle source; internal Material material; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var material = s_Material;
                if (material == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                TextureHandle activeColor = resourceData.activeColorTexture;
                if (!activeColor.IsValid()) return;

                // 1. copy the current camera colour into a temp texture we can read from
                var desc = renderGraph.GetTextureDesc(activeColor);
                desc.name = "_BossDomainScreenVFX_Source";
                desc.clearBuffer = false;
                desc.msaaSamples = MSAASamples.None;
                desc.depthBufferBits = DepthBits.None;
                TextureHandle copy = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<CopyData>("BossDomainScreenVFX Copy", out var passData, s_Sampler))
                {
                    passData.source = activeColor;
                    builder.UseTexture(activeColor, AccessFlags.Read);
                    builder.SetRenderAttachment(copy, 0, AccessFlags.Write);
                    builder.SetRenderFunc((CopyData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                    });
                }

                // 2. run the domain material full-screen, writing back over the camera colour
                using (var builder = renderGraph.AddRasterRenderPass<MainData>("BossDomainScreenVFX", out var passData, s_Sampler))
                {
                    passData.source = copy;
                    passData.material = material;
                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((MainData data, RasterGraphContext ctx) =>
                    {
                        s_Mpb.Clear();
                        s_Mpb.SetTexture(s_BlitTexture, data.source);
                        s_Mpb.SetVector(s_BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_Mpb);
                    });
                }
            }
        }
    }
}
