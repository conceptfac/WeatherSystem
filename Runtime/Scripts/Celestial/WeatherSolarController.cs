using System;
using UnityEngine;

namespace ConceptFactory.Weather
{
    /// <summary>
    /// Drives a directional light using approximate real-world solar astronomy.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Concept Factory/Weather System/Weather Solar Controller")]
    public sealed class WeatherSolarController : MonoBehaviour
    {
        public readonly struct SolarLightChangeData
        {
            public SolarLightChangeData(Light light, bool isEnabled, float intensity, Color color, float elevation, float daylightFactor, DateTime localDateTime)
            {
                Light = light;
                IsEnabled = isEnabled;
                Intensity = intensity;
                Color = color;
                Elevation = elevation;
                DaylightFactor = daylightFactor;
                LocalDateTime = localDateTime;
            }

            public Light Light { get; }
            public bool IsEnabled { get; }
            public float Intensity { get; }
            public Color Color { get; }
            public float Elevation { get; }
            public float DaylightFactor { get; }
            public DateTime LocalDateTime { get; }
        }

        public static Action<WeatherSolarController, SolarLightChangeData> OnSunLightChanged;
        public static bool HasSunLightState { get; private set; }
        public static SolarLightChangeData LastSunLightChangeData { get; private set; }
        public static WeatherSolarController ActiveInstance { get; private set; }

        [Header("References")]
        [Tooltip("Directional light that represents the Sun.")]
        [SerializeField] private Light _sunLight;

        [Tooltip("Optional transform override. If assigned, rotation is applied here instead of the Light transform.")]
        [SerializeField] private Transform _sunTransformOverride;

