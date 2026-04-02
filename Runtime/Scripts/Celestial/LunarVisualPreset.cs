using System;
using UnityEngine;

namespace ConceptFactory.Weather
{
    [CreateAssetMenu(fileName = "LunarVisualPreset", menuName = "Concept Factory/Weather System/Lunar Visual Preset")]
    public sealed class LunarVisualPreset : ScriptableObject
    {
        [Header("Lighting")]
        public Gradient lightColorOverNight;
        public AnimationCurve lightIntensityOverNight = DefaultIntensityCurve();
        public float baseIntensity = 1f;
        public bool disableLightBelowHorizon = true;
        public float horizonDisableThreshold = -2f;
        public float daylightFadeStrength = 1.6f;
        public float minimumPhaseLight = 2f;

        [Header("Daylight Fades")]
        public float daylightShadowFadeStart = -15f;
        public float daylightShadowFadeRange = 1.5f;
        public float daylightLitMoonFadeStart = -6f;
        public float daylightLitMoonFadeRange = 10f;
        public float duskLitMoonSuppression = 0.55f;
        [Range(0f, 1f)] public float minimumLitMoonSkyVisibility = 1f;

        [Header("Sky Disk")]
        public float skyboxMoonSize = 8f;
        public float moonTextureExposure = 2f;
        public float darkMoonTextureExposure = 1.5f;

        [Header("Halo Layers")]
        public Color moonHaloColor = new(0.72f, 0.82f, 1f, 1f);
        public float moonHaloIntensity = 0.15f;
        public float moonHaloInnerSize = 0.15f;
        public float moonHaloOuterSize = 0.49f;
        public float moonHaloTerminator = 3.02f;
        public Color borderHaloColor = new(0.72f, 0.82f, 1f, 1f);
        public float borderHaloIntensity = 0.4f;
        public float borderHaloInnerSize = 0.5f;
        public float borderHaloOuterSize = 0.52f;
        public float borderHaloTerminator = 20f;

        [Header("Phase Look")]
        public float darkSideVisibility = 3.8f;
        public float darkMoonExposure = 1.5f;
        public float moonHaloIntensityMultiplier = 1f;
        public float borderHaloIntensityMultiplier = 1f;

        [Header("Horizon Illusion")]
        public bool enableHorizonMoonIllusion = true;
        public float horizonIllusionMaxElevation = 22f;
        public AnimationCurve moonRiseSizeCurve = DefaultMoonRiseSizeCurve();
        public bool applyHorizonIllusion;
        public float horizonMoonIntensityMultiplier = 1.2f;
        public float horizonMoonFalloffMultiplier = 0.68f;
        public float horizonMoonWarmTintStrength = 1.2f;
        public float horizonWarmTintStrengthMultiplier = 1f;
        public Color horizonMoonTintColor = new(1f, 0.776f, 0.525f, 1f);
        public Color horizonTintColor = new(1f, 0.776f, 0.525f, 1f);
        public float moonRiseWarmTintBoost = 10f;

        private void OnEnable() => EnsureDefaults();

        private void OnValidate() => EnsureDefaults();

        public void EnsureDefaults()
        {
            if (lightColorOverNight == null || lightColorOverNight.colorKeys.Length == 0)
            {
                lightColorOverNight = CreateDefaultGradient();
            }

            if (lightIntensityOverNight == null || lightIntensityOverNight.length == 0)
            {
                lightIntensityOverNight = DefaultIntensityCurve();
            }

            if (moonRiseSizeCurve == null || moonRiseSizeCurve.length == 0)
            {
                moonRiseSizeCurve = DefaultMoonRiseSizeCurve();
            }
        }

        private static AnimationCurve DefaultIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.9f),
                new Keyframe(0.2f, 0.55f),
                new Keyframe(0.25f, 0.18f),
                new Keyframe(0.5f, 0f),
                new Keyframe(0.75f, 0.18f),
                new Keyframe(0.8f, 0.55f),
                new Keyframe(1f, 0.9f));
        }

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.59f, 0.66f, 0.92f), 0f),
                    new GradientColorKey(new Color(0.73f, 0.78f, 1f), 0.2f),
                    new GradientColorKey(new Color(0.92f, 0.96f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.73f, 0.78f, 1f), 0.8f),
                    new GradientColorKey(new Color(0.59f, 0.66f, 0.92f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve DefaultMoonRiseSizeCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.2f, 1.18f),
                new Keyframe(0.3708374f, 2.1732197f),
                new Keyframe(0.9449878f, 2.2303503f),
                new Keyframe(1f, 2.5439255f));
        }
    }
}
