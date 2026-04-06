using System.Collections.Generic;
using ConceptFactory.Weather.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using WeatherVolumetricClouds = global::VolumetricClouds;

namespace ConceptFactory.Weather.Editor
{
    [CustomEditor(typeof(NimbusCloudController))]
    public sealed class NimbusCloudControllerEditor : UnityEditor.Editor
    {
        private const string UxmlPathPackageId = "Packages/com.conceptfactory.weather/Editor/UI/NimbusCloudControllerEditor.uxml";
        private const string UxmlPathFolder = "Packages/WeatherSystem/Editor/UI/NimbusCloudControllerEditor.uxml";

        private const string DialogTitle = "Nimbus — Volumetric Volume";
        private const string DialogMessage =
            "Volumetric mode requires a Volume component (explicit reference or on the Main Camera).\n\n" +
            "Without a Volume, Volumetric Clouds cannot be applied.\n\n" +
            "Add a Volume on the Main Camera now (with profile and Volumetric Clouds enabled)?";

        private const string DialogNoMainCamera =
            "Volumetric mode needs a camera tagged MainCamera to create a Volume automatically.\n\n" +
            "Assign a Main Camera or drag a Volume into the Nimbus Volume field.";

        private static readonly HashSet<int> s_VolumetricMissingVolumePrompted = new HashSet<int>();

        private NimbusCloudRenderMode _lastRenderMode;
        private VisualElement _volumeCard;
        private VisualElement _volumetricCloudsCard;
        private VisualElement _volumetricCloudsOverrideContainer;
        private VisualElement _windCard;
        private UnityEditor.Editor _volumetricCloudsEmbeddedEditor;
        private VisualElement _warningBox;
        private Label _warningLabel;
        private Button _createButton;

        private void ReapplyVolumetricCloudsAndRepaint()
        {
            if (target is NimbusCloudController nimbus)
                nimbus.ReapplyVolumetricClouds();
            if (!Application.isPlaying)
                SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            if (target == null)
                return;
            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            if (modeProp != null)
                _lastRenderMode = (NimbusCloudRenderMode)modeProp.enumValueIndex;
        }

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualTreeAsset visualTree = LoadNimbusUxml();
            VisualElement root = visualTree != null ? visualTree.CloneTree() : new VisualElement();

            BindFields(root);
            CacheWarningElements(root);
            HookButtons();

            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            if (modeProp != null)
                _lastRenderMode = (NimbusCloudRenderMode)modeProp.enumValueIndex;

            RefreshVolumeWarning();

            EditorApplication.delayCall += InitialVolumetricEnsureIfNeeded;

            root.TrackSerializedObjectValue(serializedObject, _ => OnSerializedObjectTracked());
            return root;
        }

        private void OnDisable()
        {
            ClearVolumetricCloudsEmbeddedEditor();
        }

        private void ClearVolumetricCloudsEmbeddedEditor()
        {
            if (_volumetricCloudsEmbeddedEditor != null)
            {
                DestroyImmediate(_volumetricCloudsEmbeddedEditor);
                _volumetricCloudsEmbeddedEditor = null;
            }
        }

        private static bool TryGetResolvedVolume(NimbusCloudController nimbus, out Volume vol)
        {
            vol = nimbus.VolumeReference;
            if (vol != null)
                return true;
            Camera main = NimbusCloudController.GetMainCameraForNimbus();
            if (main == null)
                return false;
            vol = main.GetComponent<Volume>();
            return vol != null;
        }

