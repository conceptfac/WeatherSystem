using UnityEngine;

namespace ConceptFactory.Weather.Atmosphere
{
    /// <summary>
    /// Active cloud rendering path for <see cref="NimbusCloudController"/> (cloud plane vs URP volumetric Volume).
    /// </summary>
    public enum NimbusCloudRenderMode
    {
        [Tooltip("Cloud plane path: does not drive the volumetric Volume override (meshes / plane shader).")]
        CloudPlane = 0,

        [Tooltip("URP volumetric clouds: Volume + Volumetric Clouds override and density/wind curves.")]
        VolumetricClouds = 1
    }
}
