using System;
using System.Collections;
using ConceptFactory.Weather;
using UnityEngine;
using UnityEngine.Events;


[ExecuteAlways]
public class LightPole : MonoBehaviour
{

    [Serializable]
    public struct EmissiveMesh
    {
        public Renderer meshRederer;
        public int materialIndex;
    }



    [ColorUsage(true, true)]
    [SerializeField] private Color _emissiveColorActive = Color.white * 2f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _emissiveColorDisabled = Color.black;
    [SerializeField] [Range(0f, 1f)] private float _activationDaylightThreshold = 0.2f;
    [SerializeField] private Vector2 _randomActivationDelayRange = new Vector2(0f, 2f);
    [Header("Activation Flicker")]
    [SerializeField] [Range(0f, 1f)] private float _activationFlickerChance = 0.35f;
    [SerializeField] private Vector2Int _activationFlickerCountRange = new Vector2Int(2, 6);
    [SerializeField] private Vector2 _activationFlickerStepDelayRange = new Vector2(0.03f, 0.12f);

    [SerializeField] private EmissiveMesh[] _emissiveTargets;
    [SerializeField] private Light[] _lightTargets;

    [SerializeField]
    private UnityEvent OnActivate = new UnityEvent();
    [SerializeField]
    private UnityEvent OnDeactivate = new UnityEvent();


    private bool _isEmissionActive;
    private bool _hasAppliedEmissionState;
    private bool _pendingState;
    private MaterialPropertyBlock _mpb;
    private Coroutine _stateChangeCoroutine;

    private void Awake()
    {
        _mpb ??= new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _mpb ??= new MaterialPropertyBlock();
        WeatherSolarController.OnSunLightChanged += HandleSunLightChanged;

        if (WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            bool shouldActivate = solarLightData.DaylightFactor <= _activationDaylightThreshold;
            ApplyEmissionState(shouldActivate, true);
        }
    }

    private void OnDisable()
    {
        WeatherSolarController.OnSunLightChanged -= HandleSunLightChanged;
        CancelPendingStateChange();
        _hasAppliedEmissionState = false;
    }

    private void HandleSunLightChanged(WeatherSolarController controller, WeatherSolarController.SolarLightChangeData data)
    {
        bool shouldActivate = data.DaylightFactor <= _activationDaylightThreshold;

        if (!Application.isPlaying)
        {
            ApplyEmissionState(shouldActivate);
            return;
        }

        ScheduleEmissionStateChange(shouldActivate);
    }

    private void ApplyEmissionState(bool shouldActivate, bool force = false)
    {
        bool stateChanged = !_hasAppliedEmissionState || _isEmissionActive != shouldActivate;
        if (!force && !stateChanged)
        {
            return;
        }

        _hasAppliedEmissionState = true;
        _isEmissionActive = shouldActivate;
        ApplyVisualState(shouldActivate);

        if (!force && !stateChanged)
            return;

        if (shouldActivate)
        {
            OnActivate?.Invoke();
        }
        else
        {
            OnDeactivate?.Invoke();
        }
    }

    private void ScheduleEmissionStateChange(bool shouldActivate)
    {
        bool stateChanged = !_hasAppliedEmissionState || _isEmissionActive != shouldActivate;
        if (!stateChanged)
        {
            return;
        }

        if (_stateChangeCoroutine != null && _pendingState == shouldActivate)
        {
            return;
        }

        CancelPendingStateChange();
        _pendingState = shouldActivate;
        _stateChangeCoroutine = StartCoroutine(ApplyEmissionStateAfterDelay(shouldActivate));
    }

    private IEnumerator ApplyEmissionStateAfterDelay(bool shouldActivate)
    {
        Vector2 sortedDelayRange = new Vector2(
            Mathf.Min(_randomActivationDelayRange.x, _randomActivationDelayRange.y),
            Mathf.Max(_randomActivationDelayRange.x, _randomActivationDelayRange.y));

        float delay = UnityEngine.Random.Range(sortedDelayRange.x, sortedDelayRange.y);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (shouldActivate && ShouldPlayActivationFlicker())
        {
            yield return PlayActivationFlicker();
        }

        _stateChangeCoroutine = null;
        ApplyEmissionState(shouldActivate);
    }

    private void CancelPendingStateChange()
    {
        if (_stateChangeCoroutine == null)
        {
            return;
        }

        StopCoroutine(_stateChangeCoroutine);
        _stateChangeCoroutine = null;
    }

    private bool ShouldPlayActivationFlicker()
    {
        return _activationFlickerChance > 0f && UnityEngine.Random.value <= _activationFlickerChance;
    }

    private IEnumerator PlayActivationFlicker()
    {
        Vector2Int flickerCountRange = new Vector2Int(
            Mathf.Min(_activationFlickerCountRange.x, _activationFlickerCountRange.y),
            Mathf.Max(_activationFlickerCountRange.x, _activationFlickerCountRange.y));

        int flickerCount = UnityEngine.Random.Range(flickerCountRange.x, flickerCountRange.y + 1);
        Vector2 sortedStepDelayRange = new Vector2(
            Mathf.Min(_activationFlickerStepDelayRange.x, _activationFlickerStepDelayRange.y),
            Mathf.Max(_activationFlickerStepDelayRange.x, _activationFlickerStepDelayRange.y));

        for (int i = 0; i < flickerCount; i++)
        {
            bool emitActive = UnityEngine.Random.value > 0.5f;
            ApplyVisualState(emitActive);

            float stepDelay = UnityEngine.Random.Range(sortedStepDelayRange.x, sortedStepDelayRange.y);
            if (stepDelay > 0f)
            {
                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    private void ApplyVisualState(bool isActive)
    {
        SetEmission(isActive ? _emissiveColorActive : _emissiveColorDisabled);
        SetLightsActive(isActive);
    }

    private void SetLightsActive(bool isActive)
    {
        if (_lightTargets == null || _lightTargets.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _lightTargets.Length; i++)
        {
            Light targetLight = _lightTargets[i];
            if (targetLight == null)
            {
                continue;
            }

            targetLight.enabled = isActive;
        }
    }

    private void SetEmission(Color finalColor)
    {
        if (_emissiveTargets == null || _emissiveTargets.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _emissiveTargets.Length; i++)
        {
            var target = _emissiveTargets[i];
            if (target.meshRederer == null) continue;

            if (!Application.isPlaying)
            {
                target.meshRederer.GetPropertyBlock(_mpb, target.materialIndex);
                _mpb.SetColor("_EmissionColor", finalColor);
                target.meshRederer.SetPropertyBlock(_mpb, target.materialIndex);
                continue;
            }

            Material[] materials = target.meshRederer.materials;
            if (materials == null || target.materialIndex < 0 || target.materialIndex >= materials.Length)
            {
                continue;
            }

            Material targetMaterial = materials[target.materialIndex];
            if (targetMaterial == null || !targetMaterial.HasProperty("_EmissionColor"))
            {
                continue;
            }

            targetMaterial.EnableKeyword("_EMISSION");
            targetMaterial.SetColor("_EmissionColor", finalColor);
        }
    }


}
