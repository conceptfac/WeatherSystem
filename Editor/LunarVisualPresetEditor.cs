using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LunarVisualPreset))]
    public sealed class LunarVisualPresetEditor : UnityEditor.Editor
    {
        private const string DefaultPresetsFolder = "Packages/com.conceptfactory.weather/Runtime/Scripts/Celestial/Presets/";

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();

            VisualElement root = new VisualElement();
            AddSection(root, "Lighting", "lightColorOverNight", "lightIntensityOverNight", "baseIntensity", "disableLightBelowHorizon", "horizonDisableThreshold", "daylightFadeStrength", "minimumPhaseLight");
            AddSection(root, "Daylight Fades", "daylightShadowFadeStart", "daylightShadowFadeRange", "daylightLitMoonFadeStart", "daylightLitMoonFadeRange", "duskLitMoonSuppression", "minimumLitMoonSkyVisibility");
            AddSection(root, "Sky Disk", "skyboxMoonSize", "moonTextureExposure", "darkMoonTextureExposure");
            AddSection(root, "Halo Layers", "moonHaloColor", "moonHaloIntensity", "moonHaloInnerSize", "moonHaloOuterSize", "moonHaloTerminator", "borderHaloColor", "borderHaloIntensity", "borderHaloInnerSize", "borderHaloOuterSize", "borderHaloTerminator");
            AddSection(root, "Phase Look", "darkSideVisibility", "darkMoonExposure", "moonHaloIntensityMultiplier", "borderHaloIntensityMultiplier");
            AddHorizonSection(root);

            return root;
        }

        private void AddSection(VisualElement root, string title, params string[] propertyNames)
        {
            Foldout foldout = new Foldout { text = title, value = true };
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null)
                {
                    continue;
                }

                PropertyField field = new PropertyField(property);
                field.Bind(serializedObject);
                foldout.Add(field);
            }

            root.Add(foldout);
        }

        private void AddHorizonSection(VisualElement root)
        {
            Foldout foldout = new Foldout { text = "Horizon Illusion", value = true };
            AddField(foldout, "enableHorizonMoonIllusion");
            AddField(foldout, "horizonIllusionMaxElevation");
            AddField(foldout, "moonRiseSizeCurve");
            AddField(foldout, "horizonMoonFalloffMultiplier");
            AddField(foldout, "horizonMoonWarmTintStrength");
            AddField(foldout, "horizonMoonTintColor");
            AddField(foldout, "moonRiseWarmTintBoost");

            SerializedProperty applyHorizonIllusion = serializedObject.FindProperty("applyHorizonIllusion");
            PropertyField applyField = new PropertyField(applyHorizonIllusion, "Apply Horizon Illusion");
            applyField.Bind(serializedObject);
            foldout.Add(applyField);

            VisualElement phaseHorizonFields = new VisualElement();
            phaseHorizonFields.SetEnabled(applyHorizonIllusion != null && applyHorizonIllusion.boolValue);
            AddField(phaseHorizonFields, "horizonMoonIntensityMultiplier");
            AddField(phaseHorizonFields, "horizonWarmTintStrengthMultiplier");
            AddField(phaseHorizonFields, "horizonTintColor");
            foldout.Add(phaseHorizonFields);

            applyField.RegisterValueChangeCallback(_ =>
            {
                serializedObject.Update();
                phaseHorizonFields.SetEnabled(applyHorizonIllusion != null && applyHorizonIllusion.boolValue);
            });

            root.Add(foldout);
        }

        private void AddField(VisualElement root, string propertyName, string label = null)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            PropertyField field = label == null ? new PropertyField(property) : new PropertyField(property, label);
            field.Bind(serializedObject);
            root.Add(field);
        }
    }
}
