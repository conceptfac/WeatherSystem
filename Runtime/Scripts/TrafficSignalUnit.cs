using System;
using ConceptFactory.Weather;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TrafficSignalUnit : MonoBehaviour
{
    public enum SignalKind
    {
        Vehicle = 0,
        Pedestrian = 1
    }

    public enum SignalState
    {
        Off = 0,
        Red = 1,
        RedYellow = 2,
        Green = 3,
        Yellow = 4,
        DontWalk = 5,
        Walk = 6,
        FlashingDontWalk = 7
    }

    [Serializable]
    public struct LampTarget
    {
        public bool working;
        [SerializeField] private bool _workingInitialized;
        public Renderer meshRenderer;
        public int materialIndex;
        public Light[] nightLightTargets;
        [ColorUsage(false, false)] public Color activeBaseColor;
        [ColorUsage(false, false)] public Color disabledBaseColor;
        [ColorUsage(true, true)] public Color activeEmissionColor;
        [ColorUsage(true, true)] public Color disabledEmissionColor;

        public void EnsureDefaults()
        {
            if (_workingInitialized)
            {
                return;
            }

            if (!working)
            {
                working = true;
            }

            _workingInitialized = true;
        }
    }

    [Serializable]
    private struct LampChannel
    {
        private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapColorShaderId = Shader.PropertyToID("_BaseMapColor");
        private static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private LampTarget[] _targets;

        public void EnsureDefaults()
        {
            if (_targets == null)
            {
                return;
            }

            for (int index = 0; index < _targets.Length; index++)
            {
                _targets[index].EnsureDefaults();
            }
        }

        public void Apply(bool isActive, bool enableSceneLights, MaterialPropertyBlock propertyBlock)
        {
            ApplyMaterialColors(isActive, propertyBlock);
            SetLightsActive(isActive && enableSceneLights);
        }

        private void SetLightsActive(bool isActive)
        {
            if (_targets == null)
            {
                return;
            }

            for (int targetIndex = 0; targetIndex < _targets.Length; targetIndex++)
            {
                Light[] lightTargets = _targets[targetIndex].nightLightTargets;
                if (lightTargets == null)
                {
                    continue;
                }

                for (int lightIndex = 0; lightIndex < lightTargets.Length; lightIndex++)
                {
                    Light target = lightTargets[lightIndex];
                    if (target == null)
                    {
                        continue;
                    }

                    target.enabled = isActive && _targets[targetIndex].working;
                }
            }
        }

        private void ApplyMaterialColors(bool isActive, MaterialPropertyBlock propertyBlock)
        {
            if (_targets == null)
            {
                return;
            }

            for (int index = 0; index < _targets.Length; index++)
            {
                LampTarget target = _targets[index];
                if (target.meshRenderer == null)
                {
                    continue;
                }

                if (!TryGetTargetMaterial(target, out Material targetMaterial))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    targetMaterial.EnableKeyword("_EMISSION");
                }

                target.meshRenderer.GetPropertyBlock(propertyBlock, target.materialIndex);
                bool targetIsActive = isActive && target.working;

                if (targetMaterial.HasProperty(BaseColorShaderId))
                {
                    propertyBlock.SetColor(BaseColorShaderId, targetIsActive ? target.activeBaseColor : target.disabledBaseColor);
                }

                if (targetMaterial.HasProperty(BaseMapColorShaderId))
                {
                    propertyBlock.SetColor(BaseMapColorShaderId, targetIsActive ? target.activeBaseColor : target.disabledBaseColor);
                }

                propertyBlock.SetColor(EmissionColorShaderId, targetIsActive ? target.activeEmissionColor : target.disabledEmissionColor);
                target.meshRenderer.SetPropertyBlock(propertyBlock, target.materialIndex);
            }
        }

        private static bool TryGetTargetMaterial(LampTarget target, out Material material)
        {
            Material[] materials = Application.isPlaying ? target.meshRenderer.materials : target.meshRenderer.sharedMaterials;
            if (materials == null || target.materialIndex < 0 || target.materialIndex >= materials.Length)
            {
                material = null;
                return false;
            }

            material = materials[target.materialIndex];
            return material != null && material.HasProperty(EmissionColorShaderId);
        }
    }

    [Header("Type")]
    [SerializeField] private SignalKind _signalKind = SignalKind.Vehicle;
    [SerializeField] private string _channelId = "Main";

    [SerializeField] private LampChannel _redLamp;
    [SerializeField] private LampChannel _yellowLamp;
    [SerializeField] private LampChannel _greenLamp;

   [SerializeField] private LampChannel _walkLamp;
    [SerializeField] private LampChannel _dontWalkLamp;

    [SerializeField] private bool _useSceneLightsOnlyAtNight = true;
    [SerializeField] [Range(0f, 1f)] private float _nightLightActivationThreshold = 0.5f;

    [Header("Failure States")]
    [SerializeField] private bool _isWorking = true;
    [SerializeField] private bool _isBlackoutActive;

    [Header("Debug")]
    [SerializeField] private SignalState _currentState = SignalState.Off;
    [SerializeField] private bool _sceneLightsAllowed = true;

    private MaterialPropertyBlock _propertyBlock;

    public SignalKind Kind => _signalKind;
    public string ChannelId => _channelId;
    public SignalState CurrentState => _currentState;
    public bool IsWorking => _isWorking;
    public bool IsBlackoutActive => _isBlackoutActive;

    private void Awake()
    {
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _propertyBlock ??= new MaterialPropertyBlock();
        EnsureTargetDefaults();
        WeatherSolarController.OnSunLightChanged += HandleSunLightChanged;
        RefreshSceneLightAllowance();
        ApplyState(_currentState);
    }

    private void OnValidate()
    {
        EnsureTargetDefaults();
    }

    private void OnDisable()
    {
        WeatherSolarController.OnSunLightChanged -= HandleSunLightChanged;
    }

    public void ApplyState(SignalState state)
    {
        _propertyBlock ??= new MaterialPropertyBlock();
        _currentState = state;

        if (!_isWorking || _isBlackoutActive)
        {
            ApplyOffVisualState();
            return;
        }

        bool redOn = state == SignalState.Red || state == SignalState.RedYellow;
        bool yellowOn = state == SignalState.Yellow || state == SignalState.RedYellow;
        bool greenOn = state == SignalState.Green;
        bool walkOn = state == SignalState.Walk;
        bool dontWalkOn = state == SignalState.DontWalk || state == SignalState.FlashingDontWalk;

        if (_signalKind == SignalKind.Vehicle)
        {
            _redLamp.Apply(redOn, _sceneLightsAllowed, _propertyBlock);
            _yellowLamp.Apply(yellowOn, _sceneLightsAllowed, _propertyBlock);
            _greenLamp.Apply(greenOn, _sceneLightsAllowed, _propertyBlock);
            _walkLamp.Apply(false, false, _propertyBlock);
            _dontWalkLamp.Apply(false, false, _propertyBlock);
            return;
        }

        _redLamp.Apply(false, false, _propertyBlock);
        _yellowLamp.Apply(false, false, _propertyBlock);
        _greenLamp.Apply(false, false, _propertyBlock);
        _walkLamp.Apply(walkOn, _sceneLightsAllowed, _propertyBlock);
        _dontWalkLamp.Apply(dontWalkOn, _sceneLightsAllowed, _propertyBlock);
    }

    public void SetBlackout(bool isActive)
    {
        if (_isBlackoutActive == isActive)
        {
            return;
        }

        _isBlackoutActive = isActive;
        ApplyState(_currentState);
    }

    public void SetWorking(bool isWorking)
    {
        if (_isWorking == isWorking)
        {
            return;
        }

        _isWorking = isWorking;
        ApplyState(_currentState);
    }

    private void ApplyOffVisualState()
    {
        _redLamp.Apply(false, false, _propertyBlock);
        _yellowLamp.Apply(false, false, _propertyBlock);
        _greenLamp.Apply(false, false, _propertyBlock);
        _walkLamp.Apply(false, false, _propertyBlock);
        _dontWalkLamp.Apply(false, false, _propertyBlock);
    }

    private void EnsureTargetDefaults()
    {
        EnsureLampChannelDefaults(ref _redLamp);
        EnsureLampChannelDefaults(ref _yellowLamp);
        EnsureLampChannelDefaults(ref _greenLamp);
        EnsureLampChannelDefaults(ref _walkLamp);
        EnsureLampChannelDefaults(ref _dontWalkLamp);
    }

    private static void EnsureLampChannelDefaults(ref LampChannel lampChannel)
    {
        lampChannel.EnsureDefaults();
    }

    private void HandleSunLightChanged(WeatherSolarController controller, WeatherSolarController.SolarLightChangeData data)
    {
        bool previousValue = _sceneLightsAllowed;
        _sceneLightsAllowed = EvaluateSceneLightAllowance(data.DaylightFactor);
        if (previousValue != _sceneLightsAllowed)
        {
            ApplyState(_currentState);
        }
    }

    private void RefreshSceneLightAllowance()
    {
        if (WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            _sceneLightsAllowed = EvaluateSceneLightAllowance(solarLightData.DaylightFactor);
            return;
        }

        _sceneLightsAllowed = !_useSceneLightsOnlyAtNight;
    }

    private bool EvaluateSceneLightAllowance(float daylightFactor)
    {
        if (!_useSceneLightsOnlyAtNight)
        {
            return true;
        }

        return daylightFactor <= _nightLightActivationThreshold;
    }

    [ContextMenu("Preview Off")]
    private void PreviewOff() => ApplyState(SignalState.Off);

    [ContextMenu("Trigger Blackout")]
    private void TriggerBlackout() => SetBlackout(true);

    [ContextMenu("Restore From Blackout")]
    private void RestoreFromBlackout() => SetBlackout(false);

    [ContextMenu("Preview Red")]
    private void PreviewRed() => ApplyState(SignalState.Red);

    [ContextMenu("Preview Red Yellow")]
    private void PreviewRedYellow() => ApplyState(SignalState.RedYellow);

    [ContextMenu("Preview Green")]
    private void PreviewGreen() => ApplyState(SignalState.Green);

    [ContextMenu("Preview Yellow")]
    private void PreviewYellow() => ApplyState(SignalState.Yellow);

    [ContextMenu("Preview Walk")]
    private void PreviewWalk() => ApplyState(SignalState.Walk);

    [ContextMenu("Preview Dont Walk")]
    private void PreviewDontWalk() => ApplyState(SignalState.DontWalk);
}
