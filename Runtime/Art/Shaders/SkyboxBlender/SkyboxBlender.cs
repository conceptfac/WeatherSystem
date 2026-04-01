using UnityEngine;


namespace ConceptFactory.Shaders
{


    public class SkyboxBlender : MonoBehaviour
    {
        [SerializeField] private Material m_skyBox;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            SetHDRI(m_skyBox);
        }


        public static void SetHDRI(Material hdri)
        {
            if (hdri == null) return;

            if (RenderSettings.skybox != hdri)
            {
                RenderSettings.skybox = hdri;
                DynamicGI.UpdateEnvironment();
            }
        }


    }

}