        private void RefreshVolumetricCloudsOverrideInspector()
        {
            if (_volumetricCloudsOverrideContainer == null || _volumetricCloudsCard == null)
                return;

            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            var mode = modeProp != null
                ? (NimbusCloudRenderMode)modeProp.enumValueIndex
                : NimbusCloudRenderMode.CloudPlane;
            bool volumetric = mode == NimbusCloudRenderMode.VolumetricClouds;

            if (!volumetric)
            {
                _volumetricCloudsCard.style.display = DisplayStyle.None;
                ClearVolumetricCloudsEmbeddedEditor();
                _volumetricCloudsOverrideContainer.Clear();
                return;
            }

            _volumetricCloudsCard.style.display = DisplayStyle.Flex;
            ClearVolumetricCloudsEmbeddedEditor();
            _volumetricCloudsOverrideContainer.Clear();

            if (target is not NimbusCloudController nimbus)
                return;

            if (!TryGetResolvedVolume(nimbus, out Volume vol))
            {
                var lbl = new Label("No Volume yet. Assign one or add a Volume on the Main Camera.");
                lbl.AddToClassList("inline-note");
                _volumetricCloudsOverrideContainer.Add(lbl);
                return;
            }

            nimbus.ReapplyVolumetricClouds();

            VolumeProfile profile = vol.profile;
            if (profile == null)
            {
                var lbl = new Label("Volume has no profile.");
                lbl.AddToClassList("inline-note");
                _volumetricCloudsOverrideContainer.Add(lbl);
                return;
            }

            if (!profile.TryGet(out WeatherVolumetricClouds clouds))
            {
                var lbl = new Label("Volume profile has no Volumetric Clouds override.");
                lbl.AddToClassList("inline-note");
                _volumetricCloudsOverrideContainer.Add(lbl);
                return;
            }

            // Unity fake-null or destroyed component would throw SerializedObjectNotCreatableException in VolumetricCloudsEditor.OnEnable.
            if (!clouds)
            {
                var lbl = new Label("Volumetric Clouds override is invalid. Re-open the inspector or re-add the override on the Volume profile.");
                lbl.AddToClassList("inline-note");
                _volumetricCloudsOverrideContainer.Add(lbl);
                return;
            }

            // VolumetricClouds uses VolumeComponentEditor (IMGUI + SerializedDataParameter). InspectorElement
            // does not run that pipeline, so edits would not persist / would not match the real Volume inspector.
            try
            {
                _volumetricCloudsEmbeddedEditor = UnityEditor.Editor.CreateEditor(clouds);
            }
            catch (System.Exception ex)
            {
                var lbl = new Label($"Could not open embedded Volumetric Clouds inspector: {ex.Message}");
                lbl.AddToClassList("inline-note");
                _volumetricCloudsOverrideContainer.Add(lbl);
                return;
            }

            var imgui = new IMGUIContainer(() =>
            {
                if (_volumetricCloudsEmbeddedEditor == null)
                    return;

                UnityEditor.Editor ed = _volumetricCloudsEmbeddedEditor;
                ed.serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                ed.OnInspectorGUI();
                bool changed = EditorGUI.EndChangeCheck();
                bool applied = ed.serializedObject.ApplyModifiedProperties();
                if (changed || applied)
                {
                    EditorUtility.SetDirty(clouds);
                    EditorUtility.SetDirty(profile);
                    EditorUtility.SetDirty(vol);
                    if (!Application.isPlaying && vol.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
                }
            });
            _volumetricCloudsOverrideContainer.Add(imgui);
        }

        private void OnSerializedObjectTracked()
        {
            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            if (modeProp == null)
            {
                RefreshVolumeWarning();
                return;
            }

            var current = (NimbusCloudRenderMode)modeProp.enumValueIndex;
            bool switchedToVolumetric =
                current == NimbusCloudRenderMode.VolumetricClouds &&
                _lastRenderMode != NimbusCloudRenderMode.VolumetricClouds;

            if (switchedToVolumetric)
            {
                EditorApplication.delayCall += () =>
                {
                    if (target == null)
                        return;
                    TryEnsureVolumetricVolumeAndClouds(promptIfMissingVolume: true);
                    RefreshVolumeWarning();
                };
            }

            bool switchedToCloudPlane =
                current == NimbusCloudRenderMode.CloudPlane &&
                _lastRenderMode == NimbusCloudRenderMode.VolumetricClouds;

            if (switchedToCloudPlane)
            {
                EditorApplication.delayCall += () =>
                {
                    if (target == null)
                        return;
                    serializedObject.Update();
                    TryDeactivateVolumetricCloudsOverrideIfPresent();
                    ReapplyVolumetricCloudsAndRepaint();
                    RefreshVolumeWarning();
                };
            }

            _lastRenderMode = current;
            RefreshVolumeWarning();

            if (current == NimbusCloudRenderMode.VolumetricClouds)
            {
                EditorApplication.delayCall += () =>
                {
                    ReapplyVolumetricCloudsAndRepaint();
                };
            }
        }

        /// <summary>
        /// On first inspector open in volumetric mode with no Volume: prompt once per instance per editor session.
        /// </summary>
        private void InitialVolumetricEnsureIfNeeded()
        {
            if (target == null)
                return;
            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            if (modeProp == null)
                return;
            if ((NimbusCloudRenderMode)modeProp.enumValueIndex != NimbusCloudRenderMode.VolumetricClouds)
                return;
            var nimbus = (NimbusCloudController)target;
            if (HasResolvedVolume(nimbus))
                return;
            int id = nimbus.GetInstanceID();
            if (s_VolumetricMissingVolumePrompted.Contains(id))
                return;
            s_VolumetricMissingVolumePrompted.Add(id);
            TryEnsureVolumetricVolumeAndClouds(promptIfMissingVolume: true);
            ReapplyVolumetricCloudsAndRepaint();
            RefreshVolumeWarning();
        }

        private void BindVolumeField(VisualElement volumeFields)
        {
            if (volumeFields == null)
                return;

            volumeFields.Clear();

            SerializedProperty volumeProp = serializedObject.FindProperty("_volume");
            if (volumeProp == null)
                return;

            var root = new VisualElement();
            root.AddToClassList("cfw-nimbus-volume-field");

            var labelRow = new VisualElement();
            labelRow.AddToClassList("cfw-nimbus-volume-label-row");

            var title = new Label("Volume");
            title.AddToClassList("cfw-nimbus-volume-title");

            var optional = new Label("(optional)");
            optional.AddToClassList("cfw-nimbus-volume-optional");

            labelRow.Add(title);
            labelRow.Add(optional);
            root.Add(labelRow);

            var field = new PropertyField(volumeProp);
            field.label = string.Empty;
            field.Bind(serializedObject);
            root.Add(field);

            var hint = new Label("When empty, resolves the Volume on the Main Camera (MainCamera tag).");
            hint.AddToClassList("cfw-nimbus-volume-hint");
            root.Add(hint);

            volumeFields.Add(root);
        }

        private static VisualTreeAsset LoadNimbusUxml()
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPathPackageId);
            if (asset != null)
                return asset;
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPathFolder);
        }

        private void BindFields(VisualElement root)
        {
            BindVolumeField(root.Q<VisualElement>("volumeFields"));
            BindDriveFields(root.Q<VisualElement>("driveFields"));
            Populate(
                root.Q<VisualElement>("windFields"),
                "_syncDensityWindFromSolarCurves",
                "_globalWindSpeedKmhOverDay",
                "_globalWindOrientationOverDay");
        }

        private void BindDriveFields(VisualElement driveFields)
        {
            if (driveFields == null)
                return;

            driveFields.Clear();

            SerializedProperty renderModeProp = serializedObject.FindProperty("_renderMode");
            if (renderModeProp != null)
            {
                PropertyField renderModeField = new PropertyField(renderModeProp);
                renderModeField.Bind(serializedObject);
                renderModeField.RegisterValueChangeCallback(_ =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (target == null)
                            return;
                        serializedObject.Update();
                        serializedObject.ApplyModifiedProperties();

                        SerializedProperty p = serializedObject.FindProperty("_renderMode");
                        if (p == null)
                            return;

                        var mode = (NimbusCloudRenderMode)p.enumValueIndex;
                        if (mode == NimbusCloudRenderMode.VolumetricClouds)
                            TryEnsureVolumetricVolumeAndClouds(promptIfMissingVolume: false);
                        else if (mode == NimbusCloudRenderMode.CloudPlane)
                            TryDeactivateVolumetricCloudsOverrideIfPresent();

                        ReapplyVolumetricCloudsAndRepaint();
                        RefreshVolumeWarning();
                    };
                });
                driveFields.Add(renderModeField);
            }

        }

        private void CacheWarningElements(VisualElement root)
        {
            _volumeCard = root.Q<VisualElement>("volumeCard");
            _volumetricCloudsCard = root.Q<VisualElement>("volumetricCloudsCard");
            _volumetricCloudsOverrideContainer = root.Q<VisualElement>("volumetricCloudsOverrideContainer");
            _windCard = root.Q<VisualElement>("windCard");
            _warningBox = root.Q<VisualElement>("volumeWarningBox");
            _warningLabel = root.Q<Label>("volumeWarningLabel");
            _createButton = root.Q<Button>("createMainCameraVolumeButton");
        }

        private void HookButtons()
        {
            if (_createButton != null)
                _createButton.clicked += () =>
                {
                    TryEnsureVolumetricVolumeAndClouds(promptIfMissingVolume: false);
                    ReapplyVolumetricCloudsAndRepaint();
                    RefreshVolumeWarning();
                };
        }

        private void RefreshVolumeWarning()
        {
            serializedObject.Update();
            var nimbus = (NimbusCloudController)target;
            if (nimbus == null)
                return;

            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            var mode = modeProp != null
                ? (NimbusCloudRenderMode)modeProp.enumValueIndex
                : NimbusCloudRenderMode.CloudPlane;

            _lastRenderMode = mode;

            bool volumetric = mode == NimbusCloudRenderMode.VolumetricClouds;

            if (_volumeCard != null)
                _volumeCard.style.display = volumetric ? DisplayStyle.Flex : DisplayStyle.None;

            if (_windCard != null)
                _windCard.style.display = volumetric ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshVolumetricCloudsOverrideInspector();

            bool hasVolume = HasResolvedVolume(nimbus);
            bool showWarning = volumetric && !hasVolume;

            if (_warningBox != null)
                _warningBox.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;

            if (!showWarning)
                return;

            Camera main = NimbusCloudController.GetMainCameraForNimbus();
            if (_warningLabel != null)
            {
                _warningLabel.text = main == null
                    ? "Volumetric mode: no Main Camera (MainCamera tag). Assign a Volume manually or set a Main Camera."
                    : "Volumetric mode: no Volume (neither explicit reference nor on Main Camera).";
            }
        }

        private static bool HasResolvedVolume(NimbusCloudController nimbus)
        {
            return TryGetResolvedVolume(nimbus, out _);
        }

        /// <summary>
        /// When switching to Cloud Plane, turns off the Volumetric Clouds Volume override if it exists and is active (does not remove the override).
        /// </summary>
        private void TryDeactivateVolumetricCloudsOverrideIfPresent()
        {
            foreach (UnityEngine.Object obj in serializedObject.targetObjects)
            {
                if (obj is not NimbusCloudController nimbus)
                    continue;

                if (!TryGetResolvedVolume(nimbus, out Volume vol))
                    continue;

                VolumeProfile profile = vol.profile;
                if (profile == null)
                    continue;

                if (!profile.TryGet(out WeatherVolumetricClouds clouds))
                    continue;

                if (!clouds.active)
                    continue;

                if (!Application.isPlaying)
                    Undo.RecordObject(profile, "Nimbus — Volumetric Clouds off (Cloud Plane)");

                clouds.active = false;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(vol);
                if (!Application.isPlaying && vol.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
            }
        }

        /// <summary>
        /// Ensures Volume (explicit or Main Camera), profile, and active Volumetric Clouds override.
        /// </summary>
        private void TryEnsureVolumetricVolumeAndClouds(bool promptIfMissingVolume)
        {
            serializedObject.Update();
            SerializedProperty modeProp = serializedObject.FindProperty("_renderMode");
            if (modeProp == null)
                return;
            if ((NimbusCloudRenderMode)modeProp.enumValueIndex != NimbusCloudRenderMode.VolumetricClouds)
                return;

            var nimbus = (NimbusCloudController)target;
            if (nimbus == null)
                return;

            if (Application.isPlaying)
            {
                serializedObject.ApplyModifiedProperties();
                ReapplyVolumetricCloudsAndRepaint();
                return;
            }

            Volume vol = nimbus.VolumeReference;
            if (vol != null)
            {
                EnsureVolumeProfileWithVolumetricClouds(vol);
                ReapplyVolumetricCloudsAndRepaint();
                return;
            }

            Camera main = NimbusCloudController.GetMainCameraForNimbus();
            if (main == null)
            {
                if (promptIfMissingVolume)
                    EditorUtility.DisplayDialog(DialogTitle, DialogNoMainCamera, "OK");
                return;
            }

            vol = main.GetComponent<Volume>();
            if (vol == null)
            {
                if (promptIfMissingVolume && !EditorUtility.DisplayDialog(DialogTitle, DialogMessage, "Yes", "Cancel"))
                    return;

                Undo.AddComponent<Volume>(main.gameObject);
                vol = main.GetComponent<Volume>();
            }

            EnsureVolumeProfileWithVolumetricClouds(vol);
            EditorUtility.SetDirty(main.gameObject);
            EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
            ReapplyVolumetricCloudsAndRepaint();
        }

        private static void EnsureVolumeProfileWithVolumetricClouds(Volume volume)
        {
            if (volume == null)
                return;

            Undo.RecordObject(volume, "Nimbus — Volume profile");

            VolumeProfile profile = volume.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Undo.RegisterCreatedObjectUndo(profile, "Nimbus — Volume Profile");
                volume.profile = profile;
            }

            Undo.RecordObject(profile, "Nimbus — Volumetric Clouds override");

            if (!profile.TryGet(out WeatherVolumetricClouds clouds))
                clouds = profile.Add<WeatherVolumetricClouds>(true);

            clouds.active = true;
            clouds.state.overrideState = true;
            clouds.state.value = true;

            volume.enabled = true;
            if (volume.weight <= 0f)
                volume.weight = 1f;

            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(profile);

            GameObject go = volume.gameObject;
            if (go != null && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private void Populate(VisualElement container, params string[] propertyNames)
        {
            if (container == null)
                return;

            container.Clear();
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null)
                    continue;

                PropertyField field = new PropertyField(property);
                field.Bind(serializedObject);
                if (property.propertyType == SerializedPropertyType.AnimationCurve)
                    field.AddToClassList("cfw-curve-field");
                container.Add(field);
            }
        }
    }
}
