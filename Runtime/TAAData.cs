using UnityEngine;
using UnityEngine.Rendering.Universal;
using System;
namespace Naiwen.TAA
{

    internal enum AntialiasingQuality
    {
        Low,
        Medium,
        High
    }

    internal sealed class TAAData 
    {
        #region Fields
        internal Vector2 sampleOffset;
        internal Matrix4x4 projOverride;
        internal Matrix4x4 porjPreview;
        internal Matrix4x4 viewPreview;
        #endregion
        #region Constructors
        internal TAAData()
        {
            projOverride = Matrix4x4.identity;
            porjPreview = Matrix4x4.identity;
            viewPreview = Matrix4x4.identity;
        }
        #endregion

        

    }
}
