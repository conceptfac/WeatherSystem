using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ConceptFactory.Weather
{
    public enum MoonPhase
    {
        NewMoon,
        WaxingCrescent,
        FirstQuarter,
        WaxingGibbous,
        FullMoon,
        WaningGibbous,
        LastQuarter,
        WaningCrescent
    }

    [Serializable]
    public sealed class MoonPhaseVisualTuning
    {
        public float darkMoonExposure = 1.5f;
        public float moonHaloIntensityMultiplier = 1f;
        public float borderHaloIntensityMultiplier = 1f;
        public bool applyHorizonIllusion;
        public float horizonMoonIntensityMultiplier = 1f;
        public float horizonWarmTintStrengthMultiplier = 1f;
        public Color horizonTintColor = new(1f, 0.776f, 0.525f, 1f);
    }

    [Serializable]
    public sealed class MoonPhaseVisualTuningSet
    {
        public MoonPhaseVisualTuning newMoon = new MoonPhaseVisualTuning { darkMoonExposure = 3f, moonHaloIntensityMultiplier = 1f, borderHaloIntensityMultiplier = 1f, applyHorizonIllusion = false, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning waxingCrescent = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.25f, borderHaloIntensityMultiplier = 1.1666666f, applyHorizonIllusion = false, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning firstQuarter = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.5f, borderHaloIntensityMultiplier = 1.3333334f, applyHorizonIllusion = false, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning waxingGibbous = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.75f, borderHaloIntensityMultiplier = 1.6666666f, applyHorizonIllusion = true, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning fullMoon = new MoonPhaseVisualTuning { darkMoonExposure = 1f, moonHaloIntensityMultiplier = 2f, borderHaloIntensityMultiplier = 2f, applyHorizonIllusion = true, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning waningGibbous = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.75f, borderHaloIntensityMultiplier = 1.6666666f, applyHorizonIllusion = true, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning lastQuarter = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.5f, borderHaloIntensityMultiplier = 1.3333334f, applyHorizonIllusion = false, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
        public MoonPhaseVisualTuning waningCrescent = new MoonPhaseVisualTuning { darkMoonExposure = 1.5f, moonHaloIntensityMultiplier = 1.25f, borderHaloIntensityMultiplier = 1.1666666f, applyHorizonIllusion = false, horizonMoonIntensityMultiplier = 1f, horizonWarmTintStrengthMultiplier = 1f, horizonTintColor = new Color(1f, 0.776f, 0.525f, 1f) };
    }

    [Serializable]
    public sealed class MoonPhasePresetSet
    {
        public LunarVisualPreset newMoon;
        public LunarVisualPreset waxingCrescent;
        public LunarVisualPreset firstQuarter;
        public LunarVisualPreset waxingGibbous;
        public LunarVisualPreset fullMoon;
        public LunarVisualPreset waningGibbous;
        public LunarVisualPreset lastQuarter;
        public LunarVisualPreset waningCrescent;
    }

    /// <summary>
    /// Drives a directional light using approximate lunar astronomy while reading time and location from a WeatherSolarController.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Concept Factory/Weather System/Lunar Controller")]
    public sealed class LunarController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Solar controller used as the source of location, date and local civil time.")]
        [SerializeField] private WeatherSolarController _solarController;

        [Tooltip("Directional light that represents the Moon.")]
        [SerializeField] private Light _moonLight;

        [Tooltip("Optional transform override. If assigned, rotation is applied here instead of the Light transform.")]
        [SerializeField] private Transform _moonTransformOverride;

        [Tooltip("Per-phase visual presets that own lighting, fades, halos, horizon illusion and phase-specific tuning.")]
        [SerializeField] private MoonPhasePresetSet _phasePresets = new MoonPhasePresetSet();

        [Tooltip("Optional color gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _lightColorOverNight;

        [Tooltip("Optional intensity curve sampled over the local 24-hour cycle.")]
        [SerializeField] private AnimationCurve _lightIntensityOverNight = DefaultIntensityCurve();

        [Header("Lighting")]
        [Tooltip("Base intensity multiplier before optional curve, moon phase and daylight attenuation.")]
        [SerializeField] private float _baseIntensity = 1f;

        [Tooltip("Whether the light should be disabled when the moon is below the horizon threshold.")]
        [SerializeField] private bool _disableLightBelowHorizon = true;

        [Tooltip("Below this apparent elevation, the light is considered out of view and can be disabled.")]
        [SerializeField] private float _horizonDisableThreshold = -2f;

        [Tooltip("How strongly daylight suppresses the moon light. Higher values fade the moon faster after sunrise.")]
        [SerializeField] private float _daylightFadeStrength = 1.6f;

        [Tooltip("Minimum illumination multiplier, useful if you never want the moon to disappear completely on crescent phases.")]
        [SerializeField] private float _minimumPhaseLight = 2f;

        [Header("Skybox")]
        [Tooltip("Whether this controller should also drive the moon disk values used by the sky shader.")]
        [SerializeField] private bool _driveSkyboxMaterial = true;

        [Tooltip("Global moon disk intensity multiplier sent to the sky shader.")]
        [SerializeField] private float _skyboxMoonIntensity = 1f;

        [Tooltip("Controls the softness of the moon disk outer edge against the sky. Lower values make the edge softer, higher values make it crisper.")]
        [FormerlySerializedAs("_skyboxMoonFalloff")]
        [SerializeField] private float _moonDiskEdgeSoftness = 1598.1f;

        [Tooltip("Global moon disk size sent to the sky shader.")]
        [HideInInspector]
        [SerializeField] private float _skyboxMoonSize = 8f;

        [Tooltip("Lit moon texture used for the illuminated side of the disk.")]
        [SerializeField] private Texture2D _moonTexture;

        [Tooltip("Dark moon texture used for the shadowed side of the disk.")]
        [SerializeField] private Texture2D _darkMoonTexture;

        [Tooltip("Optional negative phase mask texture used to crop the illuminated moon disk.")]
        [SerializeField] private Texture2D _moonPhaseMaskTexture;

        [Tooltip("Brightness multiplier applied to the illuminated moon texture in the sky.")]
        [HideInInspector]
        [SerializeField] private float _moonTextureExposure = 2f;

        [Tooltip("Brightness multiplier applied to the dark moon texture in the sky.")]
        [HideInInspector]
        [SerializeField] private float _darkMoonTextureExposure = 1.5f;

        [Tooltip("Softness of the terminator between light and shadow.")]
        [SerializeField] private float _terminatorSoftness = 0.08f;

        [Tooltip("How visible the dark side of the moon remains at night.")]
        [HideInInspector]
        [SerializeField] private float _darkSideVisibility = 3.8f;

        [Tooltip("Solar elevation, in degrees, where the dark side of the moon and its halos start to disappear.")]
        [SerializeField] private float _daylightShadowFadeStart = -15f;

        [Tooltip("Solar elevation range, in degrees, used to fade the dark side and its halos after the fade start.")]
        [SerializeField] private float _daylightShadowFadeRange = 1.5f;

        [Tooltip("Solar elevation, in degrees, where the illuminated side of the moon starts becoming visible against a bright sky.")]
        [SerializeField] private float _daylightLitMoonFadeStart = -6f;

        [Tooltip("Solar elevation range, in degrees, used to fade in the illuminated side of the moon around dusk and dawn.")]
        [SerializeField] private float _daylightLitMoonFadeRange = 10f;

        [Tooltip("Extra suppression applied to the illuminated side of the moon while the sun is setting, to avoid an overly bright moon at dusk.")]
        [SerializeField] private float _duskLitMoonSuppression = 0.55f;

        [Tooltip("Minimum visibility kept on the illuminated moon disk even under a bright sky, so it never disappears completely.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minimumLitMoonSkyVisibility = 1f;

        [Tooltip("Tint used by the subtle glow around the moon disk.")]
        [SerializeField] private Color _moonHaloColor = new(0.72f, 0.82f, 1f, 1f);

        [Tooltip("Brightness of the halo around the moon.")]
        [SerializeField] private float _moonHaloIntensity = 0.15f;

        [Tooltip("Inner start of the halo relative to the moon edge. Use small negative values to pull it slightly inside.")]
        [SerializeField] private float _moonHaloInnerSize = 0.15f;

        [Tooltip("Outer reach of the halo beyond the moon edge.")]
        [SerializeField] private float _moonHaloOuterSize = 0.49f;

        [Tooltip("Softness of the halo fade from inner to outer edge.")]
        [SerializeField] private float _moonHaloTerminator = 3.02f;

        [Tooltip("Tint used by the border halo around the moon disk.")]
        [SerializeField] private Color _borderHaloColor = new(0.72f, 0.82f, 1f, 1f);

        [Tooltip("Brightness of the border halo around the moon.")]
        [SerializeField] private float _borderHaloIntensity = 0.4f;

        [Tooltip("Inner start of the border halo, where 0 starts at the moon center and 1 reaches the moon edge.")]
        [SerializeField] private float _borderHaloInnerSize = 0.5f;

        [Tooltip("Outer reach of the border halo beyond the moon edge.")]
        [SerializeField] private float _borderHaloOuterSize = 0.52f;

        [Tooltip("Softness of the border halo fade from inner to outer edge.")]
        [SerializeField] private float _borderHaloTerminator = 20f;

        [Tooltip("Applies a perceptual size boost near the horizon so the moon feels larger when rising or setting.")]
        [SerializeField] private bool _enableHorizonMoonIllusion = true;

        [Tooltip("Maximum absolute lunar elevation, in degrees, where the horizon-size illusion fades out.")]
        [SerializeField] private float _horizonIllusionMaxElevation = 22f;

        [Tooltip("Multiplier applied to the moon disk size when the moon is on the horizon.")]
        [SerializeField] private float _horizonMoonSizeMultiplier = 1f;

        [Tooltip("Whether the moon disk size should respond to the Moon's orbital distance from Earth.")]
        [SerializeField] private bool _useOrbitalDistanceSizeVariation = true;

        [Tooltip("Size multiplier applied when the Moon is closest to Earth.")]
        [SerializeField] private float _perigeeMoonSizeMultiplier = 1.07f;

        [Tooltip("Size multiplier applied when the Moon is farthest from Earth.")]
        [SerializeField] private float _apogeeMoonSizeMultiplier = 0.93f;

        [Tooltip("Extra size curve applied only while the moon is rising. Use it to exaggerate the moon near moonrise without affecting moonset.")]
        [SerializeField] private AnimationCurve _moonRiseSizeCurve = DefaultMoonRiseSizeCurve();

        [Tooltip("Multiplier applied to moon disk intensity when the moon is on the horizon.")]
        [SerializeField] private float _horizonMoonIntensityMultiplier = 1.2f;

        [Tooltip("Multiplier applied to moon disk edge softness when the moon is on the horizon. Lower values create a softer outer edge.")]
        [SerializeField] private float _horizonMoonFalloffMultiplier = 0.68f;

        [Tooltip("How strongly the moon color warms near the horizon.")]
        [SerializeField] private float _horizonMoonWarmTintStrength = 1.2f;

        [Tooltip("Warm tint blended into the moon near the horizon.")]
        [SerializeField] private Color _horizonMoonTintColor = new(1f, 0.776f, 0.525f, 1f);

        [Tooltip("Extra warm-tint boost applied specifically while the moon is rising near the horizon.")]
        [SerializeField] private float _moonRiseWarmTintBoost = 10f;

        [Header("Altitude Atmosphere")]
        [Tooltip("Altitude where the atmospheric moon adjustments reach their maximum effect.")]
        [SerializeField] private float _altitudeAtmosphereMaxMeters = 3000f;

        [Tooltip("How much the horizon warm tint is reduced at high altitude.")]
        [SerializeField] private float _altitudeWarmTintReduction = 0.4f;

        [Tooltip("Extra clarity boost applied to the moon at high altitude.")]
        [SerializeField] private float _altitudeClarityBoost = 0.14f;

        [Header("Phase Tuning")]
        [Tooltip("Per-phase overrides for dark moon exposure and halo multipliers in the sky.")]
        [HideInInspector]
        [SerializeField] private MoonPhaseVisualTuningSet _phaseTuning = new MoonPhaseVisualTuningSet();

        [Header("Debug")]
        [Tooltip("Latest apparent lunar elevation in degrees.")]
        [SerializeField] private float _currentElevation;

        [Tooltip("Latest lunar azimuth in degrees clockwise from north.")]
        [SerializeField] private float _currentAzimuth;

        [Tooltip("Current moon illumination fraction from 0 to 1.")]
        [SerializeField] private float _currentIlluminationFraction;

        [Tooltip("Current unit vector from world origin toward the Moon.")]
        [SerializeField] private Vector3 _currentMoonDirection = Vector3.up;

        [Tooltip("Approximate lunar age inside the synodic month.")]
        [SerializeField] private float _currentLunarAgeDays;

        [Tooltip("Current Earth-Moon distance in kilometers.")]
        [SerializeField] private float _currentDistanceKilometers;

        [Tooltip("Normalized Earth-Moon distance where 0 is perigee and 1 is apogee.")]
        [Range(0f, 1f)]
        [SerializeField] private float _currentDistanceNormalized;

        [Tooltip("Formatted moon phase label for quick inspection.")]
        [SerializeField] private string _currentPhaseLabel = "Waxing Gibbous";

        [Tooltip("Current major moon phase bucket.")]
        [SerializeField] private MoonPhase _currentPhase = MoonPhase.WaxingGibbous;

        [Tooltip("Whether the moon is currently rising rather than setting.")]
        [SerializeField] private bool _isMoonRising;

        [NonSerialized] private LunarVisualPreset _defaultNewMoonPreset;
        [NonSerialized] private LunarVisualPreset _defaultWaxingCrescentPreset;
        [NonSerialized] private LunarVisualPreset _defaultFirstQuarterPreset;
        [NonSerialized] private LunarVisualPreset _defaultWaxingGibbousPreset;
        [NonSerialized] private LunarVisualPreset _defaultFullMoonPreset;
        [NonSerialized] private LunarVisualPreset _defaultWaningGibbousPreset;
        [NonSerialized] private LunarVisualPreset _defaultLastQuarterPreset;
        [NonSerialized] private LunarVisualPreset _defaultWaningCrescentPreset;

        public LunarPositionData CurrentLunarData { get; private set; }

        public WeatherSolarController SolarController => _solarController;
        public MoonPhasePresetSet PhasePresets => _phasePresets;

        private static readonly int WeatherMoonDirectionShaderId = Shader.PropertyToID("_WeatherMoonDirection");
        private static readonly int WeatherMoonColorShaderId = Shader.PropertyToID("_WeatherMoonColor");
        private static readonly int WeatherMoonIntensityShaderId = Shader.PropertyToID("_WeatherMoonIntensity");
        private static readonly int WeatherMoonDiskEdgeSoftnessShaderId = Shader.PropertyToID("_WeatherMoonDiskEdgeSoftness");
        private static readonly int WeatherMoonSizeShaderId = Shader.PropertyToID("_WeatherMoonSize");
        private static readonly int WeatherMoonPhaseAngleShaderId = Shader.PropertyToID("_WeatherMoonPhaseAngle");
        private static readonly int WeatherMoonIlluminationShaderId = Shader.PropertyToID("_WeatherMoonIllumination");
        private static readonly int WeatherMoonTextureShaderId = Shader.PropertyToID("_WeatherMoonTexture");
        private static readonly int WeatherMoonDarkTextureShaderId = Shader.PropertyToID("_WeatherMoonDarkTexture");
        private static readonly int WeatherMoonPhaseMaskTextureShaderId = Shader.PropertyToID("_WeatherMoonPhaseMaskTexture");
        private static readonly int WeatherMoonUseTextureShaderId = Shader.PropertyToID("_WeatherMoonUseTexture");
        private static readonly int WeatherMoonTextureExposureShaderId = Shader.PropertyToID("_WeatherMoonTextureExposure");
        private static readonly int WeatherMoonDarkTextureExposureShaderId = Shader.PropertyToID("_WeatherMoonDarkTextureExposure");
        private static readonly int WeatherMoonTerminatorSoftnessShaderId = Shader.PropertyToID("_WeatherMoonTerminatorSoftness");
        private static readonly int WeatherMoonDarkSideVisibilityShaderId = Shader.PropertyToID("_WeatherMoonDarkSideVisibility");
        private static readonly int WeatherMoonHaloColorShaderId = Shader.PropertyToID("_WeatherMoonHaloColor");
        private static readonly int WeatherMoonHaloIntensityShaderId = Shader.PropertyToID("_WeatherMoonHaloIntensity");
        private static readonly int WeatherMoonHaloInnerSizeShaderId = Shader.PropertyToID("_WeatherMoonHaloInnerSize");
        private static readonly int WeatherMoonHaloOuterSizeShaderId = Shader.PropertyToID("_WeatherMoonHaloOuterSize");
        private static readonly int WeatherMoonHaloTerminatorShaderId = Shader.PropertyToID("_WeatherMoonHaloTerminator");
        private static readonly int WeatherMoonBorderHaloColorShaderId = Shader.PropertyToID("_WeatherMoonBorderHaloColor");
        private static readonly int WeatherMoonBorderHaloIntensityShaderId = Shader.PropertyToID("_WeatherMoonBorderHaloIntensity");
        private static readonly int WeatherMoonBorderHaloInnerSizeShaderId = Shader.PropertyToID("_WeatherMoonBorderHaloInnerSize");
        private static readonly int WeatherMoonBorderHaloOuterSizeShaderId = Shader.PropertyToID("_WeatherMoonBorderHaloOuterSize");
        private static readonly int WeatherMoonBorderHaloTerminatorShaderId = Shader.PropertyToID("_WeatherMoonBorderHaloTerminator");
        private static readonly int WeatherMoonSkyVisibilityShaderId = Shader.PropertyToID("_WeatherMoonSkyVisibility");
        private static readonly int WeatherMoonDarkSkyVisibilityShaderId = Shader.PropertyToID("_WeatherMoonDarkSkyVisibility");

        private void Reset()
        {
            _moonLight = GetComponent<Light>();
            EnsureDefaults();
        }

        private void OnEnable()
        {
            EnsureDefaults();
            UpdateLunarState();
        }

        private void OnDisable()
        {
            ApplySkyboxDefaults();
        }

        private void Update()
        {
            EnsureDefaults();
            UpdateLunarState();
        }

        private void OnValidate()
        {
            SanitizeMoonDiskEdgeSoftness();

            if (_moonRiseSizeCurve == null || _moonRiseSizeCurve.length == 0)
            {
                _moonRiseSizeCurve = DefaultMoonRiseSizeCurve();
            }

            if (_phaseTuning == null)
            {
                _phaseTuning = new MoonPhaseVisualTuningSet();
            }

            EnsurePhasePresets();

            EnsureDefaults();
            UpdateLunarState();
        }

        private void EnsureDefaults()
        {
            SanitizeMoonDiskEdgeSoftness();

            if (_solarController == null)
            {
                _solarController = FindFirstObjectByType<WeatherSolarController>();
            }

            if (_moonLight == null)
            {
                _moonLight = GetComponent<Light>();
            }

            if (_moonLight != null && _moonLight.type != LightType.Directional)
            {
                _moonLight.type = LightType.Directional;
            }

            if (_lightColorOverNight == null || _lightColorOverNight.colorKeys.Length == 0)
            {
                _lightColorOverNight = CreateDefaultGradient();
            }

            if (_lightIntensityOverNight == null || _lightIntensityOverNight.length == 0)
            {
                _lightIntensityOverNight = DefaultIntensityCurve();
            }

            if (_moonRiseSizeCurve == null || _moonRiseSizeCurve.length == 0)
            {
                _moonRiseSizeCurve = DefaultMoonRiseSizeCurve();
            }

            if (_phaseTuning == null)
            {
                _phaseTuning = new MoonPhaseVisualTuningSet();
            }

            EnsurePhasePresets();

        }

        private void SanitizeMoonDiskEdgeSoftness()
        {
            if (float.IsNaN(_moonDiskEdgeSoftness) || float.IsInfinity(_moonDiskEdgeSoftness) || Mathf.Abs(_moonDiskEdgeSoftness) > 100000f)
            {
                _moonDiskEdgeSoftness = 1598.1f;
                return;
            }

            _moonDiskEdgeSoftness = Mathf.Clamp(_moonDiskEdgeSoftness, 0f, 5000f);
        }

        private void UpdateLunarState()
        {
            if (_solarController == null)
            {
                DisableMoonLight();
                return;
            }

            CurrentLunarData = LunarPositionCalculator.CalculateLunarPosition(
                _solarController.LocalDateTime,
                _solarController.Latitude,
                _solarController.Longitude,
                _solarController.UtcOffsetHours);

            _currentMoonDirection = CurrentLunarData.MoonDirection;
            _currentElevation = CurrentLunarData.ApparentElevationDegrees;
            _currentAzimuth = CurrentLunarData.AzimuthDegrees;
            _currentIlluminationFraction = CurrentLunarData.IlluminationFraction;
            _currentLunarAgeDays = CurrentLunarData.LunarAgeDays;
            _currentDistanceKilometers = CurrentLunarData.DistanceKilometers;
            _currentDistanceNormalized = CurrentLunarData.DistanceNormalized;
            _currentPhase = GetPhase(CurrentLunarData.LunarAgeDays);
            _currentPhaseLabel = GetPhaseLabel(_currentPhase);
            _isMoonRising = EvaluateIsMoonRising();

            ApplyLightTransform(GetNightLightDirection());
            ApplyLightAppearance();
            ApplySkyboxAppearance();
        }

        private Vector3 GetNightLightDirection()
        {
            bool isNight = _solarController == null || _solarController.CurrentSolarData.DaylightFactor < 0.999f;
            if (!isNight || _currentElevation >= _horizonDisableThreshold)
            {
                return _currentMoonDirection;
            }

            // Keep the visible moon astronomical, but force the night light to stay above the horizon.
            const float fallbackNightElevation = 18f;
            return SolarPositionCalculator.ToUnitySunDirection(_currentAzimuth, fallbackNightElevation).normalized;
        }

        private void ApplyLightTransform(Vector3 moonDirection)
        {
            Transform targetTransform = GetTargetTransform();
            if (targetTransform == null)
            {
                return;
            }

            Vector3 lightForward = -moonDirection;
            if (lightForward.sqrMagnitude < 0.000001f)
            {
                return;
            }

            targetTransform.rotation = Quaternion.LookRotation(lightForward.normalized, Vector3.up);
        }

        private void ApplyLightAppearance()
        {
            if (_moonLight == null)
            {
                return;
            }

            float dayFraction = GetCurrentDayFraction();
            Gradient lightColorOverNight = GetLightColorOverNight();
            AnimationCurve lightIntensityOverNight = GetLightIntensityOverNight();
            float curveIntensity = lightIntensityOverNight != null && lightIntensityOverNight.length > 0
                ? Mathf.Max(0f, lightIntensityOverNight.Evaluate(dayFraction))
                : 1f;
            float phaseIntensity = Mathf.Clamp01(_currentIlluminationFraction);
            float daylightSuppression = 1f;
            if (_solarController != null)
            {
                daylightSuppression = Mathf.Pow(1f - Mathf.Clamp01(_solarController.CurrentSolarData.DaylightFactor), Mathf.Max(0.01f, GetDaylightFadeStrength()));
            }

            bool isNight = _solarController == null || _solarController.CurrentSolarData.DaylightFactor < 0.999f;
            float elevationVisibility = isNight
                ? 1f
                : Mathf.InverseLerp(GetHorizonDisableThreshold() - 8f, 8f, _currentElevation);
            float horizonIllusion = EvaluateHorizonIllusionFactor();
            float altitudeAtmosphereFactor = EvaluateAltitudeAtmosphereFactor();
            float altitudeClarity = 1f + (GetAltitudeClarityBoost() * altitudeAtmosphereFactor);
            _moonLight.intensity = GetBaseIntensity() * curveIntensity * phaseIntensity * daylightSuppression * elevationVisibility * altitudeClarity;

            Color baseMoonColor = lightColorOverNight != null && lightColorOverNight.colorKeys.Length > 0
                ? lightColorOverNight.Evaluate(dayFraction)
                : Color.white;
            _moonLight.color = EvaluateHorizonTintedMoonColor(baseMoonColor, dayFraction, altitudeAtmosphereFactor);

            if (GetDisableLightBelowHorizon() && !isNight)
            {
                _moonLight.enabled = _currentElevation > GetHorizonDisableThreshold() && _moonLight.intensity > 0.0001f;
            }
            else
            {
                _moonLight.enabled = _moonLight.intensity > 0.0001f;
            }
        }

        private Transform GetTargetTransform()
        {
            if (_moonTransformOverride != null)
            {
                return _moonTransformOverride;
            }

            return _moonLight != null ? _moonLight.transform : transform;
        }

        private float GetCurrentDayFraction()
        {
            if (_solarController == null)
            {
                return 0f;
            }

            double seconds = _solarController.LocalDateTime.TimeOfDay.TotalSeconds / 86400.0;
            return (float)Math.Clamp(seconds, 0.0, 1.0);
        }

        private void DisableMoonLight()
        {
            CurrentLunarData = default;
            _currentMoonDirection = Vector3.up;
            _currentElevation = 0f;
            _currentAzimuth = 0f;
            _currentIlluminationFraction = 0f;
            _currentLunarAgeDays = 0f;
            _currentDistanceKilometers = 0f;
            _currentDistanceNormalized = 0f;
            _currentPhaseLabel = "No Solar Controller";

            if (_moonLight != null)
            {
                _moonLight.enabled = false;
                _moonLight.intensity = 0f;
            }

            ApplySkyboxDefaults();
        }

        private void ApplySkyboxAppearance()
        {
            if (!GetDriveSkyboxMaterial())
            {
                ApplySkyboxDefaults();
                return;
            }

            GetSkyboxPhasePreset(_currentPhase, out float skyboxPhaseAngle, out float skyboxIllumination);
            float phaseIntensity = Mathf.Lerp(GetMinimumPhaseLight(), 1f, skyboxIllumination);
            float horizonIllusion = EvaluateHorizonIllusionFactor();
            float altitudeAtmosphereFactor = EvaluateAltitudeAtmosphereFactor();
            float altitudeClarity = 1f + (GetAltitudeClarityBoost() * altitudeAtmosphereFactor);
            float dayFraction = GetCurrentDayFraction();
            Color baseMoonColor = EvaluateBaseMoonColor(dayFraction);
            Color moonColor = EvaluateHorizonTintedMoonColor(baseMoonColor, dayFraction, altitudeAtmosphereFactor);
            MoonPhaseVisualTuning phaseTuning = GetPhaseTuning(_currentPhase);
            float moonIntensity = Mathf.Lerp(GetSkyboxMoonIntensity(), GetSkyboxMoonIntensity() * GetHorizonMoonIntensityMultiplier() * Mathf.Max(0f, phaseTuning.horizonMoonIntensityMultiplier), horizonIllusion) * phaseIntensity * altitudeClarity;
            float moonDiskEdgeSoftness = Mathf.Lerp(GetMoonDiskEdgeSoftness(), GetMoonDiskEdgeSoftness() * GetHorizonMoonFalloffMultiplier(), horizonIllusion) * altitudeClarity;
            float risingSizeMultiplier = EvaluateMoonRiseSizeMultiplier(horizonIllusion);
            float orbitalDistanceSizeMultiplier = EvaluateOrbitalDistanceSizeMultiplier();
            float moonSize = Mathf.Lerp(GetSkyboxMoonSize(), GetSkyboxMoonSize() * GetHorizonMoonSizeMultiplier() * risingSizeMultiplier, horizonIllusion) * orbitalDistanceSizeMultiplier;
            float moonSkyVisibility = EvaluateMoonSkyVisibility();
            float darkMoonSkyVisibility = EvaluateDarkMoonSkyVisibility();
            float moonHaloIntensity = GetMoonHaloIntensity() * Mathf.Max(0f, phaseTuning.moonHaloIntensityMultiplier);
            float borderHaloIntensity = GetBorderHaloIntensity() * Mathf.Max(0f, phaseTuning.borderHaloIntensityMultiplier);

            Shader.SetGlobalVector(WeatherMoonDirectionShaderId, _currentMoonDirection.normalized);
            Shader.SetGlobalColor(WeatherMoonColorShaderId, moonColor);
            Shader.SetGlobalFloat(WeatherMoonIntensityShaderId, moonIntensity);
            Shader.SetGlobalFloat(WeatherMoonDiskEdgeSoftnessShaderId, moonDiskEdgeSoftness);
            Shader.SetGlobalFloat(WeatherMoonSizeShaderId, moonSize);
            Shader.SetGlobalFloat(WeatherMoonPhaseAngleShaderId, skyboxPhaseAngle);
            Shader.SetGlobalFloat(WeatherMoonIlluminationShaderId, skyboxIllumination);
            Shader.SetGlobalFloat(WeatherMoonTextureExposureShaderId, GetMoonTextureExposure());
            float darkMoonExposure = GetDarkMoonTextureExposure() * Mathf.Max(0f, phaseTuning.darkMoonExposure);
            Shader.SetGlobalFloat(WeatherMoonDarkTextureExposureShaderId, darkMoonExposure);
            Shader.SetGlobalFloat(WeatherMoonTerminatorSoftnessShaderId, GetTerminatorSoftness());
            Shader.SetGlobalFloat(WeatherMoonDarkSideVisibilityShaderId, GetDarkSideVisibility());
            Shader.SetGlobalColor(WeatherMoonHaloColorShaderId, GetMoonHaloColor());
            Shader.SetGlobalFloat(WeatherMoonHaloIntensityShaderId, moonHaloIntensity);
            Shader.SetGlobalFloat(WeatherMoonHaloInnerSizeShaderId, GetMoonHaloInnerSize());
            Shader.SetGlobalFloat(WeatherMoonHaloOuterSizeShaderId, GetMoonHaloOuterSize());
            Shader.SetGlobalFloat(WeatherMoonHaloTerminatorShaderId, GetMoonHaloTerminator());
            Shader.SetGlobalColor(WeatherMoonBorderHaloColorShaderId, GetBorderHaloColor());
            Shader.SetGlobalFloat(WeatherMoonBorderHaloIntensityShaderId, borderHaloIntensity);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloInnerSizeShaderId, GetBorderHaloInnerSize());
            Shader.SetGlobalFloat(WeatherMoonBorderHaloOuterSizeShaderId, GetBorderHaloOuterSize());
            Shader.SetGlobalFloat(WeatherMoonBorderHaloTerminatorShaderId, GetBorderHaloTerminator());
            Shader.SetGlobalFloat(WeatherMoonSkyVisibilityShaderId, moonSkyVisibility);
            Shader.SetGlobalFloat(WeatherMoonDarkSkyVisibilityShaderId, darkMoonSkyVisibility);
            ApplyMoonTexturesToShader();
        }

        private static void ApplySkyboxDefaults()
        {
            Shader.SetGlobalVector(WeatherMoonDirectionShaderId, Vector3.zero);
            Shader.SetGlobalColor(WeatherMoonColorShaderId, Color.black);
            Shader.SetGlobalFloat(WeatherMoonIntensityShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonDiskEdgeSoftnessShaderId, 1400f);
            Shader.SetGlobalFloat(WeatherMoonSizeShaderId, 0.38f);
            Shader.SetGlobalFloat(WeatherMoonPhaseAngleShaderId, 180f);
            Shader.SetGlobalFloat(WeatherMoonIlluminationShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonTextureExposureShaderId, 1.35f);
            Shader.SetGlobalFloat(WeatherMoonDarkTextureExposureShaderId, 1f);
            Shader.SetGlobalTexture(WeatherMoonTextureShaderId, Texture2D.whiteTexture);
            Shader.SetGlobalTexture(WeatherMoonDarkTextureShaderId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(WeatherMoonPhaseMaskTextureShaderId, Texture2D.whiteTexture);
            Shader.SetGlobalFloat(WeatherMoonUseTextureShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonTerminatorSoftnessShaderId, 0.08f);
            Shader.SetGlobalFloat(WeatherMoonDarkSideVisibilityShaderId, 0.24f);
            Shader.SetGlobalColor(WeatherMoonHaloColorShaderId, new Color(0.72f, 0.82f, 1f, 1f));
            Shader.SetGlobalFloat(WeatherMoonHaloIntensityShaderId, 0.08f);
            Shader.SetGlobalFloat(WeatherMoonHaloInnerSizeShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonHaloOuterSizeShaderId, 0.03f);
            Shader.SetGlobalFloat(WeatherMoonHaloTerminatorShaderId, 0.5f);
            Shader.SetGlobalColor(WeatherMoonBorderHaloColorShaderId, new Color(0.72f, 0.82f, 1f, 1f));
            Shader.SetGlobalFloat(WeatherMoonBorderHaloIntensityShaderId, 0.04f);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloInnerSizeShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloOuterSizeShaderId, 0.015f);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloTerminatorShaderId, 0.25f);
            Shader.SetGlobalFloat(WeatherMoonSkyVisibilityShaderId, 1f);
            Shader.SetGlobalFloat(WeatherMoonDarkSkyVisibilityShaderId, 1f);
        }

        private float EvaluateMoonSkyVisibility()
        {
            if (_solarController == null)
            {
                return 1f;
            }

            float solarElevation = _solarController.CurrentSolarData.ApparentElevationDegrees;
            float fadeEnd = GetDaylightLitMoonFadeStart() + Mathf.Max(0.01f, GetDaylightLitMoonFadeRange());
            float visibility = Mathf.InverseLerp(fadeEnd, GetDaylightLitMoonFadeStart(), solarElevation);
            visibility = visibility * visibility * (3f - (2f * visibility));

            bool isSunSetting = _solarController.CurrentSolarData.HourAngleDegrees > 0f;
            if (isSunSetting)
            {
                visibility *= Mathf.Clamp01(1f - GetDuskLitMoonSuppression());
            }

            return Mathf.Lerp(
                Mathf.Clamp01(GetMinimumLitMoonSkyVisibility()),
                1f,
                Mathf.Clamp01(visibility));
        }

        private float EvaluateDarkMoonSkyVisibility()
        {
            if (_solarController == null)
            {
                return 1f;
            }

            float solarElevation = _solarController.CurrentSolarData.ApparentElevationDegrees;
            float fadeRange = Mathf.Max(0.01f, GetDaylightShadowFadeRange());
            float fadeCenter = GetDaylightShadowFadeStart() + (fadeRange * 0.5f);
            float softness = 6f / fadeRange;
            float logistic = 1f / (1f + Mathf.Exp((solarElevation - fadeCenter) * softness));
            return logistic;
        }

        private void ApplyMoonTexturesToShader()
        {
            if (_moonTexture == null && _darkMoonTexture == null)
            {
                Shader.SetGlobalTexture(WeatherMoonTextureShaderId, Texture2D.whiteTexture);
                Shader.SetGlobalTexture(WeatherMoonDarkTextureShaderId, Texture2D.blackTexture);
                Shader.SetGlobalTexture(WeatherMoonPhaseMaskTextureShaderId, _moonPhaseMaskTexture != null ? _moonPhaseMaskTexture : Texture2D.whiteTexture);
                Shader.SetGlobalFloat(WeatherMoonUseTextureShaderId, 0f);
                return;
            }

            Shader.SetGlobalTexture(WeatherMoonTextureShaderId, _moonTexture != null ? _moonTexture : Texture2D.whiteTexture);
            Shader.SetGlobalTexture(WeatherMoonDarkTextureShaderId, _darkMoonTexture != null ? _darkMoonTexture : Texture2D.blackTexture);
            Shader.SetGlobalTexture(WeatherMoonPhaseMaskTextureShaderId, _moonPhaseMaskTexture != null ? _moonPhaseMaskTexture : Texture2D.whiteTexture);
            Shader.SetGlobalFloat(WeatherMoonUseTextureShaderId, 1f);
        }

        private float EvaluateHorizonIllusionFactor()
        {
            if (!GetEnableHorizonMoonIllusion() || !GetPhaseTuning(_currentPhase).applyHorizonIllusion)
            {
                return 0f;
            }

            float normalizedDistanceFromHorizon = Mathf.Clamp01(Mathf.Abs(_currentElevation) / GetHorizonIllusionMaxElevation());
            float proximityToHorizon = 1f - normalizedDistanceFromHorizon;
            return proximityToHorizon * proximityToHorizon * (3f - (2f * proximityToHorizon));
        }

        private float EvaluateMoonRiseSizeMultiplier(float horizonIllusion)
        {
            AnimationCurve moonRiseSizeCurve = GetMoonRiseSizeCurve();
            if (!_isMoonRising || moonRiseSizeCurve == null || moonRiseSizeCurve.length == 0)
            {
                return 1f;
            }

            return Mathf.Max(1f, moonRiseSizeCurve.Evaluate(Mathf.Clamp01(horizonIllusion)));
        }

        private float EvaluateOrbitalDistanceSizeMultiplier()
        {
            if (!GetUseOrbitalDistanceSizeVariation())
            {
                return 1f;
            }

            return Mathf.Lerp(GetPerigeeMoonSizeMultiplier(), GetApogeeMoonSizeMultiplier(), Mathf.Clamp01(CurrentLunarData.DistanceNormalized));
        }

        private Color EvaluateHorizonTintedMoonColor(Color baseMoonColor, float dayFraction, float altitudeAtmosphereFactor)
        {
            MoonPhaseVisualTuning phaseTuning = GetPhaseTuning(_currentPhase);
            if (!phaseTuning.applyHorizonIllusion)
            {
                return baseMoonColor;
            }

            float warmTintFactor = EvaluateHorizonWarmTintFactor();
            float tintReduction = 1f - (GetAltitudeWarmTintReduction() * altitudeAtmosphereFactor);
            float tintAmount = Mathf.Clamp01(warmTintFactor * GetHorizonMoonWarmTintStrength() * Mathf.Max(0f, phaseTuning.horizonWarmTintStrengthMultiplier) * tintReduction);
            Color targetTint = phaseTuning.horizonTintColor;
            return Color.Lerp(baseMoonColor, targetTint, tintAmount);
        }

        private Color EvaluateBaseMoonColor(float dayFraction)
        {
            Gradient lightColorOverNight = GetLightColorOverNight();
            if (lightColorOverNight != null && lightColorOverNight.colorKeys.Length > 0)
            {
                return lightColorOverNight.Evaluate(dayFraction);
            }

            return Color.white;
        }

        private float EvaluateAltitudeAtmosphereFactor()
        {
            if (_solarController == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(_solarController.AltitudeMeters / GetAltitudeAtmosphereMaxMeters());
        }

        private float EvaluateHorizonWarmTintFactor()
        {
            float horizonWindow = Mathf.Max(GetHorizonIllusionMaxElevation(), 0.1f);
            float proximityToHorizon = 1f - Mathf.Clamp01(Mathf.Abs(_currentElevation) / horizonWindow);
            float warmTintFactor = proximityToHorizon * proximityToHorizon;

            if (_isMoonRising)
            {
                warmTintFactor *= GetMoonRiseWarmTintBoost();
            }

            return Mathf.Clamp01(warmTintFactor);
        }

        private bool EvaluateIsMoonRising()
        {
            if (_solarController == null)
            {
                return false;
            }

            DateTime sampleTime = _solarController.LocalDateTime.AddMinutes(5.0);
            LunarPositionData futureData = LunarPositionCalculator.CalculateLunarPosition(
                sampleTime,
                _solarController.Latitude,
                _solarController.Longitude,
                _solarController.UtcOffsetHours);

            return futureData.ApparentElevationDegrees > _currentElevation;
        }

        private static void GetSkyboxPhasePreset(MoonPhase phase, out float phaseAngleDegrees, out float illuminationFraction)
        {
            switch (phase)
            {
                case MoonPhase.NewMoon:
                    phaseAngleDegrees = 0f;
                    illuminationFraction = 0f;
                    break;
                case MoonPhase.WaxingCrescent:
                    phaseAngleDegrees = 45f;
                    illuminationFraction = 0.25f;
                    break;
                case MoonPhase.FirstQuarter:
                    phaseAngleDegrees = 90f;
                    illuminationFraction = 0.5f;
                    break;
                case MoonPhase.WaxingGibbous:
                    phaseAngleDegrees = 135f;
                    illuminationFraction = 0.75f;
                    break;
                case MoonPhase.FullMoon:
                    phaseAngleDegrees = 180f;
                    illuminationFraction = 1f;
                    break;
                case MoonPhase.WaningGibbous:
                    phaseAngleDegrees = 225f;
                    illuminationFraction = 0.75f;
                    break;
                case MoonPhase.LastQuarter:
                    phaseAngleDegrees = 270f;
                    illuminationFraction = 0.5f;
                    break;
                default:
                    phaseAngleDegrees = 315f;
                    illuminationFraction = 0.25f;
                    break;
            }
        }

        private static MoonPhase GetPhase(float lunarAgeDays)
        {
            const float synodicMonthDays = 29.53058867f;
            float normalizedLunarAgeDays = Mathf.Repeat(lunarAgeDays, synodicMonthDays);
            float phaseSpan = synodicMonthDays / 8f;

            if (normalizedLunarAgeDays < phaseSpan * 0.5f || normalizedLunarAgeDays >= synodicMonthDays - (phaseSpan * 0.5f))
            {
                return MoonPhase.NewMoon;
            }

            if (normalizedLunarAgeDays < phaseSpan * 1.5f)
            {
                return MoonPhase.WaxingCrescent;
            }

            if (normalizedLunarAgeDays < phaseSpan * 2.5f)
            {
                return MoonPhase.FirstQuarter;
            }

            if (normalizedLunarAgeDays < phaseSpan * 3.5f)
            {
                return MoonPhase.WaxingGibbous;
            }

            if (normalizedLunarAgeDays < phaseSpan * 4.5f)
            {
                return MoonPhase.FullMoon;
            }

            if (normalizedLunarAgeDays < phaseSpan * 5.5f)
            {
                return MoonPhase.WaningGibbous;
            }

            if (normalizedLunarAgeDays < phaseSpan * 6.5f)
            {
                return MoonPhase.LastQuarter;
            }

            return MoonPhase.WaningCrescent;
        }

        private static string GetPhaseLabel(MoonPhase phase)
        {
            return phase switch
            {
                MoonPhase.NewMoon => "New Moon",
                MoonPhase.WaxingCrescent => "Waxing Crescent",
                MoonPhase.FirstQuarter => "First Quarter",
                MoonPhase.WaxingGibbous => "Waxing Gibbous",
                MoonPhase.FullMoon => "Full Moon",
                MoonPhase.WaningGibbous => "Waning Gibbous",
                MoonPhase.LastQuarter => "Last Quarter",
                _ => "Waning Crescent"
            };
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

        private MoonPhaseVisualTuning GetPhaseTuning(MoonPhase phase)
        {
            LunarVisualPreset preset = GetVisualPreset();
            if (preset != null)
            {
                return new MoonPhaseVisualTuning
                {
                    darkMoonExposure = preset.darkMoonExposure,
                    moonHaloIntensityMultiplier = preset.moonHaloIntensityMultiplier,
                    borderHaloIntensityMultiplier = preset.borderHaloIntensityMultiplier,
                    applyHorizonIllusion = preset.applyHorizonIllusion,
                    horizonMoonIntensityMultiplier = preset.horizonMoonIntensityMultiplier,
                    horizonWarmTintStrengthMultiplier = preset.horizonWarmTintStrengthMultiplier,
                    horizonTintColor = preset.horizonTintColor
                };
            }

            MoonPhaseVisualTuningSet phaseTuning = GetPhaseTuningSet();

            return phase switch
            {
                MoonPhase.NewMoon => phaseTuning.newMoon,
                MoonPhase.WaxingCrescent => phaseTuning.waxingCrescent,
                MoonPhase.FirstQuarter => phaseTuning.firstQuarter,
                MoonPhase.WaxingGibbous => phaseTuning.waxingGibbous,
                MoonPhase.FullMoon => phaseTuning.fullMoon,
                MoonPhase.WaningGibbous => phaseTuning.waningGibbous,
                MoonPhase.LastQuarter => phaseTuning.lastQuarter,
                _ => phaseTuning.waningCrescent
            };
        }

        private LunarVisualPreset GetVisualPreset()
        {
            EnsurePhasePresets();
            return _currentPhase switch
            {
                MoonPhase.NewMoon => GetAssignedOrDefaultPhasePreset(_phasePresets.newMoon, ref _defaultNewMoonPreset, "NewMoonPreset"),
                MoonPhase.WaxingCrescent => GetAssignedOrDefaultPhasePreset(_phasePresets.waxingCrescent, ref _defaultWaxingCrescentPreset, "WaxingCrescentPreset"),
                MoonPhase.FirstQuarter => GetAssignedOrDefaultPhasePreset(_phasePresets.firstQuarter, ref _defaultFirstQuarterPreset, "FirstQuarterPreset"),
                MoonPhase.WaxingGibbous => GetAssignedOrDefaultPhasePreset(_phasePresets.waxingGibbous, ref _defaultWaxingGibbousPreset, "WaxingGibbousPreset"),
                MoonPhase.FullMoon => GetAssignedOrDefaultPhasePreset(_phasePresets.fullMoon, ref _defaultFullMoonPreset, "FullMoonPreset"),
                MoonPhase.WaningGibbous => GetAssignedOrDefaultPhasePreset(_phasePresets.waningGibbous, ref _defaultWaningGibbousPreset, "WaningGibbousPreset"),
                MoonPhase.LastQuarter => GetAssignedOrDefaultPhasePreset(_phasePresets.lastQuarter, ref _defaultLastQuarterPreset, "LastQuarterPreset"),
                _ => GetAssignedOrDefaultPhasePreset(_phasePresets.waningCrescent, ref _defaultWaningCrescentPreset, "WaningCrescentPreset")
            };
        }

        private Gradient GetLightColorOverNight() => GetVisualPreset() != null ? GetVisualPreset().lightColorOverNight : _lightColorOverNight;
        private AnimationCurve GetLightIntensityOverNight() => GetVisualPreset() != null ? GetVisualPreset().lightIntensityOverNight : _lightIntensityOverNight;
        private float GetBaseIntensity() => GetVisualPreset() != null ? GetVisualPreset().baseIntensity : _baseIntensity;
        private bool GetDisableLightBelowHorizon() => GetVisualPreset() != null ? GetVisualPreset().disableLightBelowHorizon : _disableLightBelowHorizon;
        private float GetHorizonDisableThreshold() => GetVisualPreset() != null ? GetVisualPreset().horizonDisableThreshold : _horizonDisableThreshold;
        private float GetDaylightFadeStrength() => GetVisualPreset() != null ? GetVisualPreset().daylightFadeStrength : _daylightFadeStrength;
        private float GetMinimumPhaseLight() => GetVisualPreset() != null ? GetVisualPreset().minimumPhaseLight : _minimumPhaseLight;
        private bool GetDriveSkyboxMaterial() => _driveSkyboxMaterial;
        private float GetSkyboxMoonIntensity() => _skyboxMoonIntensity;
        private float GetMoonDiskEdgeSoftness() => _moonDiskEdgeSoftness;
        private float GetSkyboxMoonSize() => GetVisualPreset() != null ? GetVisualPreset().skyboxMoonSize : _skyboxMoonSize;
        private float GetMoonTextureExposure() => GetVisualPreset() != null ? GetVisualPreset().moonTextureExposure : _moonTextureExposure;
        private float GetDarkMoonTextureExposure() => GetVisualPreset() != null ? GetVisualPreset().darkMoonTextureExposure : _darkMoonTextureExposure;
        private float GetTerminatorSoftness() => _terminatorSoftness;
        private float GetDarkSideVisibility() => GetVisualPreset() != null ? GetVisualPreset().darkSideVisibility : _darkSideVisibility;
        private float GetDaylightShadowFadeStart() => GetVisualPreset() != null ? GetVisualPreset().daylightShadowFadeStart : _daylightShadowFadeStart;
        private float GetDaylightShadowFadeRange() => GetVisualPreset() != null ? GetVisualPreset().daylightShadowFadeRange : _daylightShadowFadeRange;
        private float GetDaylightLitMoonFadeStart() => GetVisualPreset() != null ? GetVisualPreset().daylightLitMoonFadeStart : _daylightLitMoonFadeStart;
        private float GetDaylightLitMoonFadeRange() => GetVisualPreset() != null ? GetVisualPreset().daylightLitMoonFadeRange : _daylightLitMoonFadeRange;
        private float GetDuskLitMoonSuppression() => GetVisualPreset() != null ? GetVisualPreset().duskLitMoonSuppression : _duskLitMoonSuppression;
        private float GetMinimumLitMoonSkyVisibility() => GetVisualPreset() != null ? GetVisualPreset().minimumLitMoonSkyVisibility : _minimumLitMoonSkyVisibility;
        private Color GetMoonHaloColor() => GetVisualPreset() != null ? GetVisualPreset().moonHaloColor : _moonHaloColor;
        private float GetMoonHaloIntensity() => GetVisualPreset() != null ? GetVisualPreset().moonHaloIntensity : _moonHaloIntensity;
        private float GetMoonHaloInnerSize() => GetVisualPreset() != null ? GetVisualPreset().moonHaloInnerSize : _moonHaloInnerSize;
        private float GetMoonHaloOuterSize() => GetVisualPreset() != null ? GetVisualPreset().moonHaloOuterSize : _moonHaloOuterSize;
        private float GetMoonHaloTerminator() => GetVisualPreset() != null ? GetVisualPreset().moonHaloTerminator : _moonHaloTerminator;
        private Color GetBorderHaloColor() => GetVisualPreset() != null ? GetVisualPreset().borderHaloColor : _borderHaloColor;
        private float GetBorderHaloIntensity() => GetVisualPreset() != null ? GetVisualPreset().borderHaloIntensity : _borderHaloIntensity;
        private float GetBorderHaloInnerSize() => GetVisualPreset() != null ? GetVisualPreset().borderHaloInnerSize : _borderHaloInnerSize;
        private float GetBorderHaloOuterSize() => GetVisualPreset() != null ? GetVisualPreset().borderHaloOuterSize : _borderHaloOuterSize;
        private float GetBorderHaloTerminator() => GetVisualPreset() != null ? GetVisualPreset().borderHaloTerminator : _borderHaloTerminator;
        private bool GetEnableHorizonMoonIllusion() => GetVisualPreset() != null ? GetVisualPreset().enableHorizonMoonIllusion : _enableHorizonMoonIllusion;
        private float GetHorizonIllusionMaxElevation() => GetVisualPreset() != null ? GetVisualPreset().horizonIllusionMaxElevation : _horizonIllusionMaxElevation;
        private float GetHorizonMoonSizeMultiplier() => _horizonMoonSizeMultiplier;
        private bool GetUseOrbitalDistanceSizeVariation() => _useOrbitalDistanceSizeVariation;
        private float GetPerigeeMoonSizeMultiplier() => _perigeeMoonSizeMultiplier;
        private float GetApogeeMoonSizeMultiplier() => _apogeeMoonSizeMultiplier;
        private AnimationCurve GetMoonRiseSizeCurve() => GetVisualPreset() != null ? GetVisualPreset().moonRiseSizeCurve : _moonRiseSizeCurve;
        private float GetHorizonMoonIntensityMultiplier() => GetVisualPreset() != null ? GetVisualPreset().horizonMoonIntensityMultiplier : _horizonMoonIntensityMultiplier;
        private float GetHorizonMoonFalloffMultiplier() => GetVisualPreset() != null ? GetVisualPreset().horizonMoonFalloffMultiplier : _horizonMoonFalloffMultiplier;
        private float GetHorizonMoonWarmTintStrength() => GetVisualPreset() != null ? GetVisualPreset().horizonMoonWarmTintStrength : _horizonMoonWarmTintStrength;
        private float GetMoonRiseWarmTintBoost() => GetVisualPreset() != null ? GetVisualPreset().moonRiseWarmTintBoost : _moonRiseWarmTintBoost;
        private float GetAltitudeAtmosphereMaxMeters() => _altitudeAtmosphereMaxMeters;
        private float GetAltitudeWarmTintReduction() => _altitudeWarmTintReduction;
        private float GetAltitudeClarityBoost() => _altitudeClarityBoost;
        private MoonPhaseVisualTuningSet GetPhaseTuningSet() => _phaseTuning;

        private void EnsurePhasePresets()
        {
            _phasePresets ??= new MoonPhasePresetSet();
#if UNITY_EDITOR
            EnsureDefaultPhasePresetAssetFile("NewMoonPreset");
            EnsureDefaultPhasePresetAssetFile("WaxingCrescentPreset");
            EnsureDefaultPhasePresetAssetFile("FirstQuarterPreset");
            EnsureDefaultPhasePresetAssetFile("WaxingGibbousPreset");
            EnsureDefaultPhasePresetAssetFile("FullMoonPreset");
            EnsureDefaultPhasePresetAssetFile("WaningGibbousPreset");
            EnsureDefaultPhasePresetAssetFile("LastQuarterPreset");
            EnsureDefaultPhasePresetAssetFile("WaningCrescentPreset");
#endif
            _phasePresets.newMoon?.EnsureDefaults();
            _phasePresets.waxingCrescent?.EnsureDefaults();
            _phasePresets.firstQuarter?.EnsureDefaults();
            _phasePresets.waxingGibbous?.EnsureDefaults();
            _phasePresets.fullMoon?.EnsureDefaults();
            _phasePresets.waningGibbous?.EnsureDefaults();
            _phasePresets.lastQuarter?.EnsureDefaults();
            _phasePresets.waningCrescent?.EnsureDefaults();
        }

        private LunarVisualPreset GetAssignedOrDefaultPhasePreset(LunarVisualPreset assignedPreset, ref LunarVisualPreset defaultPresetCache, string assetName)
        {
            if (assignedPreset != null)
            {
                assignedPreset.EnsureDefaults();
                return assignedPreset;
            }

            defaultPresetCache ??= LoadOrCreateDefaultPhasePreset(assetName);
            defaultPresetCache?.EnsureDefaults();
            return defaultPresetCache;
        }

        private LunarVisualPreset LoadOrCreateDefaultPhasePreset(string assetName)
        {
#if UNITY_EDITOR
            const string presetsFolder = "Packages/com.conceptfactory.weather/Runtime/Scripts/Celestial/Presets";
            string assetPath = $"{presetsFolder}/{assetName}.asset";
            LunarVisualPreset preset = AssetDatabase.LoadAssetAtPath<LunarVisualPreset>(assetPath);
            if (preset != null)
            {
                return preset;
            }
#endif

            LunarVisualPreset runtimePreset = ScriptableObject.CreateInstance<LunarVisualPreset>();
            runtimePreset.hideFlags = HideFlags.HideAndDontSave;
            runtimePreset.name = assetName;
            runtimePreset.EnsureDefaults();
            ApplyDefaultPresetOverrides(assetName, runtimePreset);
            return runtimePreset;
        }

        private static void ApplyDefaultPresetOverrides(string assetName, LunarVisualPreset preset)
        {
            switch (assetName)
            {
                case "NewMoonPreset":
                    preset.darkMoonExposure = 3f;
                    preset.moonHaloIntensityMultiplier = 1f;
                    preset.borderHaloIntensityMultiplier = 1f;
                    preset.applyHorizonIllusion = false;
                    break;
                case "WaxingCrescentPreset":
                case "WaningCrescentPreset":
                    preset.darkMoonExposure = 1.5f;
                    preset.moonHaloIntensityMultiplier = 1.25f;
                    preset.borderHaloIntensityMultiplier = 1.1666666f;
                    preset.applyHorizonIllusion = false;
                    break;
                case "FirstQuarterPreset":
                case "LastQuarterPreset":
                    preset.darkMoonExposure = 1.5f;
                    preset.moonHaloIntensityMultiplier = 1.5f;
                    preset.borderHaloIntensityMultiplier = 1.3333334f;
                    preset.applyHorizonIllusion = false;
                    break;
                case "WaxingGibbousPreset":
                case "WaningGibbousPreset":
                    preset.darkMoonExposure = 1.5f;
                    preset.moonHaloIntensityMultiplier = 1.75f;
                    preset.borderHaloIntensityMultiplier = 1.6666666f;
                    preset.applyHorizonIllusion = true;
                    break;
                case "FullMoonPreset":
                    preset.darkMoonExposure = 1f;
                    preset.moonHaloIntensityMultiplier = 2f;
                    preset.borderHaloIntensityMultiplier = 2f;
                    preset.applyHorizonIllusion = true;
                    break;
            }
        }

#if UNITY_EDITOR
        private void EnsureDefaultPhasePresetAssetFile(string assetName)
        {
            const string presetsFolder = "Packages/com.conceptfactory.weather/Runtime/Scripts/Celestial/Presets";
            string assetPath = $"{presetsFolder}/{assetName}.asset";
            LunarVisualPreset preset = AssetDatabase.LoadAssetAtPath<LunarVisualPreset>(assetPath);
            if (preset != null)
            {
                return;
            }

            if (File.Exists(assetPath))
            {
                return;
            }

            preset = ScriptableObject.CreateInstance<LunarVisualPreset>();
            preset.name = assetName;
            preset.EnsureDefaults();
            ApplyDefaultPresetOverrides(assetName, preset);
            AssetDatabase.CreateAsset(preset, assetPath);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
