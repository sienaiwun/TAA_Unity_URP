using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Naiwen.TAA
{
    internal static class ShaderKeywordStrings
    {
        internal static readonly string HighTAAQuality = "_HIGH_TAA";
        internal static readonly string MiddleTAAQuality = "_MIDDLE_TAA";
        internal static readonly string LOWTAAQuality = "_LOW_TAA";
    }

    internal static class ShaderConstants
    {
         public static readonly int _TAA_Params = Shader.PropertyToID("_TAA_Params");
        public static readonly int _TAA_pre_texture = Shader.PropertyToID("_TAA_Pretexture");
        public static readonly int _TAA_pre_vp = Shader.PropertyToID("_TAA_Pretexture");
        public static readonly int _TAA_PrevViewProjM = Shader.PropertyToID("_PrevViewProjM_TAA");
        public static readonly int _TAA_CurInvView = Shader.PropertyToID("_I_V_Current_jittered");
        public static readonly int _TAA_CurInvProj = Shader.PropertyToID("_I_P_Current_jittered");
    }

    internal class TAAPass : ScriptableRenderPass
    {
        #region Field
        const string TaaShader = "Shaders/Naiwen/TAA.shader";
        RenderTexture[] historyBuffer;
        static int indexWrite = 0;
        TAAData m_TaaData;
        TemporalAntiAliasing m_taa;
        Material m_Material;
        ProfilingSampler m_ProfilingSampler;
        string m_ProfilerTag = "TAA Pass";
        #endregion

        internal TAAPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        internal void Setup(TAAData TaaData,TemporalAntiAliasing Taa)
        {
            // Set data
            m_TaaData = TaaData;
            m_taa = Taa;
            m_Material = new Material(Shader.Find(TaaShader));
        }

        void ClearRT(ref RenderTexture rt)
        {
            if(rt!= null)
            {
                RenderTexture.ReleaseTemporary(rt);
                rt = null;
            }
        }

        internal void Clear()
        {
            if(historyBuffer!=null)
            {
                ClearRT(ref historyBuffer[0]);
                ClearRT(ref historyBuffer[1]);
                historyBuffer = null;
            }
        }

        void EnsureArray<T>(ref T[] array, int size, T initialValue = default(T))
        {
            if (array == null || array.Length != size)
            {
                array = new T[size];
                for (int i = 0; i != size; i++)
                    array[i] = initialValue;
            }
        }

        bool EnsureRenderTarget(ref RenderTexture rt, int width, int height, RenderTextureFormat format, FilterMode filterMode, int depthBits = 0, int antiAliasing = 1)
        {
            if (rt != null && (rt.width != width || rt.height != height || rt.format != format || rt.filterMode != filterMode || rt.antiAliasing != antiAliasing))
            {
                RenderTexture.ReleaseTemporary(rt);
                rt = null;
            }
            if (rt == null)
            {
                rt = RenderTexture.GetTemporary(width, height, depthBits, format, RenderTextureReadWrite.Default, antiAliasing);
                rt.filterMode = filterMode;
                rt.wrapMode = TextureWrapMode.Clamp;
                return true;// new target
            }
            return false;// same target
        }

        void DoTemporalAntiAliasing(CameraData cameraData, CommandBuffer cmd)
        {
            var camera = cameraData.camera;

            // Never draw in Preview
            if (camera.cameraType == CameraType.Preview)
                return;
            var colorTextureIdentifier = new RenderTargetIdentifier("_CameraColorTexture");
            var descriptor = new RenderTextureDescriptor(camera.scaledPixelWidth, camera.scaledPixelHeight, RenderTextureFormat.DefaultHDR, 16);
            EnsureArray(ref historyBuffer, 2);
            EnsureRenderTarget(ref historyBuffer[0], descriptor.width, descriptor.height, descriptor.colorFormat, FilterMode.Bilinear);
            EnsureRenderTarget(ref historyBuffer[1], descriptor.width, descriptor.height, descriptor.colorFormat, FilterMode.Bilinear);

            int indexRead = indexWrite;
            indexWrite = (++indexWrite) % 2;
            
            Matrix4x4 inv_p_jitterd = Matrix4x4.Inverse(m_TaaData.projOverride);
            Matrix4x4 inv_v_jitterd = Matrix4x4.Inverse(camera.worldToCameraMatrix);
            Matrix4x4 previous_vp = m_TaaData.porjPreview * m_TaaData.viewPreview;
            m_Material.SetMatrix(ShaderConstants._TAA_CurInvView, inv_v_jitterd);
            m_Material.SetMatrix(ShaderConstants._TAA_CurInvProj, inv_p_jitterd);
            m_Material.SetMatrix(ShaderConstants._TAA_PrevViewProjM, previous_vp);
            m_Material.SetVector(ShaderConstants._TAA_Params, new Vector3(m_TaaData.sampleOffset.x, m_TaaData.sampleOffset.y, m_taa.feedback.value));
            m_Material.SetTexture(ShaderConstants._TAA_pre_texture, historyBuffer[indexRead]);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.HighTAAQuality, m_taa.quality.value == MotionBlurQuality.High);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MiddleTAAQuality, m_taa.quality.value == MotionBlurQuality.Medium);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.LOWTAAQuality, m_taa.quality.value == MotionBlurQuality.Low);
            cmd.Blit(colorTextureIdentifier, historyBuffer[indexWrite], m_Material);
            cmd.Blit(historyBuffer[indexWrite], colorTextureIdentifier);

        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                DoTemporalAntiAliasing(renderingData.cameraData, cmd);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }
}
