using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using ConceptFactory.Weather;
using WeatherVolumetricClouds = global::VolumetricClouds; // Package URP VolumeComponent (not UnityEngine.Rendering.VolumetricClouds).

namespace ConceptFactory.Weather.Atmosphere
{
    /// <summary>
    /// URP volumetric clouds vs cloud plane (<see cref="NimbusCloudRenderMode"/>). Solar time: <see cref="WeatherSolarController.ActiveInstance"/>.
    /// Optional <see cref="Volume"/> — when empty, resolves <c>Camera.main.GetComponent&lt;Volume&gt;()</c> (adds Volume on Main Camera at runtime if missing). Used in volumetric mode only.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Concept Factory/Weather System/Nimbus Cloud Controller")]
    public sealed class NimbusCloudController : MonoBehaviour
    {

        [SerializeField] private NimbusCloudRenderMode _renderMode = NimbusCloudRenderMode.CloudPlane;

        [Tooltip("Optional. When empty, resolves Volume on the Main Camera (Camera.main / MainCamera tag).")]
        [SerializeField] private Volume _volume;

        [Tooltip("When enabled, applies wind speed and orientation curves from solar local time when the sun/light state updates (WeatherSolarController). Density uses Volumetric Clouds densityMultiplier only.")]
        [SerializeField] private bool _syncDensityWindFromSolarCurves;

        [SerializeField] private AnimationCurve _globalWindSpeedKmhOverDay = AnimationCurve.Linear(0f, 15f, 1f, 15f);

        [SerializeField] private AnimationCurve _globalWindOrientationOverDay = AnimationCurve.Linear(0f, 0f, 1f, 360f);

        private WeatherVolumetricClouds _clouds;
        private bool _loggedMissingClouds;
        private bool _loggedMissingVolume;

        private Action<WeatherSolarController, WeatherSolarController.SolarLightChangeData> _onSunLightChangedHandler;

        /// <summary>Explicit Volume reference (null = resolve Main Camera).</summary>
        public Volume VolumeReference => _volume;

        /// <summary>
        /// Re-applies volumetric Volume + Volumetric Clouds override. Call after inspector edits when <see cref="OnValidate"/> does not run (UI Toolkit inspectors).
        /// </summary>
        public void ReapplyVolumetricClouds()
        {
            ApplyVolumetricImmediate();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                MarkVolumeDirtyIfEditor();
#endif
        }

        private void OnEnable()
        {
            ApplyVolumetricImmediate();
            RefreshSolarSubscription();
            ApplySolarSyncIfNeeded();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                MarkVolumeDirtyIfEditor();
#endif
        }

        private void Start()
        {
            // OnEnable can run before Main Camera / Volume exist; retry once play mode has initialized the scene.
            if (_renderMode == NimbusCloudRenderMode.VolumetricClouds)
                ApplyVolumetricImmediate();
        }

        private void LateUpdate()
        {
            // OnValidate does not run when _renderMode changes from code at runtime; also re-tries if camera/Volume appear late.
            if (!Application.isPlaying)
                return;
            if (_renderMode != NimbusCloudRenderMode.VolumetricClouds)
                return;
            ApplyVolumetricImmediate();
        }

        private void OnDisable()
        {
            UnsubscribeSolar();
            TryDeactivateVolumetricCloudsOverrideOnDisable();
        }

        /// <summary>
        /// When the controller is disabled, turns off the Volumetric Clouds override if this instance was in volumetric mode (does not remove the override).
        /// </summary>
        private void TryDeactivateVolumetricCloudsOverrideOnDisable()
        {
            if (_renderMode != NimbusCloudRenderMode.VolumetricClouds)
                return;

            if (!TryResolveVolumeReadOnly(out Volume vol) || vol.profile == null)
                return;

            if (!vol.profile.TryGet(out WeatherVolumetricClouds clouds))
                return;

            clouds.active = false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                MarkVolumeDirtyIfEditor();
#endif
        }

        private void OnValidate()
        {
            ApplyVolumetricImmediate();
            RefreshSolarSubscription();
            ApplySolarSyncIfNeeded();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                MarkVolumeDirtyIfEditor();
#endif
        }

        private void RefreshSolarSubscription()
        {
            UnsubscribeSolar();
            if (!_syncDensityWindFromSolarCurves || _renderMode != NimbusCloudRenderMode.VolumetricClouds)
                return;

            if (_onSunLightChangedHandler == null)
                _onSunLightChangedHandler = OnSunLightChanged;

            WeatherSolarController.OnSunLightChanged += _onSunLightChangedHandler;
        }

        private void UnsubscribeSolar()
        {
            if (_onSunLightChangedHandler != null)
                WeatherSolarController.OnSunLightChanged -= _onSunLightChangedHandler;
        }

        private void OnSunLightChanged(WeatherSolarController solar, WeatherSolarController.SolarLightChangeData data)
        {
            ApplySolarSyncIfNeeded();
        }

        private void ApplySolarSyncIfNeeded()
        {
            if (!_syncDensityWindFromSolarCurves || _renderMode != NimbusCloudRenderMode.VolumetricClouds)
                return;

            if (!TryResolveOrEnsureVolume(out Volume vol) || vol.profile == null)
                return;

            ResolveVolumeComponent(vol);
            ApplyVolumetricCloudState();
        }

