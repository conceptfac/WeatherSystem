#if UNITY_EDITOR
using ConceptFactory.Shaders;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Editor.Shaders
{
    [CustomEditor(typeof(SkyboxBlender))]
    public class SkyboxBlenderEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shaders/Resources/SkyboxBlenderEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shaders/Resources/SkyboxBlenderEditor.uss";
        private const string SkyboxPropertyName = "m_skyBox";
        private const string ExpectedShaderGraphPath =
            "Packages/com.twinny.twe26/Runtime/Core/Scripts/Shaders/SkyboxBlender/SG_SkyboxBlender.shadergraph";
        private const string ExpectedShaderNameHint = "SG_SkyboxBlender";

        private Shader _expectedShader;
        private VisualTreeAsset _visualTree;
        private StyleSheet _styleSheet;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            LoadExpectedShader();
            LoadAssets();

            var root = _visualTree != null ? _visualTree.CloneTree() : new VisualElement();
            if (_styleSheet != null)
                root.styleSheets.Add(_styleSheet);

            SerializedProperty skyboxProp = serializedObject.FindProperty(SkyboxPropertyName);
            if (skyboxProp == null)
            {
                root.Add(new HelpBox(
                    "[SkyboxBlenderEditor] Could not find serialized property m_skyBox.",
                    HelpBoxMessageType.Error
                ));
                return root;
            }

            VisualElement fieldsRoot = root.Q<VisualElement>("fieldsRoot") ?? root;
            var skyboxField = new PropertyField(skyboxProp, "Skybox Material");
            skyboxField.Bind(serializedObject);
            fieldsRoot.Add(skyboxField);

            var warning = new HelpBox(
                "Skybox Blender will not work with this material. Assign a material using SG_SkyboxBlender shader.",
                HelpBoxMessageType.Warning
            );
            warning.name = "shaderWarning";
            fieldsRoot.Add(warning);

            void RefreshWarning()
            {
                Material material = skyboxProp.objectReferenceValue as Material;
                bool showWarning = material != null && !IsCompatibleSkyboxMaterial(material);
                warning.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;
            }

            root.TrackPropertyValue(skyboxProp, _ => RefreshWarning());
            RefreshWarning();

            return root;
        }

        private void LoadAssets()
        {
            if (_visualTree == null)
                _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (_styleSheet == null)
                _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        }

        private void LoadExpectedShader()
        {
            if (_expectedShader != null) return;
            _expectedShader = AssetDatabase.LoadAssetAtPath<Shader>(ExpectedShaderGraphPath);
        }

        private bool IsCompatibleSkyboxMaterial(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            if (_expectedShader != null && material.shader == _expectedShader)
                return true;

            return material.shader.name.Contains(ExpectedShaderNameHint);
        }
    }
}
#endif