        [Tooltip("Optional color gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _lightColorOverDay;

        [Tooltip("Optional intensity curve sampled over the local 24-hour cycle.")]
        [SerializeField] private AnimationCurve _lightIntensityOverDay = DefaultIntensityCurve();

        [Header("Location")]
        [Tooltip("Latitude in degrees. North is positive, south is negative.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _latitude = -15.7939f;

        [Tooltip("Longitude in degrees. East is positive, west is negative.")]
        [Range(-180f, 180f)]
        [SerializeField] private float _longitude = -47.8828f;

        [Tooltip("Optional site altitude in meters. Reserved for future atmospheric extensions.")]
        [SerializeField] private float _altitudeMeters;

        [Tooltip("UTC offset used for the local civil clock, without automatic daylight saving adjustments.")]
        [Range(-12f, 14f)]
        [SerializeField] private float _utcOffsetHours = -3f;

        [Header("Date")]
        [Tooltip("Simulation year in the Gregorian calendar.")]
        [Range(1, 9999)]
        [SerializeField] private int _year = 2026;

        [Tooltip("Simulation month.")]
        [Range(1, 12)]
        [SerializeField] private int _month = 12;

        [Tooltip("Simulation day.")]
        [Range(1, 31)]
        [SerializeField] private int _day = 3;

        [Header("Local Time")]
        [Tooltip("Local civil time as total minutes from 00:00 to 23:59.")]
        [Range(0f, 1439f)]
        [SerializeField] private float _timeOfDayMinutes = 764f;

        [Header("Runtime")]
        [Tooltip("Automatically starts playback only when the application enters Play Mode at runtime.")]
        [SerializeField] private bool _playOnAwake = true;

        [Tooltip("Real-time minutes required to simulate a full 24-hour day.")]
        [Min(0.01f)]
        [SerializeField] private float _dayDurationMinutes = 5f;

        [Tooltip("Additional speed multiplier applied on top of the day duration.")]
        [Min(0f)]
        [HideInInspector]
        [SerializeField] private float _timeScaleMultiplier = 1f;

        [Header("Lighting")]
        [Tooltip("Base intensity multiplier before optional curve and daylight attenuation.")]
        [Min(0f)]
        [SerializeField] private float _baseIntensity = 1f;

        [Tooltip("Whether the light should be disabled once the Sun is sufficiently below the horizon.")]
        [SerializeField] private bool _disableLightAtNight = true;

        [Tooltip("Below this apparent elevation, the light is considered night and can be disabled.")]
        [Range(-18f, 5f)]
        [SerializeField] private float _nightDisableThreshold = -4f;

        [Header("Skybox")]
        [Tooltip("Whether this controller should also update the global weather sky shader values.")]
        [SerializeField] private bool _driveSkyboxMaterial = true;

        [Tooltip("Optional top sky gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _skyColorOverDay;

        [Tooltip("Optional horizon gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _horizonColorOverDay;

        [Tooltip("Optional ground hemisphere gradient sampled over the local 24-hour cycle.")]
        [SerializeField] private Gradient _groundColorOverDay;

        [Tooltip("Optional skybox intensity curve sampled over the local 24-hour cycle.")]
        [SerializeField] private AnimationCurve _skyIntensityOverDay = DefaultSkyIntensityCurve();

        [Tooltip("Optional sun disk color gradient used specifically by the weather sky shader.")]
        [SerializeField] private Gradient _skyboxSunColorOverDay;

        [Tooltip("Global sky top falloff sent to the weather sky shader.")]
        [Min(0.01f)]
        [SerializeField] private float _skyTopFalloff = 8.7f;

        [Tooltip("Global sky bottom falloff sent to the weather sky shader.")]
        [Min(0.01f)]
        [SerializeField] private float _skyBottomFalloff = 22.3f;

        [Tooltip("Global sun disk intensity multiplier sent to the weather sky shader.")]
        [Min(0f)]
        [SerializeField] private float _skyboxSunIntensity = 20f;

        [Tooltip("Global sun disk sharpness sent to the weather sky shader.")]
        [Min(1f)]
        [SerializeField] private float _skyboxSunFalloff = 5000f;

        [Tooltip("Global sun disk size sent to the weather sky shader.")]
        [Min(0f)]
        [SerializeField] private float _skyboxSunSize = 1f;

        [Tooltip("Overall visual size of procedural stars rendered in the sky shader.")]
        [Min(0f)]
        [SerializeField] private float _starSize = 0.5f;

        [Tooltip("Controls how many stars are visible in the procedural night sky.")]
        [Min(0f)]
        [SerializeField] private float _starDensity = 4f;

        [Tooltip("Gradient used to randomize star colors across the night sky.")]
        [SerializeField] private Gradient _starColorGradient;

        [Tooltip("Controls how much the stars pulse in brightness over time.")]
        [Min(0f)]
        [SerializeField] private float _starTwinkleAmount = 8f;

        [Tooltip("Controls how much some stars briefly shift color while twinkling.")]
        [Min(0f)]
        [SerializeField] private float _starColorFlicker = 1f;

        [Tooltip("Boosts chromatic twinkle closer to the horizon, mimicking atmospheric dispersion.")]
        [Min(0f)]
        [SerializeField] private float _starHorizonChromaticShift = 0.509f;

        [Header("Horizon Illusion")]
        [Tooltip("Applies a perceptual boost near the horizon so the sun disk feels larger at sunrise and sunset.")]
        [SerializeField] private bool _enableHorizonSunIllusion = true;

        [Tooltip("Maximum absolute solar elevation, in degrees, where the horizon-size illusion fades out.")]
        [Range(0.1f, 30f)]
        [SerializeField] private float _horizonIllusionMaxElevation = 12f;

        [Tooltip("Multiplier applied to the sun disk size when the sun is on the horizon.")]
        [Min(1f)]
        [SerializeField] private float _horizonSunSizeMultiplier = 1.8f;

        [Tooltip("Optional size curve sampled over the local 24-hour cycle. This multiplier is applied on top of the base sun size and horizon illusion.")]
        [SerializeField] private AnimationCurve _skyboxSunSizeOverDay = DefaultSunSizeCurve();

        [Tooltip("Multiplier applied to sun disk intensity when the sun is on the horizon.")]
        [Min(0f)]
        [SerializeField] private float _horizonSunIntensityMultiplier = 1.15f;

        [Tooltip("Multiplier applied to sun disk falloff when the sun is on the horizon. Lower values create a softer, wider disk.")]
        [Min(0.01f)]
        [SerializeField] private float _horizonSunFalloffMultiplier = 0.55f;

        [Header("Debug")]
        [Tooltip("Latest apparent solar elevation in degrees.")]
        [SerializeField] private float _currentElevation;

        [Tooltip("Latest solar azimuth in degrees clockwise from north.")]
        [SerializeField] private float _currentAzimuth;

        [Tooltip("Smoothed daylight factor from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _currentDaylightFactor;

        [Tooltip("Current unit vector from world origin toward the Sun.")]
        [SerializeField] private Vector3 _currentSunDirection = Vector3.up;

        [Tooltip("Formatted local time for quick inspection.")]
        [SerializeField] private string _currentLocalTimeLabel = "12:00";

        [Tooltip("Current simulation playback state.")]
        [SerializeField] private SimulationPlaybackState _playbackState = SimulationPlaybackState.Stopped;

        private DateTime _localDateTime;
        private SimulationSnapshot _defaultSimulationSnapshot;
        private bool _hasDefaultSimulationSnapshot;
        private static readonly int WeatherSunDirectionShaderId = Shader.PropertyToID("_WeatherSunDirection");
        private static readonly int WeatherSunColorShaderId = Shader.PropertyToID("_WeatherSunColor");
        private static readonly int WeatherAmbientSkyShaderId = Shader.PropertyToID("_WeatherAmbientSky");
        private static readonly int WeatherAmbientHorizonShaderId = Shader.PropertyToID("_WeatherAmbientHorizon");
        private static readonly int WeatherAmbientGroundShaderId = Shader.PropertyToID("_WeatherAmbientGround");
        private static readonly int WeatherSkyIntensityShaderId = Shader.PropertyToID("_WeatherSkyIntensity");
        private static readonly int WeatherSunIntensityShaderId = Shader.PropertyToID("_WeatherSunIntensity");
        private static readonly int WeatherSunFalloffShaderId = Shader.PropertyToID("_WeatherSunFalloff");
        private static readonly int WeatherSunSizeShaderId = Shader.PropertyToID("_WeatherSunSize");
        private static readonly int WeatherTopSkyFalloffShaderId = Shader.PropertyToID("_WeatherTopSkyFalloff");
        private static readonly int WeatherBottomSkyFalloffShaderId = Shader.PropertyToID("_WeatherBottomSkyFalloff");
        private static readonly int WeatherNightValueShaderId = Shader.PropertyToID("_WeatherNightValue");
        private static readonly int WeatherStarSizeShaderId = Shader.PropertyToID("_WeatherStarSize");
        private static readonly int WeatherStarDensityShaderId = Shader.PropertyToID("_WeatherStarDensity");
        private static readonly int WeatherStarColorRampShaderId = Shader.PropertyToID("_WeatherStarColorRamp");
        private static readonly int WeatherStarTwinkleAmountShaderId = Shader.PropertyToID("_WeatherStarTwinkleAmount");
        private static readonly int WeatherStarColorFlickerShaderId = Shader.PropertyToID("_WeatherStarColorFlicker");
        private static readonly int WeatherStarHorizonChromaticShiftShaderId = Shader.PropertyToID("_WeatherStarHorizonChromaticShift");
        private static readonly int WeatherSkyTimeShaderId = Shader.PropertyToID("_WeatherSkyTime");
        private Texture2D _starColorRampTexture;
        private bool _lastNotifiedSunEnabled;
        private float _lastNotifiedSunIntensity = float.MinValue;
        private Color _lastNotifiedSunColor = new(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
        private float _lastNotifiedDaylightFactor = float.MinValue;
        private bool _hasNotifiedSunState;
#if UNITY_EDITOR
        private double _lastEditorTime;
#endif

        public SolarPositionData CurrentSolarData { get; private set; }

        public DateTime LocalDateTime => _localDateTime;

        public float Latitude => _latitude;

        public float Longitude => _longitude;

        public float UtcOffsetHours => _utcOffsetHours;

        public float AltitudeMeters => _altitudeMeters;

        public bool IsPlayingSimulation => _playbackState == SimulationPlaybackState.Playing;

        public bool IsPausedSimulation => _playbackState == SimulationPlaybackState.Paused;

        public bool IsStoppedSimulation => _playbackState == SimulationPlaybackState.Stopped;

        public static bool TryGetCurrentSunLightState(out SolarLightChangeData data)
        {
            if (HasSunLightState)
            {
                data = LastSunLightChangeData;
                return true;
            }

            if (ActiveInstance != null)
            {
                data = ActiveInstance.BuildSolarLightChangeData();
                return true;
            }

            data = default;
            return false;
        }

        private void Reset()
        {
            _sunLight = GetComponent<Light>();
            EnsureDirectionalLightReference();
            if (_lightColorOverDay == null || _lightColorOverDay.colorKeys.Length == 0)
            {
                _lightColorOverDay = CreateDefaultGradient();
            }

            if (_skyColorOverDay == null || _skyColorOverDay.colorKeys.Length == 0)
            {
                _skyColorOverDay = CreateDefaultSkyGradient();
            }

            if (_horizonColorOverDay == null || _horizonColorOverDay.colorKeys.Length == 0)
            {
                _horizonColorOverDay = CreateDefaultHorizonGradient();
            }

            if (_groundColorOverDay == null || _groundColorOverDay.colorKeys.Length == 0)
            {
                _groundColorOverDay = CreateDefaultGroundGradient();
            }

            if (_skyboxSunColorOverDay == null || _skyboxSunColorOverDay.colorKeys.Length == 0)
            {
                _skyboxSunColorOverDay = CreateDefaultSkyboxSunGradient();
            }

            if (_starColorGradient == null || _starColorGradient.colorKeys.Length == 0)
            {
                _starColorGradient = CreateDefaultStarGradient();
            }
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            EnsureDefaults();
            SyncDateTimeFromInspector();
            CaptureDefaultSimulationSnapshot();

            if (Application.isPlaying)
            {
                _playbackState = _playOnAwake ? SimulationPlaybackState.Playing : SimulationPlaybackState.Stopped;
            }
#if UNITY_EDITOR
            SubscribeEditorUpdate();
            _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            UpdateSolarState();
        }

        private void OnDisable()
        {
            NotifySunLightChanged(force: true);
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
#if UNITY_EDITOR
            UnsubscribeEditorUpdate();
#endif
            ReleaseStarColorRampTexture();
        }

        private void Update()
        {
            EnsureDefaults();

            if (Application.isPlaying)
            {
                if (_playbackState == SimulationPlaybackState.Playing)
                {
                    AdvanceSimulatedTime(Time.deltaTime);
                }
                else
                {
                    SyncDateTimeFromInspector();
                }
            }
            else if (_playbackState != SimulationPlaybackState.Playing)
            {
                SyncDateTimeFromInspector();
            }

            if (_playbackState == SimulationPlaybackState.Stopped)
            {
                CaptureDefaultSimulationSnapshot();
            }

            UpdateSolarState();
        }

        private void OnValidate()
        {
            _dayDurationMinutes = Mathf.Max(0.01f, _dayDurationMinutes);
            _timeScaleMultiplier = Mathf.Max(0f, _timeScaleMultiplier);
            _baseIntensity = Mathf.Max(0f, _baseIntensity);
            _skyTopFalloff = Mathf.Max(0.01f, _skyTopFalloff);
            _skyBottomFalloff = Mathf.Max(0.01f, _skyBottomFalloff);
            _skyboxSunIntensity = Mathf.Max(0f, _skyboxSunIntensity);
            _skyboxSunFalloff = Mathf.Max(1f, _skyboxSunFalloff);
            _skyboxSunSize = Mathf.Max(0f, _skyboxSunSize);
            _starSize = Mathf.Max(0f, _starSize);
            _starDensity = Mathf.Max(0f, _starDensity);
            _starTwinkleAmount = Mathf.Max(0f, _starTwinkleAmount);
            _starColorFlicker = Mathf.Max(0f, _starColorFlicker);
            _starHorizonChromaticShift = Mathf.Max(0f, _starHorizonChromaticShift);
            _horizonIllusionMaxElevation = Mathf.Max(0.1f, _horizonIllusionMaxElevation);
            _horizonSunSizeMultiplier = Mathf.Max(1f, _horizonSunSizeMultiplier);
            _horizonSunIntensityMultiplier = Mathf.Max(0f, _horizonSunIntensityMultiplier);
            _horizonSunFalloffMultiplier = Mathf.Max(0.01f, _horizonSunFalloffMultiplier);
            _day = Mathf.Clamp(_day, 1, DateTime.DaysInMonth(Mathf.Clamp(_year, 1, 9999), Mathf.Clamp(_month, 1, 12)));
            _timeOfDayMinutes = Mathf.Clamp(_timeOfDayMinutes, 0f, 1439f);

            EnsureDefaults();
            SyncDateTimeFromInspector();

            if (_playbackState == SimulationPlaybackState.Stopped)
            {
                CaptureDefaultSimulationSnapshot();
            }

            UpdateSolarState();
        }

        public void PlaySimulation()
        {
            if (_playbackState == SimulationPlaybackState.Stopped)
            {
                CaptureDefaultSimulationSnapshot();
            }

            _playbackState = SimulationPlaybackState.Playing;
            RefreshEditorState();
        }

        public void PauseSimulation()
        {
            if (_playbackState == SimulationPlaybackState.Stopped)
            {
                CaptureDefaultSimulationSnapshot();
            }

            _playbackState = SimulationPlaybackState.Paused;
            RefreshEditorState();
        }

        public void StopSimulation()
        {
            RestoreDefaultSimulationSnapshot();
            _playbackState = SimulationPlaybackState.Stopped;
            UpdateSolarState();
            RefreshEditorState();
        }

        private void EnsureDefaults()
        {
            EnsureDirectionalLightReference();

            if (_lightColorOverDay == null || _lightColorOverDay.colorKeys.Length == 0)
            {
                _lightColorOverDay = CreateDefaultGradient();
            }

            if (_lightIntensityOverDay == null || _lightIntensityOverDay.length == 0)
            {
                _lightIntensityOverDay = DefaultIntensityCurve();
            }

            if (_skyColorOverDay == null || _skyColorOverDay.colorKeys.Length == 0)
            {
                _skyColorOverDay = CreateDefaultSkyGradient();
            }

            if (_horizonColorOverDay == null || _horizonColorOverDay.colorKeys.Length == 0)
            {
                _horizonColorOverDay = CreateDefaultHorizonGradient();
            }

            if (_groundColorOverDay == null || _groundColorOverDay.colorKeys.Length == 0)
            {
                _groundColorOverDay = CreateDefaultGroundGradient();
            }

            if (_skyboxSunColorOverDay == null || _skyboxSunColorOverDay.colorKeys.Length == 0)
            {
                _skyboxSunColorOverDay = CreateDefaultSkyboxSunGradient();
            }

            if (_skyIntensityOverDay == null || _skyIntensityOverDay.length == 0)
            {
                _skyIntensityOverDay = DefaultSkyIntensityCurve();
            }

            if (_skyboxSunSizeOverDay == null || _skyboxSunSizeOverDay.length == 0)
            {
                _skyboxSunSizeOverDay = DefaultSunSizeCurve();
            }

            if (_starColorGradient == null || _starColorGradient.colorKeys.Length == 0)
            {
                _starColorGradient = CreateDefaultStarGradient();
            }
        }

        private void AdvanceSimulatedTime(float deltaTime)
        {
            if (deltaTime <= 0f || _timeScaleMultiplier <= 0f)
            {
                return;
            }

            double simulatedSecondsPerSecond = (86400.0 / (_dayDurationMinutes * 60.0)) * _timeScaleMultiplier;
            _localDateTime = _localDateTime.AddSeconds(deltaTime * simulatedSecondsPerSecond);
            SyncInspectorFromDateTime();
        }

        private void SyncDateTimeFromInspector()
        {
            int clampedYear = Mathf.Clamp(_year, 1, 9999);
            int clampedMonth = Mathf.Clamp(_month, 1, 12);
            int clampedDay = Mathf.Clamp(_day, 1, DateTime.DaysInMonth(clampedYear, clampedMonth));

            _localDateTime = new DateTime(
                clampedYear,
                clampedMonth,
                clampedDay,
                0,
                0,
                0,
                DateTimeKind.Unspecified);

            _localDateTime = _localDateTime.AddMinutes(Mathf.Clamp(_timeOfDayMinutes, 0f, 1439f));

            _year = _localDateTime.Year;
            _month = _localDateTime.Month;
            _day = _localDateTime.Day;
            _timeOfDayMinutes = (_localDateTime.Hour * 60f) + _localDateTime.Minute;
        }

        private void SyncInspectorFromDateTime()
        {
            _year = _localDateTime.Year;
            _month = _localDateTime.Month;
            _day = _localDateTime.Day;
            _timeOfDayMinutes = (_localDateTime.Hour * 60f) + _localDateTime.Minute;
        }

        private void CaptureDefaultSimulationSnapshot()
        {
            _defaultSimulationSnapshot = new SimulationSnapshot(_year, _month, _day, _timeOfDayMinutes);
            _hasDefaultSimulationSnapshot = true;
        }

        private void RestoreDefaultSimulationSnapshot()
        {
            if (!_hasDefaultSimulationSnapshot)
            {
                CaptureDefaultSimulationSnapshot();
            }

            _year = _defaultSimulationSnapshot.Year;
            _month = _defaultSimulationSnapshot.Month;
            _day = _defaultSimulationSnapshot.Day;
            _timeOfDayMinutes = _defaultSimulationSnapshot.TimeOfDayMinutes;
            SyncDateTimeFromInspector();
        }

        private void UpdateSolarState()
        {
            CurrentSolarData = SolarPositionCalculator.CalculateSolarPosition(_localDateTime, _latitude, _longitude, _utcOffsetHours);
            _currentSunDirection = CurrentSolarData.SunDirection;
            _currentElevation = CurrentSolarData.ApparentElevationDegrees;
            _currentAzimuth = CurrentSolarData.AzimuthDegrees;
            _currentDaylightFactor = CurrentSolarData.DaylightFactor;
            _currentLocalTimeLabel = _localDateTime.ToString("HH:mm");

            ApplyLightTransform(CurrentSolarData.SunDirection);
            ApplyLightAppearance();
            ApplySkyboxAppearance();
        }

        private void ApplyLightTransform(Vector3 sunDirection)
        {
            Transform targetTransform = GetTargetTransform();
            if (targetTransform == null)
            {
                return;
            }

            Vector3 lightForward = -sunDirection;
            if (lightForward.sqrMagnitude < 0.000001f)
            {
                return;
            }

            targetTransform.rotation = Quaternion.LookRotation(lightForward.normalized, Vector3.up);
        }

        private void ApplyLightAppearance()
        {
            if (_sunLight == null)
            {
                NotifySunLightChanged(force: true);
                return;
            }

            float dayFraction = GetCurrentDayFraction();
            float curveIntensity = _lightIntensityOverDay != null && _lightIntensityOverDay.length > 0
                ? Mathf.Max(0f, _lightIntensityOverDay.Evaluate(dayFraction))
                : 1f;

            _sunLight.intensity = _baseIntensity * curveIntensity * _currentDaylightFactor;

            if (_lightColorOverDay != null)
            {
                _sunLight.color = _lightColorOverDay.Evaluate(dayFraction);
            }

            if (_disableLightAtNight)
            {
                _sunLight.enabled = _currentElevation > _nightDisableThreshold;
            }
            else if (!_sunLight.enabled)
            {
                _sunLight.enabled = true;
            }

            NotifySunLightChanged();
        }

        private void ApplySkyboxAppearance()
        {
            if (!_driveSkyboxMaterial)
            {
                return;
            }

            float dayFraction = GetCurrentDayFraction();
            Color sunColor = _skyboxSunColorOverDay != null && _skyboxSunColorOverDay.colorKeys.Length > 0
                ? _skyboxSunColorOverDay.Evaluate(dayFraction)
                : (_sunLight != null ? _sunLight.color : Color.white);

            float skyIntensity = _skyIntensityOverDay != null && _skyIntensityOverDay.length > 0
                ? Mathf.Max(0f, _skyIntensityOverDay.Evaluate(dayFraction))
                : 1f;
            float sunSizeOverDay = _skyboxSunSizeOverDay != null && _skyboxSunSizeOverDay.length > 0
                ? Mathf.Max(0f, _skyboxSunSizeOverDay.Evaluate(dayFraction))
                : 1f;

            float horizonIllusion = EvaluateHorizonIllusionFactor();
            float sunIntensity = Mathf.Lerp(_skyboxSunIntensity, _skyboxSunIntensity * _horizonSunIntensityMultiplier, horizonIllusion);
            float sunFalloff = Mathf.Lerp(_skyboxSunFalloff, _skyboxSunFalloff * _horizonSunFalloffMultiplier, horizonIllusion);
            float sunSize = Mathf.Lerp(_skyboxSunSize, _skyboxSunSize * _horizonSunSizeMultiplier, horizonIllusion) * sunSizeOverDay;
            Texture2D starColorRamp = GetOrCreateStarColorRampTexture();

            Shader.SetGlobalVector(WeatherSunDirectionShaderId, _currentSunDirection.normalized);
            Shader.SetGlobalColor(WeatherSunColorShaderId, sunColor);
            Shader.SetGlobalColor(WeatherAmbientSkyShaderId, _skyColorOverDay.Evaluate(dayFraction));
            Shader.SetGlobalColor(WeatherAmbientHorizonShaderId, _horizonColorOverDay.Evaluate(dayFraction));
            Shader.SetGlobalColor(WeatherAmbientGroundShaderId, _groundColorOverDay.Evaluate(dayFraction));
            Shader.SetGlobalFloat(WeatherSkyIntensityShaderId, skyIntensity);
            Shader.SetGlobalFloat(WeatherSunIntensityShaderId, sunIntensity);
            Shader.SetGlobalFloat(WeatherSunFalloffShaderId, sunFalloff);
            Shader.SetGlobalFloat(WeatherSunSizeShaderId, sunSize);
            Shader.SetGlobalFloat(WeatherTopSkyFalloffShaderId, _skyTopFalloff);
            Shader.SetGlobalFloat(WeatherBottomSkyFalloffShaderId, _skyBottomFalloff);
            Shader.SetGlobalFloat(WeatherNightValueShaderId, 1f - _currentDaylightFactor);
            Shader.SetGlobalFloat(WeatherStarSizeShaderId, _starSize);
            Shader.SetGlobalFloat(WeatherStarDensityShaderId, _starDensity);
            Shader.SetGlobalFloat(WeatherStarTwinkleAmountShaderId, _starTwinkleAmount);
            Shader.SetGlobalFloat(WeatherStarColorFlickerShaderId, _starColorFlicker);
            Shader.SetGlobalFloat(WeatherStarHorizonChromaticShiftShaderId, _starHorizonChromaticShift);
            Shader.SetGlobalFloat(WeatherSkyTimeShaderId, GetSkyShaderTime());
            if (starColorRamp != null)
            {
                Shader.SetGlobalTexture(WeatherStarColorRampShaderId, starColorRamp);
            }

            DynamicGI.UpdateEnvironment();
        }

        private Texture2D GetOrCreateStarColorRampTexture()
        {
            if (_starColorRampTexture == null)
            {
                _starColorRampTexture = new Texture2D(128, 1, TextureFormat.RGBA32, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            for (int index = 0; index < _starColorRampTexture.width; index++)
            {
                float t = index / (float)(_starColorRampTexture.width - 1);
                _starColorRampTexture.SetPixel(index, 0, _starColorGradient.Evaluate(t));
            }

            _starColorRampTexture.Apply(false, false);
            return _starColorRampTexture;
        }

        private void ReleaseStarColorRampTexture()
        {
            if (_starColorRampTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_starColorRampTexture);
            }
            else
            {
                DestroyImmediate(_starColorRampTexture);
            }

            _starColorRampTexture = null;
        }

        private float EvaluateHorizonIllusionFactor()
        {
            if (!_enableHorizonSunIllusion)
            {
                return 0f;
            }

            float normalizedDistanceFromHorizon = Mathf.Clamp01(Mathf.Abs(_currentElevation) / _horizonIllusionMaxElevation);
            float proximityToHorizon = 1f - normalizedDistanceFromHorizon;
            return proximityToHorizon * proximityToHorizon * (3f - (2f * proximityToHorizon));
        }

        private Transform GetTargetTransform()
        {
            if (_sunTransformOverride != null)
            {
                return _sunTransformOverride;
            }

            return _sunLight != null ? _sunLight.transform : transform;
        }

        private float GetCurrentDayFraction()
        {
            double seconds = _localDateTime.TimeOfDay.TotalSeconds / 86400.0;
            return (float)Math.Clamp(seconds, 0.0, 1.0);
        }

        private float GetSkyShaderTime()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)(UnityEditor.EditorApplication.timeSinceStartup % 10000.0);
            }
#endif
            return Mathf.Repeat(Time.time, 10000f);
        }

        private void EnsureDirectionalLightReference()
        {
            if (_sunLight == null)
            {
                _sunLight = GetComponent<Light>();
            }

            if (_sunLight != null && _sunLight.type != LightType.Directional)
            {
                _sunLight.type = LightType.Directional;
            }

            if (_sunLight != null && RenderSettings.sun == null)
            {
                RenderSettings.sun = _sunLight;
            }
        }

        private void RefreshEditorState()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);

                if (_sunLight != null)
                {
                    UnityEditor.EditorUtility.SetDirty(_sunLight);
                }

                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        private void NotifySunLightChanged(bool force = false)
        {
            bool isEnabled = _sunLight != null && _sunLight.enabled;
            float intensity = _sunLight != null ? _sunLight.intensity : 0f;
            Color color = _sunLight != null ? _sunLight.color : Color.black;

            bool changed = force ||
                           !_hasNotifiedSunState ||
                           _lastNotifiedSunEnabled != isEnabled ||
                           !Mathf.Approximately(_lastNotifiedSunIntensity, intensity) ||
                           !ColorsApproximatelyEqual(_lastNotifiedSunColor, color) ||
                           !Mathf.Approximately(_lastNotifiedDaylightFactor, _currentDaylightFactor);

            if (!changed)
            {
                return;
            }

            _hasNotifiedSunState = true;
            _lastNotifiedSunEnabled = isEnabled;
            _lastNotifiedSunIntensity = intensity;
            _lastNotifiedSunColor = color;
            _lastNotifiedDaylightFactor = _currentDaylightFactor;
            HasSunLightState = true;
            LastSunLightChangeData = BuildSolarLightChangeData();

            OnSunLightChanged?.Invoke(this, LastSunLightChangeData);
        }

        private SolarLightChangeData BuildSolarLightChangeData()
        {
            bool isEnabled = _sunLight != null && _sunLight.enabled;
            float intensity = _sunLight != null ? _sunLight.intensity : 0f;
            Color color = _sunLight != null ? _sunLight.color : Color.black;
            return new SolarLightChangeData(_sunLight, isEnabled, intensity, color, _currentElevation, _currentDaylightFactor, _localDateTime);
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }

#if UNITY_EDITOR
        private void SubscribeEditorUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            UnsubscribeEditorUpdate();
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        private void UnsubscribeEditorUpdate()
        {
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (this == null || !isActiveAndEnabled || Application.isPlaying)
            {
                return;
            }

            double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Max(0f, (float)(editorTime - _lastEditorTime));
            _lastEditorTime = editorTime;

            if (_playbackState == SimulationPlaybackState.Playing)
            {
                AdvanceSimulatedTime(deltaTime);
            }
            else if (_playbackState != SimulationPlaybackState.Playing)
            {
                SyncDateTimeFromInspector();
            }

            if (_playbackState == SimulationPlaybackState.Stopped)
            {
                CaptureDefaultSimulationSnapshot();
            }

            UpdateSolarState();

            if (_sunLight != null)
            {
                UnityEditor.EditorUtility.SetDirty(_sunLight);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        private static AnimationCurve DefaultIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.23f, 0f),
                new Keyframe(0.28f, 0.5f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.72f, 0.5f),
                new Keyframe(0.77f, 0f),
                new Keyframe(1f, 0f));
        }

        private static AnimationCurve DefaultSkyIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.03f),
                new Keyframe(0.22f, 0.08f),
                new Keyframe(0.27f, 0.55f),
                new Keyframe(0.5f, 1.1f),
                new Keyframe(0.73f, 0.55f),
                new Keyframe(0.78f, 0.08f),
                new Keyframe(1f, 0.03f));
        }

        private static AnimationCurve DefaultSunSizeCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.22f, 1f),
                new Keyframe(0.27f, 1.18f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.73f, 1.18f),
                new Keyframe(0.78f, 1f),
                new Keyframe(1f, 1f));
        }

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.07f, 0.1f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.56f, 0.28f), 0.24f),
                    new GradientColorKey(new Color(1f, 0.5803922f, 0.29803923f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.58f, 0.3f), 0.76f),
                    new GradientColorKey(new Color(0.07f, 0.1f, 0.18f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultSkyGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.02f, 0.03f, 0.07f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.22f, 0.38f), 0.22f),
                    new GradientColorKey(new Color(0.9f, 0.46f, 0.28f), 0.26f),
                    new GradientColorKey(new Color(0.25f, 0.49f, 0.84f), 0.5f),
                    new GradientColorKey(new Color(0.92f, 0.48f, 0.3f), 0.74f),
                    new GradientColorKey(new Color(0.18f, 0.22f, 0.38f), 0.78f),
                    new GradientColorKey(new Color(0.02f, 0.03f, 0.07f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultHorizonGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.42f, 0.32f, 0.36f), 0.22f),
                    new GradientColorKey(new Color(1f, 0.63f, 0.4f), 0.26f),
                    new GradientColorKey(new Color(0.84f, 0.9f, 0.98f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.38f), 0.74f),
                    new GradientColorKey(new Color(0.42f, 0.32f, 0.36f), 0.78f),
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultGroundGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.01f, 0.015f, 0.03f), 0f),
                    new GradientColorKey(new Color(0.08f, 0.08f, 0.12f), 0.24f),
                    new GradientColorKey(new Color(0.2f, 0.18f, 0.18f), 0.28f),
                    new GradientColorKey(new Color(0.21f, 0.24f, 0.29f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.17f, 0.17f), 0.72f),
                    new GradientColorKey(new Color(0.08f, 0.08f, 0.12f), 0.76f),
                    new GradientColorKey(new Color(0.01f, 0.015f, 0.03f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultSkyboxSunGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.42f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.62f, 0.3f), 0.22f),
                    new GradientColorKey(new Color(1f, 0.73168916f, 0.3915094f), 0.26f),
                    new GradientColorKey(new Color(1f, 0.80919236f, 0.495283f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.76f, 0.48f), 0.74f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.28f), 0.78f),
                    new GradientColorKey(new Color(0.95f, 0.42f, 0.18f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultStarGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.62f, 0.72f, 1f), 0f),
                    new GradientColorKey(new Color(0.84f, 0.9f, 1f), 0.35f),
                    new GradientColorKey(new Color(1f, 0.96f, 0.9f), 0.65f),
                    new GradientColorKey(new Color(1f, 0.87f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private readonly struct SimulationSnapshot
        {
            public SimulationSnapshot(int year, int month, int day, float timeOfDayMinutes)
            {
                Year = year;
                Month = month;
                Day = day;
                TimeOfDayMinutes = timeOfDayMinutes;
            }

            public int Year { get; }
            public int Month { get; }
            public int Day { get; }
            public float TimeOfDayMinutes { get; }
        }

        public enum SimulationPlaybackState
        {
            Stopped,
            Playing,
            Paused
        }
    }
}
