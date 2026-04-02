using System;
using UnityEngine;

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

        [Tooltip("Optional color gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _lightColorOverNight;

        [Tooltip("Optional intensity curve sampled over the local 24-hour cycle.")]
        [SerializeField] private AnimationCurve _lightIntensityOverNight = DefaultIntensityCurve();

        [Header("Lighting")]
        [Tooltip("Base intensity multiplier before optional curve, moon phase and daylight attenuation.")]
        [SerializeField] private float _baseIntensity = 0.18f;

        [Tooltip("Whether the light should be disabled when the moon is below the horizon threshold.")]
        [SerializeField] private bool _disableLightBelowHorizon = true;

        [Tooltip("Below this apparent elevation, the light is considered out of view and can be disabled.")]
        [SerializeField] private float _horizonDisableThreshold = -2f;

        [Tooltip("How strongly daylight suppresses the moon light. Higher values fade the moon faster after sunrise.")]
        [SerializeField] private float _daylightFadeStrength = 1.65f;

        [Tooltip("Minimum illumination multiplier, useful if you never want the moon to disappear completely on crescent phases.")]
        [SerializeField] private float _minimumPhaseLight = 0.08f;

        [Header("Skybox")]
        [Tooltip("Whether this controller should also drive the moon disk values used by the sky shader.")]
        [SerializeField] private bool _driveSkyboxMaterial = true;

        [Tooltip("Global moon disk intensity multiplier sent to the sky shader.")]
        [SerializeField] private float _skyboxMoonIntensity = 0.22f;

        [Tooltip("Global moon disk sharpness sent to the sky shader.")]
        [SerializeField] private float _skyboxMoonFalloff = 1400f;

        [Tooltip("Global moon disk size sent to the sky shader.")]
        [SerializeField] private float _skyboxMoonSize = 0.38f;

        [Tooltip("Lit moon texture used for the illuminated side of the disk.")]
        [SerializeField] private Texture2D _moonTexture;

        [Tooltip("Dark moon texture used for the shadowed side of the disk.")]
        [SerializeField] private Texture2D _darkMoonTexture;

        [Tooltip("Optional negative phase mask texture used to crop the illuminated moon disk.")]
        [SerializeField] private Texture2D _moonPhaseMaskTexture;

        [Tooltip("Brightness multiplier applied to the illuminated moon texture in the sky.")]
        [SerializeField] private float _moonTextureExposure = 1.35f;

        [Tooltip("Brightness multiplier applied to the dark moon texture in the sky.")]
        [SerializeField] private float _darkMoonTextureExposure = 1f;

        [Tooltip("Softness of the terminator between light and shadow.")]
        [SerializeField] private float _terminatorSoftness = 0.08f;

        [Tooltip("How visible the dark side of the moon remains at night.")]
        [SerializeField] private float _darkSideVisibility = 0.24f;

        [Tooltip("Solar elevation, in degrees, where the dark side of the moon and its halos start to disappear.")]
        [SerializeField] private float _daylightShadowFadeStart = -6f;

        [Tooltip("Solar elevation range, in degrees, used to fade the dark side and its halos after the fade start.")]
        [SerializeField] private float _daylightShadowFadeRange = 4f;

        [Tooltip("Solar elevation, in degrees, where the illuminated side of the moon starts becoming visible against a bright sky.")]
        [SerializeField] private float _daylightLitMoonFadeStart = -6f;

        [Tooltip("Solar elevation range, in degrees, used to fade in the illuminated side of the moon around dusk and dawn.")]
        [SerializeField] private float _daylightLitMoonFadeRange = 10f;

        [Tooltip("Extra suppression applied to the illuminated side of the moon while the sun is setting, to avoid an overly bright moon at dusk.")]
        [SerializeField] private float _duskLitMoonSuppression = 0.55f;

        [Tooltip("Tint used by the subtle glow around the moon disk.")]
        [SerializeField] private Color _moonHaloColor = new(0.72f, 0.82f, 1f, 1f);

        [Tooltip("Brightness of the halo around the moon.")]
        [SerializeField] private float _moonHaloIntensity = 0.08f;

        [Tooltip("Inner start of the halo relative to the moon edge. Use small negative values to pull it slightly inside.")]
        [SerializeField] private float _moonHaloInnerSize = 0f;

        [Tooltip("Outer reach of the halo beyond the moon edge.")]
        [SerializeField] private float _moonHaloOuterSize = 0.03f;

        [Tooltip("Softness of the halo fade from inner to outer edge.")]
        [SerializeField] private float _moonHaloTerminator = 0.5f;

        [Tooltip("Tint used by the border halo around the moon disk.")]
        [SerializeField] private Color _borderHaloColor = new(0.72f, 0.82f, 1f, 1f);

        [Tooltip("Brightness of the border halo around the moon.")]
        [SerializeField] private float _borderHaloIntensity = 0.04f;

        [Tooltip("Inner start of the border halo, where 0 starts at the moon center and 1 reaches the moon edge.")]
        [SerializeField] private float _borderHaloInnerSize = 0f;

        [Tooltip("Outer reach of the border halo beyond the moon edge.")]
        [SerializeField] private float _borderHaloOuterSize = 0.015f;

        [Tooltip("Softness of the border halo fade from inner to outer edge.")]
        [SerializeField] private float _borderHaloTerminator = 0.25f;

        [Header("Horizon Illusion")]
        [Tooltip("Applies a perceptual size boost near the horizon so the moon feels larger when rising or setting.")]
        [SerializeField] private bool _enableHorizonMoonIllusion = true;

        [Tooltip("Maximum absolute lunar elevation, in degrees, where the horizon-size illusion fades out.")]
        [SerializeField] private float _horizonIllusionMaxElevation = 14f;

        [Tooltip("Multiplier applied to the moon disk size when the moon is on the horizon.")]
        [SerializeField] private float _horizonMoonSizeMultiplier = 1.65f;

        [Tooltip("Extra size curve applied only while the moon is rising. Use it to exaggerate the moon near moonrise without affecting moonset.")]
        [SerializeField] private AnimationCurve _moonRiseSizeCurve = DefaultMoonRiseSizeCurve();

        [Tooltip("Multiplier applied to moon disk intensity when the moon is on the horizon.")]
        [SerializeField] private float _horizonMoonIntensityMultiplier = 1.12f;

        [Tooltip("Multiplier applied to moon disk falloff when the moon is on the horizon. Lower values create a softer, wider disk.")]
        [SerializeField] private float _horizonMoonFalloffMultiplier = 0.68f;

        [Tooltip("How strongly the moon color warms near the horizon.")]
        [SerializeField] private float _horizonMoonWarmTintStrength = 0.35f;

        [Tooltip("Warm tint blended into the moon near the horizon.")]
        [SerializeField] private Color _horizonMoonTintColor = new(1f, 0.776f, 0.525f, 1f);

        [Tooltip("Extra warm-tint boost applied specifically while the moon is rising near the horizon.")]
        [SerializeField] private float _moonRiseWarmTintBoost = 1.75f;

        [Header("Altitude Atmosphere")]
        [Tooltip("Altitude where the atmospheric moon adjustments reach their maximum effect.")]
        [SerializeField] private float _altitudeAtmosphereMaxMeters = 3000f;

        [Tooltip("How much the horizon warm tint is reduced at high altitude.")]
        [SerializeField] private float _altitudeWarmTintReduction = 0.4f;

        [Tooltip("Extra clarity boost applied to the moon at high altitude.")]
        [SerializeField] private float _altitudeClarityBoost = 0.14f;

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

        [Tooltip("Formatted moon phase label for quick inspection.")]
        [SerializeField] private string _currentPhaseLabel = "Waxing Gibbous";

        [Tooltip("Current major moon phase bucket.")]
        [SerializeField] private MoonPhase _currentPhase = MoonPhase.WaxingGibbous;

        [Tooltip("Whether the moon is currently rising rather than setting.")]
        [SerializeField] private bool _isMoonRising;

        public LunarPositionData CurrentLunarData { get; private set; }

        public WeatherSolarController SolarController => _solarController;

        private static readonly int WeatherMoonDirectionShaderId = Shader.PropertyToID("_WeatherMoonDirection");
        private static readonly int WeatherMoonColorShaderId = Shader.PropertyToID("_WeatherMoonColor");
        private static readonly int WeatherMoonIntensityShaderId = Shader.PropertyToID("_WeatherMoonIntensity");
        private static readonly int WeatherMoonFalloffShaderId = Shader.PropertyToID("_WeatherMoonFalloff");
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
            if (_moonRiseSizeCurve == null || _moonRiseSizeCurve.length == 0)
            {
                _moonRiseSizeCurve = DefaultMoonRiseSizeCurve();
            }
            EnsureDefaults();
            UpdateLunarState();
        }

        private void EnsureDefaults()
        {
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
            _currentPhase = GetPhase(CurrentLunarData.PhaseAngleDegrees, CurrentLunarData.IlluminationFraction);
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
            float curveIntensity = _lightIntensityOverNight != null && _lightIntensityOverNight.length > 0
                ? Mathf.Max(0f, _lightIntensityOverNight.Evaluate(dayFraction))
                : 1f;
            float phaseIntensity = Mathf.Lerp(_minimumPhaseLight, 1f, _currentIlluminationFraction);
            float daylightSuppression = 1f;
            if (_solarController != null)
            {
                daylightSuppression = Mathf.Pow(1f - Mathf.Clamp01(_solarController.CurrentSolarData.DaylightFactor), Mathf.Max(0.01f, _daylightFadeStrength));
            }

            bool isNight = _solarController == null || _solarController.CurrentSolarData.DaylightFactor < 0.999f;
            float elevationVisibility = isNight
                ? 1f
                : Mathf.InverseLerp(_horizonDisableThreshold - 8f, 8f, _currentElevation);
            float visibleMoonPresence = isNight
                ? Mathf.InverseLerp(_horizonDisableThreshold, 12f, _currentElevation)
                : 1f;
            float visibleMoonIntensityBoost = Mathf.Lerp(1f, 1f + _currentIlluminationFraction, visibleMoonPresence);
            float horizonIllusion = EvaluateHorizonIllusionFactor();
            float altitudeAtmosphereFactor = EvaluateAltitudeAtmosphereFactor();
            float altitudeClarity = 1f + (_altitudeClarityBoost * altitudeAtmosphereFactor);
            _moonLight.intensity = _baseIntensity * curveIntensity * phaseIntensity * daylightSuppression * elevationVisibility * visibleMoonIntensityBoost * altitudeClarity;

            Color baseMoonColor = EvaluateBaseMoonColor(dayFraction);
            _moonLight.color = EvaluateHorizonTintedMoonColor(baseMoonColor, dayFraction, altitudeAtmosphereFactor);

            if (_disableLightBelowHorizon && !isNight)
            {
                _moonLight.enabled = _currentElevation > _horizonDisableThreshold && _moonLight.intensity > 0.0001f;
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
            if (!_driveSkyboxMaterial)
            {
                ApplySkyboxDefaults();
                return;
            }

            GetSkyboxPhasePreset(_currentPhase, out float skyboxPhaseAngle, out float skyboxIllumination);
            float phaseIntensity = Mathf.Lerp(_minimumPhaseLight, 1f, skyboxIllumination);
            float horizonIllusion = EvaluateHorizonIllusionFactor();
            float altitudeAtmosphereFactor = EvaluateAltitudeAtmosphereFactor();
            float altitudeClarity = 1f + (_altitudeClarityBoost * altitudeAtmosphereFactor);
            float dayFraction = GetCurrentDayFraction();
            Color baseMoonColor = EvaluateBaseMoonColor(dayFraction);
            Color moonColor = EvaluateHorizonTintedMoonColor(baseMoonColor, dayFraction, altitudeAtmosphereFactor);
            float moonIntensity = Mathf.Lerp(_skyboxMoonIntensity, _skyboxMoonIntensity * _horizonMoonIntensityMultiplier, horizonIllusion) * phaseIntensity * altitudeClarity;
            float moonFalloff = Mathf.Lerp(_skyboxMoonFalloff, _skyboxMoonFalloff * _horizonMoonFalloffMultiplier, horizonIllusion) * altitudeClarity;
            float risingSizeMultiplier = EvaluateMoonRiseSizeMultiplier(horizonIllusion);
            float moonSize = Mathf.Lerp(_skyboxMoonSize, _skyboxMoonSize * _horizonMoonSizeMultiplier * risingSizeMultiplier, horizonIllusion);
            float moonSkyVisibility = EvaluateMoonSkyVisibility();
            float darkMoonSkyVisibility = EvaluateDarkMoonSkyVisibility();

            Shader.SetGlobalVector(WeatherMoonDirectionShaderId, _currentMoonDirection.normalized);
            Shader.SetGlobalColor(WeatherMoonColorShaderId, moonColor);
            Shader.SetGlobalFloat(WeatherMoonIntensityShaderId, moonIntensity);
            Shader.SetGlobalFloat(WeatherMoonFalloffShaderId, moonFalloff);
            Shader.SetGlobalFloat(WeatherMoonSizeShaderId, moonSize);
            Shader.SetGlobalFloat(WeatherMoonPhaseAngleShaderId, skyboxPhaseAngle);
            Shader.SetGlobalFloat(WeatherMoonIlluminationShaderId, skyboxIllumination);
            Shader.SetGlobalFloat(WeatherMoonTextureExposureShaderId, _moonTextureExposure);
            Shader.SetGlobalFloat(WeatherMoonDarkTextureExposureShaderId, _darkMoonTextureExposure);
            Shader.SetGlobalFloat(WeatherMoonTerminatorSoftnessShaderId, _terminatorSoftness);
            Shader.SetGlobalFloat(WeatherMoonDarkSideVisibilityShaderId, _darkSideVisibility);
            Shader.SetGlobalColor(WeatherMoonHaloColorShaderId, _moonHaloColor);
            Shader.SetGlobalFloat(WeatherMoonHaloIntensityShaderId, _moonHaloIntensity);
            Shader.SetGlobalFloat(WeatherMoonHaloInnerSizeShaderId, _moonHaloInnerSize);
            Shader.SetGlobalFloat(WeatherMoonHaloOuterSizeShaderId, _moonHaloOuterSize);
            Shader.SetGlobalFloat(WeatherMoonHaloTerminatorShaderId, _moonHaloTerminator);
            Shader.SetGlobalColor(WeatherMoonBorderHaloColorShaderId, _borderHaloColor);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloIntensityShaderId, _borderHaloIntensity);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloInnerSizeShaderId, _borderHaloInnerSize);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloOuterSizeShaderId, _borderHaloOuterSize);
            Shader.SetGlobalFloat(WeatherMoonBorderHaloTerminatorShaderId, _borderHaloTerminator);
            Shader.SetGlobalFloat(WeatherMoonSkyVisibilityShaderId, moonSkyVisibility);
            Shader.SetGlobalFloat(WeatherMoonDarkSkyVisibilityShaderId, darkMoonSkyVisibility);
            ApplyMoonTexturesToShader();
        }

        private static void ApplySkyboxDefaults()
        {
            Shader.SetGlobalVector(WeatherMoonDirectionShaderId, Vector3.zero);
            Shader.SetGlobalColor(WeatherMoonColorShaderId, Color.black);
            Shader.SetGlobalFloat(WeatherMoonIntensityShaderId, 0f);
            Shader.SetGlobalFloat(WeatherMoonFalloffShaderId, 1400f);
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
            float fadeEnd = _daylightLitMoonFadeStart + Mathf.Max(0.01f, _daylightLitMoonFadeRange);
            float visibility = Mathf.InverseLerp(fadeEnd, _daylightLitMoonFadeStart, solarElevation);
            visibility = visibility * visibility * (3f - (2f * visibility));

            bool isSunSetting = _solarController.CurrentSolarData.HourAngleDegrees > 0f;
            if (isSunSetting)
            {
                visibility *= Mathf.Clamp01(1f - _duskLitMoonSuppression);
            }

            return visibility;
        }

        private float EvaluateDarkMoonSkyVisibility()
        {
            if (_solarController == null)
            {
                return 1f;
            }

            float solarElevation = _solarController.CurrentSolarData.ApparentElevationDegrees;
            float fadeRange = Mathf.Max(0.01f, _daylightShadowFadeRange);
            float fadeCenter = _daylightShadowFadeStart + (fadeRange * 0.5f);
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
            if (!_enableHorizonMoonIllusion)
            {
                return 0f;
            }

            float normalizedDistanceFromHorizon = Mathf.Clamp01(Mathf.Abs(_currentElevation) / _horizonIllusionMaxElevation);
            float proximityToHorizon = 1f - normalizedDistanceFromHorizon;
            return proximityToHorizon * proximityToHorizon * (3f - (2f * proximityToHorizon));
        }

        private float EvaluateMoonRiseSizeMultiplier(float horizonIllusion)
        {
            if (!_isMoonRising || _moonRiseSizeCurve == null || _moonRiseSizeCurve.length == 0)
            {
                return 1f;
            }

            return Mathf.Max(1f, _moonRiseSizeCurve.Evaluate(Mathf.Clamp01(horizonIllusion)));
        }

        private Color EvaluateHorizonTintedMoonColor(Color baseMoonColor, float dayFraction, float altitudeAtmosphereFactor)
        {
            float warmTintFactor = EvaluateHorizonWarmTintFactor();
            float tintReduction = 1f - (_altitudeWarmTintReduction * altitudeAtmosphereFactor);
            float tintAmount = Mathf.Clamp01(warmTintFactor * _horizonMoonWarmTintStrength * tintReduction);
            Color targetTint = EvaluateHorizonMoonTint();
            return Color.Lerp(baseMoonColor, targetTint, tintAmount);
        }

        private Color EvaluateBaseMoonColor(float dayFraction)
        {
            if (_lightColorOverNight != null && _lightColorOverNight.colorKeys.Length > 0)
            {
                return _lightColorOverNight.Evaluate(dayFraction);
            }

            return Color.white;
        }

        private Color EvaluateHorizonMoonTint() => _horizonMoonTintColor;

        private float EvaluateAltitudeAtmosphereFactor()
        {
            if (_solarController == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(_solarController.AltitudeMeters / _altitudeAtmosphereMaxMeters);
        }

        private float EvaluateHorizonWarmTintFactor()
        {
            float horizonWindow = Mathf.Max(_horizonIllusionMaxElevation, 0.1f);
            float proximityToHorizon = 1f - Mathf.Clamp01(Mathf.Abs(_currentElevation) / horizonWindow);
            float warmTintFactor = proximityToHorizon * proximityToHorizon;

            if (_isMoonRising)
            {
                warmTintFactor *= _moonRiseWarmTintBoost;
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

        private static MoonPhase GetPhase(float phaseAngleDegrees, float illuminationFraction)
        {
            float normalizedPhaseAngle = Mathf.Repeat(phaseAngleDegrees, 360f);

            if (illuminationFraction <= 0.12f || normalizedPhaseAngle < 22.5f || normalizedPhaseAngle >= 337.5f)
            {
                return MoonPhase.NewMoon;
            }

            if (illuminationFraction >= 0.88f || (normalizedPhaseAngle >= 157.5f && normalizedPhaseAngle < 202.5f))
            {
                return MoonPhase.FullMoon;
            }

            if (normalizedPhaseAngle < 67.5f)
            {
                return MoonPhase.WaxingCrescent;
            }

            if (normalizedPhaseAngle < 112.5f)
            {
                return MoonPhase.FirstQuarter;
            }

            if (normalizedPhaseAngle < 157.5f)
            {
                return MoonPhase.WaxingGibbous;
            }

            if (normalizedPhaseAngle < 247.5f)
            {
                return MoonPhase.WaningGibbous;
            }

            if (normalizedPhaseAngle < 292.5f)
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
                new Keyframe(0.45f, 1.42f),
                new Keyframe(0.72f, 1.95f),
                new Keyframe(1f, 2.45f));
        }
    }
}
