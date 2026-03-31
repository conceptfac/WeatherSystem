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
        [SerializeField] private float _latitude = -12.9777f;

        [Tooltip("Longitude in degrees. East is positive, west is negative.")]
        [Range(-180f, 180f)]
        [SerializeField] private float _longitude = -38.5016f;

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
        [SerializeField] private int _month = 3;

        [Tooltip("Simulation day.")]
        [Range(1, 31)]
        [SerializeField] private int _day = 30;

        [Header("Local Time")]
        [Tooltip("Local civil time as total minutes from 00:00 to 23:59.")]
        [Range(0f, 1439f)]
        [SerializeField] private float _timeOfDayMinutes = 720f;

        [Header("Simulation")]
        [Tooltip("Automatically starts playback only when the application enters Play Mode at runtime.")]
        [SerializeField] private bool _playOnAwake = true;

        [Tooltip("Real-time minutes required to simulate a full 24-hour day.")]
        [Min(0.01f)]
        [SerializeField] private float _dayDurationMinutes = 10f;

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
#if UNITY_EDITOR
        private double _lastEditorTime;
#endif

        public SolarPositionData CurrentSolarData { get; private set; }

        public DateTime LocalDateTime => _localDateTime;

        public float AltitudeMeters => _altitudeMeters;

        public bool IsPlayingSimulation => _playbackState == SimulationPlaybackState.Playing;

        public bool IsPausedSimulation => _playbackState == SimulationPlaybackState.Paused;

        public bool IsStoppedSimulation => _playbackState == SimulationPlaybackState.Stopped;

        private void Reset()
        {
            _sunLight = GetComponent<Light>();
            EnsureDirectionalLightReference();
            if (_lightColorOverDay == null || _lightColorOverDay.colorKeys.Length == 0)
            {
                _lightColorOverDay = CreateDefaultGradient();
            }
        }

        private void OnEnable()
        {
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
#if UNITY_EDITOR
            UnsubscribeEditorUpdate();
#endif
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

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.07f, 0.1f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.56f, 0.28f), 0.24f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.9f), 0.5f),
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
