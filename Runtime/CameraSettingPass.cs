using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Naiwen.TAA
{
    internal class CameraSettingPass : ScriptableRenderPass
    {
        ProfilingSampler m_ProfilingSampler;
        string m_ProfilerTag = "SetCamera";
        TAAData m_TaaData;
        internal CameraSettingPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        internal void Setup(TAAData data)
        {
            m_TaaData = data;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CameraData cameraData = renderingData.cameraData;
                cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, m_TaaData.projOverride);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