        private void ApplyVolumetricImmediate()
        {
            if (_renderMode != NimbusCloudRenderMode.VolumetricClouds)
                return;

            if (!TryResolveOrEnsureVolume(out Volume vol))
            {
                if (!_loggedMissingVolume && _volume == null && GetMainCameraForNimbus() == null)
                {
                    _loggedMissingVolume = true;
                    Debug.LogWarning("[NimbusCloudController] Assign a Volume or ensure a camera uses the MainCamera tag.", this);
                }
                return;
            }

            _loggedMissingVolume = false;

            // Volume must contribute to the stack; weight 0 means no volumetric clouds.
            vol.enabled = true;
            if (vol.weight <= 0f)
                vol.weight = 1f;

            EnsureVolumetricCloudsActive(vol);
        }

#if UNITY_EDITOR
        private void MarkVolumeDirtyIfEditor()
        {
            if (Application.isPlaying)
                return;
            if (!TryResolveVolumeReadOnly(out Volume vol))
                return;
            TryEditorSetDirty(vol);
            if (vol.profile != null)
                TryEditorSetDirty(vol.profile);
        }

        private static MethodInfo _cachedEditorSetDirty;

        private static void TryEditorSetDirty(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            try
            {
                if (_cachedEditorSetDirty == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name != "UnityEditor")
                            continue;
                        Type editorUtility = assembly.GetType("UnityEditor.EditorUtility");
                        _cachedEditorSetDirty = editorUtility?.GetMethod(
                            "SetDirty",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(UnityEngine.Object) },
                            null);
                        break;
                    }
                }

                _cachedEditorSetDirty?.Invoke(null, new object[] { obj });
            }
            catch
            {
                // ignore
            }
        }
#endif

        /// <summary>Read-only resolve; does not add a <see cref="Volume"/>.</summary>
        private bool TryResolveVolumeReadOnly(out Volume vol)
        {
            vol = _volume;
            if (vol != null)
                return true;
            Camera main = GetMainCameraForNimbus();
            if (main == null)
                return false;
            vol = main.GetComponent<Volume>();
            return vol != null;
        }

        /// <summary>
        /// Resolves the main camera. In the Editor, <see cref="Camera.main"/> is often null even when a MainCamera-tagged camera exists.
        /// </summary>
        public static Camera GetMainCameraForNimbus()
        {
            Camera c = Camera.main;
            if (c != null)
                return c;
            GameObject go = GameObject.FindGameObjectWithTag("MainCamera");
            return go != null ? go.GetComponent<Camera>() : null;
        }

        private bool TryResolveOrEnsureVolume(out Volume vol)
        {
            vol = _volume;
            if (vol != null)
            {
                EnsureVolumeProfileInstanceIfMissing(vol);
                if (vol.profile == null)
                    return false;

                return true;
            }

            Camera main = GetMainCameraForNimbus();
            if (main == null)
                return false;

            vol = main.GetComponent<Volume>();
            if (vol == null)
                vol = main.gameObject.AddComponent<Volume>();

            EnsureVolumeProfileInstanceIfMissing(vol);
            if (vol.profile == null)
                return false;

            return true;
        }

        /// <summary>
        /// Creates an embedded <see cref="VolumeProfile"/> on the Volume when missing (serialized with the scene/prefab).
        /// </summary>
        private void EnsureVolumeProfileInstanceIfMissing(Volume vol)
        {
            if (vol == null || vol.profile != null)
                return;

            vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        private void EnsureVolumetricCloudsActive(Volume vol)
        {
            // Use .profile so Unity instantiates a unique copy when sharedProfile is set (edits apply to this Volume only).
            VolumeProfile profile = vol.profile;
            if (profile == null)
                return;

            if (!profile.TryGet(out WeatherVolumetricClouds clouds))
            {
                clouds = profile.Add<WeatherVolumetricClouds>(true);
                _loggedMissingClouds = false;
            }
            else
            {
                _loggedMissingClouds = false;
            }

            // Always re-apply when Nimbus is in volumetric mode (e.g. override was turned off for Cloud Plane, or new scene load).
            // VolumeManager skips overrides unless VolumeComponent.active is true (not only state.value).
            clouds.active = true;
            clouds.state.overrideState = true;
            clouds.state.value = true;
        }

        private void ResolveVolumeComponent(Volume vol)
        {
            _clouds = null;

            if (vol == null || vol.profile == null)
                return;

            if (!vol.profile.TryGet(out WeatherVolumetricClouds clouds))
            {
                if (!_loggedMissingClouds)
                {
                    _loggedMissingClouds = true;
                    Debug.LogWarning(
                        "[NimbusCloudController] VolumeProfile has no Volumetric Clouds (URP) override. Editing a shared profile modifies the asset on disk.",
                        this);
                }
                return;
            }

            _loggedMissingClouds = false;
            _clouds = clouds;
        }

        private void ApplyVolumetricCloudState()
        {
            if (_clouds == null)
                return;

            float dayFraction = EvaluateDayFraction();

            _clouds.globalSpeed.overrideState = true;
            _clouds.globalSpeed.value = _globalWindSpeedKmhOverDay.Evaluate(dayFraction);

            _clouds.globalOrientation.overrideState = true;
            _clouds.globalOrientation.value = Mathf.Clamp(_globalWindOrientationOverDay.Evaluate(dayFraction), 0f, 360f);
        }

        private float EvaluateDayFraction()
        {
            WeatherSolarController solar = WeatherSolarController.ActiveInstance;
            if (solar != null)
            {
                DateTime local = solar.LocalDateTime;
                return Mathf.Clamp01((float)(local.TimeOfDay.TotalSeconds / 86400.0));
            }

            if (Application.isPlaying)
                return Mathf.Repeat(Time.time / 86400f, 1f);

            // Editor, no solar: fixed midday sample for wind curves (avoids a separate manual slider).
            return 0.5f;
        }
    }
}
