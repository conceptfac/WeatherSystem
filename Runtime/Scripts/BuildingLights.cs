using System;
using System.Collections;
using System.Collections.Generic;
using ConceptFactory.Weather;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingLights : MonoBehaviour
{
    [Serializable]
    public struct EmissiveMesh
    {
        public Renderer meshRenderer;
        public int materialIndex;
    }

    private static readonly int EmissiveColorShaderId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissiveStrengthShaderId = Shader.PropertyToID("_EmissiveStrength");

    [Header("Activation")]
    [SerializeField] [Range(0f, 1f)] private float _activationDaylightThreshold = 0.22f;
    [SerializeField] private Vector2 _nightStartDelayRange = new Vector2(0f, 8f);
    [SerializeField] private Vector2 _shutdownDelayRange = new Vector2(3f, 12f);
    [SerializeField] private Vector2 _shutdownRealTimeDelayRange = new Vector2(1.5f, 4f);
    [SerializeField] [Min(0.05f)] private float _minimumShutdownStepSeconds = 1.25f;
    [SerializeField] [Range(0f, 23.99f)] private float _dayResidualStartHour = 7.5f;
    [SerializeField] [Range(0f, 1f)] private float _minimumDayLitRatio = 0.03f;
    [SerializeField] [Range(0f, 1f)] private float _maximumDayLitRatio = 0.12f;

    [Header("Windows")]
    [SerializeField] private EmissiveMesh[] _windowTargets;
    [SerializeField] [Range(0f, 1f)] private float _minimumNightLitRatio = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float _maximumNightLitRatio = 0.8f;

    [Header("Look")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _windowLitColor = new Color(1f, 0.84f, 0.62f, 1f);
    [SerializeField] private float _windowLitStrength = 2.2f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _windowOffColor = Color.black;
    [SerializeField] private float _windowOffStrength = 0f;

    [Header("Night Rhythm")]
    [SerializeField] private float _startupRushDurationMinutes = 60f;
    [SerializeField] private Vector2 _startupIntervalRange = new Vector2(5f, 12f);
    [SerializeField] private Vector2 _steadyIntervalRange = new Vector2(8f, 30f);
    [SerializeField] [Min(1f)] private float _startupSpeedBoost = 3f;
    [SerializeField] [Range(0f, 1f)] private float _steadyToggleChance = 0.45f;

    [Header("Late Night")]
    [SerializeField] private bool _enableLateNightReduction = true;
    [SerializeField] [Range(0f, 23.99f)] private float _lateNightStartHour = 23f;
    [SerializeField] [Range(0f, 23.99f)] private float _lateNightEndHour = 5.5f;
    [SerializeField] [Min(0)] private int _lateNightMinimumLitWindows = 0;
    [SerializeField] [Min(0)] private int _lateNightMaximumLitWindows = 3;

    private readonly List<int> _startupActivationQueue = new List<int>();
    private readonly List<int> _shuffledIndices = new List<int>();
    private readonly List<int> _matchingIndicesBuffer = new List<int>();

    private bool[] _windowStates;
    private bool _nightModeRequested;
    private int _targetNightLitCount;
    private int _initialNightLitCount;
    private int _lateNightTargetLitCount;
    private int _targetDayLitCount;
    private int _activeWindowCount;
    private float _nightElapsedSimulatedMinutes;
    private Coroutine _nightRoutine;
    private Coroutine _stateTransitionRoutine;
    private MaterialPropertyBlock _propertyBlock;
    private bool _isBlackoutActive;
    private bool[] _blackoutRestoreStates;
#if UNITY_EDITOR
    [SerializeField] [HideInInspector] private bool _editModeSimulationActive;
    private bool _editModeShutdownActive;
    private EditorSimulationPhase _editorSimulationPhase;
    private float _editorPhaseTimeRemaining;
    private double _lastEditorUpdateTime;
#endif

    private void Awake()
    {
        EnsureStateBuffers();
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        EnsureStateBuffers();
        _propertyBlock ??= new MaterialPropertyBlock();
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (_editModeSimulationActive)
            {
                SubscribeEditorUpdate();
                _lastEditorUpdateTime = UnityEditor.EditorApplication.timeSinceStartup;
                RefreshEditModeSimulationState();
            }
            else if (_editModeShutdownActive)
            {
                SubscribeEditorUpdate();
                _lastEditorUpdateTime = UnityEditor.EditorApplication.timeSinceStartup;
            }
#endif
            return;
        }

        WeatherSolarController.OnSunLightChanged += HandleSunLightChanged;
#if UNITY_EDITOR
        UnsubscribeEditorUpdate();
#endif

        if (WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            ApplySolarState(solarLightData.DaylightFactor <= _activationDaylightThreshold, true);
            return;
        }

        SetAllWindowsImmediate(false);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            WeatherSolarController.OnSunLightChanged -= HandleSunLightChanged;
        }

        StopAllRunningCoroutines();
#if UNITY_EDITOR
        UnsubscribeEditorUpdate();
#endif
    }

    private void HandleSunLightChanged(WeatherSolarController controller, WeatherSolarController.SolarLightChangeData data)
    {
        ApplySolarState(data.DaylightFactor <= _activationDaylightThreshold, true);
    }

    private void ApplySolarState(bool shouldActivateNightMode, bool animate)
    {
        if (_isBlackoutActive)
        {
            _nightModeRequested = shouldActivateNightMode;
            StopAllRunningCoroutines();
#if UNITY_EDITOR
            _editorSimulationPhase = EditorSimulationPhase.Idle;
            _editorPhaseTimeRemaining = 0f;
#endif
            return;
        }

        if (!animate)
        {
            _nightModeRequested = shouldActivateNightMode;
            StopAllRunningCoroutines();
            SetAllWindowsImmediate(shouldActivateNightMode);
            return;
        }

        if (_nightModeRequested == shouldActivateNightMode)
        {
            return;
        }

        _nightModeRequested = shouldActivateNightMode;

#if UNITY_EDITOR
        if (IsEditModeSimulationActive)
        {
            if (shouldActivateNightMode)
            {
                BeginNightModeInEditor();
            }
            else
            {
                BeginShutdownModeInEditor();
            }

            return;
        }
#endif

        if (shouldActivateNightMode)
        {
            BeginNightMode();
            return;
        }

        BeginShutdownMode();
    }

    private void BeginNightMode()
    {
        StopTransitionRoutine();
        if (_nightRoutine != null)
        {
            StopCoroutine(_nightRoutine);
        }

        _stateTransitionRoutine = StartCoroutine(BeginNightModeAfterDelay());
    }

    private IEnumerator BeginNightModeAfterDelay()
    {
        float delay = GetScaledDelaySeconds(_nightStartDelayRange);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        PrepareNightPlan();
        _stateTransitionRoutine = null;
        _nightRoutine = StartCoroutine(RunNightMode());
    }

    private void BeginShutdownMode()
    {
        StopTransitionRoutine();

        if (_nightRoutine != null)
        {
            StopCoroutine(_nightRoutine);
            _nightRoutine = null;
        }

        PrepareDayPlan();
        _stateTransitionRoutine = StartCoroutine(RunShutdownMode());
    }

    private IEnumerator RunNightMode()
    {
        while (_nightModeRequested)
        {
            bool changedWindow = TryApplyNightStep();
            float waitSeconds = GetNextNightDelaySeconds(changedWindow);
            _nightElapsedSimulatedMinutes += waitSeconds * WeatherSolarController.GetActiveSimulatedMinutesPerRealSecond();

            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }
            else
            {
                yield return null;
            }
        }

        _nightRoutine = null;
    }

    private IEnumerator RunShutdownMode()
    {
        while (!_nightModeRequested && _activeWindowCount != _targetDayLitCount)
        {
            float waitSeconds = GetShutdownDelaySeconds();
            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }

            bool shouldTurnOn = _activeWindowCount < _targetDayLitCount;
            int windowIndex = GetRandomWindowIndexByState(!shouldTurnOn);
            if (windowIndex < 0)
            {
                break;
            }

            SetWindowState(windowIndex, shouldTurnOn);
        }

        _stateTransitionRoutine = null;
    }

    private bool TryApplyNightStep()
    {
        UpdateLateNightTarget();

        if (_startupActivationQueue.Count > 0)
        {
            int windowIndex = _startupActivationQueue[0];
            _startupActivationQueue.RemoveAt(0);
            SetWindowState(windowIndex, true);
            return true;
        }

        if (_activeWindowCount < _targetNightLitCount)
        {
            int offIndex = GetRandomWindowIndexByState(false);
            if (offIndex >= 0)
            {
                SetWindowState(offIndex, true);
                return true;
            }
        }

        if (_activeWindowCount > _targetNightLitCount)
        {
            int onIndex = GetRandomWindowIndexByState(true);
            if (onIndex >= 0)
            {
                SetWindowState(onIndex, false);
                return true;
            }
        }

        if (UnityEngine.Random.value > _steadyToggleChance)
        {
            return false;
        }

        bool shouldTurnOn = _activeWindowCount <= 0 || (_activeWindowCount < _targetNightLitCount && UnityEngine.Random.value > 0.25f);
        int candidateIndex = GetRandomWindowIndexByState(!shouldTurnOn);

        if (candidateIndex < 0)
        {
            candidateIndex = GetRandomWindowIndexByState(shouldTurnOn);
            shouldTurnOn = candidateIndex >= 0 && !GetWindowState(candidateIndex);
        }

        if (candidateIndex < 0)
        {
            return false;
        }

        SetWindowState(candidateIndex, shouldTurnOn);
        return true;
    }

    private float GetNextNightDelaySeconds(bool changedWindow)
    {
        Vector2 baseRange = _nightElapsedSimulatedMinutes < _startupRushDurationMinutes ? _startupIntervalRange : _steadyIntervalRange;
        float delaySeconds = GetScaledDelaySeconds(baseRange);

        if (_nightElapsedSimulatedMinutes >= _startupRushDurationMinutes)
        {
            return changedWindow ? delaySeconds : Mathf.Max(0.5f, delaySeconds * 0.5f);
        }

        float startupProgress = _startupRushDurationMinutes <= 0f ? 1f : Mathf.Clamp01(_nightElapsedSimulatedMinutes / _startupRushDurationMinutes);
        float speedBoost = Mathf.Lerp(_startupSpeedBoost, 1f, startupProgress);
        float acceleratedDelay = delaySeconds / Mathf.Max(1f, speedBoost);
        return changedWindow ? acceleratedDelay : Mathf.Max(0.25f, acceleratedDelay * 0.5f);
    }

    private void PrepareNightPlan()
    {
        EnsureStateBuffers();
        _nightElapsedSimulatedMinutes = 0f;

        for (int i = 0; i < _windowStates.Length; i++)
        {
            SetWindowState(i, false);
        }

        _shuffledIndices.Clear();
        for (int i = 0; i < _windowTargets.Length; i++)
        {
            if (_windowTargets[i].meshRenderer != null)
            {
                _shuffledIndices.Add(i);
            }
        }

        Shuffle(_shuffledIndices);

        float minimumRatio = Mathf.Min(_minimumNightLitRatio, _maximumNightLitRatio);
        float maximumRatio = Mathf.Max(_minimumNightLitRatio, _maximumNightLitRatio);
        float ratio = UnityEngine.Random.Range(minimumRatio, maximumRatio);
        _targetNightLitCount = Mathf.Clamp(Mathf.RoundToInt(_shuffledIndices.Count * ratio), 0, _shuffledIndices.Count);
        _initialNightLitCount = _targetNightLitCount;

        int lateNightMinimum = Mathf.Max(0, Mathf.Min(_lateNightMinimumLitWindows, _shuffledIndices.Count));
        int lateNightMaximum = Mathf.Max(lateNightMinimum, Mathf.Min(_lateNightMaximumLitWindows, _shuffledIndices.Count));
        _lateNightTargetLitCount = UnityEngine.Random.Range(lateNightMinimum, lateNightMaximum + 1);

        _startupActivationQueue.Clear();
        for (int i = 0; i < _targetNightLitCount; i++)
        {
            _startupActivationQueue.Add(_shuffledIndices[i]);
        }
    }

    private void UpdateLateNightTarget()
    {
        if (!_enableLateNightReduction)
        {
            return;
        }

        if (!TryGetCurrentLocalTimeHours(out float currentHour))
        {
            return;
        }

        float progress = GetWrappedTimeProgress(_lateNightStartHour, _lateNightEndHour, currentHour);
        if (progress <= 0f)
        {
            return;
        }

        int minimumTarget = Mathf.Min(_initialNightLitCount, _lateNightTargetLitCount);
        _targetNightLitCount = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(_initialNightLitCount, minimumTarget, progress)),
            minimumTarget,
            _initialNightLitCount);
    }

    private static float GetWrappedTimeProgress(float startHour, float endHour, float currentHour)
    {
        float normalizedStart = Mathf.Repeat(startHour, 24f);
        float normalizedEnd = Mathf.Repeat(endHour, 24f);
        float normalizedCurrent = Mathf.Repeat(currentHour, 24f);
        float totalDuration = Mathf.Repeat(normalizedEnd - normalizedStart, 24f);

        if (totalDuration <= 0.001f)
        {
            return 1f;
        }

        float elapsed = Mathf.Repeat(normalizedCurrent - normalizedStart, 24f);
        if (elapsed > totalDuration)
        {
            return 0f;
        }

        return Mathf.Clamp01(elapsed / totalDuration);
    }

    private static bool TryGetCurrentLocalTimeHours(out float currentHour)
    {
        if (WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            currentHour = solarLightData.LocalDateTime.Hour + (solarLightData.LocalDateTime.Minute / 60f);
            return true;
        }

        currentHour = 0f;
        return false;
    }

    private void PrepareDayPlan()
    {
        EnsureStateBuffers();

        int validWindowCount = GetValidWindowCount();
        if (TryGetCurrentLocalTimeHours(out float currentHour) && currentHour < _dayResidualStartHour)
        {
            _targetDayLitCount = 0;
            return;
        }

        float minimumRatio = Mathf.Min(_minimumDayLitRatio, _maximumDayLitRatio);
        float maximumRatio = Mathf.Max(_minimumDayLitRatio, _maximumDayLitRatio);
        float ratio = UnityEngine.Random.Range(minimumRatio, maximumRatio);
        _targetDayLitCount = Mathf.Clamp(Mathf.RoundToInt(validWindowCount * ratio), 0, validWindowCount);
    }

    private void EnsureStateBuffers()
    {
        if (_windowTargets == null)
        {
            _windowTargets = Array.Empty<EmissiveMesh>();
        }

        if (_windowStates == null || _windowStates.Length != _windowTargets.Length)
        {
            _windowStates = new bool[_windowTargets.Length];
            _activeWindowCount = 0;
        }
    }

    private void SetAllWindowsImmediate(bool isOn)
    {
        EnsureStateBuffers();
        for (int i = 0; i < _windowTargets.Length; i++)
        {
            SetWindowState(i, isOn);
        }
    }

    private void SetWindowState(int index, bool isOn)
    {
        if (_windowStates == null || index < 0 || index >= _windowStates.Length)
        {
            return;
        }

        if (_windowStates[index] == isOn)
        {
            return;
        }

        _windowStates[index] = isOn;
        _activeWindowCount += isOn ? 1 : -1;

        EmissiveMesh target = _windowTargets[index];
        if (target.meshRenderer == null)
        {
            return;
        }

        ApplyWindowVisualState(target, isOn);
    }

    private bool GetWindowState(int index)
    {
        return _windowStates != null && index >= 0 && index < _windowStates.Length && _windowStates[index];
    }

    private void ApplyWindowVisualState(EmissiveMesh target, bool isOn)
    {
        Material sharedMaterial = GetTargetMaterial(target);
        if (sharedMaterial == null)
        {
            return;
        }

        target.meshRenderer.GetPropertyBlock(_propertyBlock, target.materialIndex);
        _propertyBlock.SetColor(EmissiveColorShaderId, isOn ? _windowLitColor : _windowOffColor);
        _propertyBlock.SetFloat(EmissiveStrengthShaderId, isOn ? _windowLitStrength : _windowOffStrength);
        target.meshRenderer.SetPropertyBlock(_propertyBlock, target.materialIndex);
    }

    private Material GetTargetMaterial(EmissiveMesh target)
    {
        Material[] materials = Application.isPlaying ? target.meshRenderer.materials : target.meshRenderer.sharedMaterials;
        if (materials == null || target.materialIndex < 0 || target.materialIndex >= materials.Length)
        {
            return null;
        }

        Material targetMaterial = materials[target.materialIndex];
        if (targetMaterial == null)
        {
            return null;
        }

        return targetMaterial.HasProperty(EmissiveColorShaderId) && targetMaterial.HasProperty(EmissiveStrengthShaderId)
            ? targetMaterial
            : null;
    }

    private int GetRandomWindowIndexByState(bool isOn)
    {
        _matchingIndicesBuffer.Clear();

        for (int i = 0; i < _windowTargets.Length; i++)
        {
            if (_windowTargets[i].meshRenderer == null || GetWindowState(i) != isOn)
            {
                continue;
            }

            _matchingIndicesBuffer.Add(i);
        }

        if (_matchingIndicesBuffer.Count == 0)
        {
            return -1;
        }

        return _matchingIndicesBuffer[UnityEngine.Random.Range(0, _matchingIndicesBuffer.Count)];
    }

    private float GetScaledDelaySeconds(Vector2 baseRange)
    {
        float min = Mathf.Min(baseRange.x, baseRange.y);
        float max = Mathf.Max(baseRange.x, baseRange.y);
        float baseDelay = UnityEngine.Random.Range(min, max);
        float simulatedMinutesPerSecond = Mathf.Max(0.01f, WeatherSolarController.GetActiveSimulatedMinutesPerRealSecond());
        return Mathf.Max(0.05f, baseDelay / simulatedMinutesPerSecond);
    }

    private float GetShutdownDelaySeconds()
    {
        float scaledDelaySeconds = GetScaledDelaySeconds(_shutdownDelayRange);
        float realTimeDelaySeconds = GetRandomRangeValue(_shutdownRealTimeDelayRange);
        return Mathf.Max(_minimumShutdownStepSeconds, scaledDelaySeconds, realTimeDelaySeconds);
    }

    private static float GetRandomRangeValue(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return UnityEngine.Random.Range(min, max);
    }

    private int GetValidWindowCount()
    {
        int validWindowCount = 0;
        for (int i = 0; i < _windowTargets.Length; i++)
        {
            if (_windowTargets[i].meshRenderer != null)
            {
                validWindowCount++;
            }
        }

        return validWindowCount;
    }

    private void Shuffle(List<int> indices)
    {
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
        }
    }

    private void StopTransitionRoutine()
    {
        if (_stateTransitionRoutine == null)
        {
            return;
        }

        StopCoroutine(_stateTransitionRoutine);
        _stateTransitionRoutine = null;
    }

    private void StopAllRunningCoroutines()
    {
        StopTransitionRoutine();

        if (_nightRoutine != null)
        {
            StopCoroutine(_nightRoutine);
            _nightRoutine = null;
        }
    }

    private void CaptureWindowStates(ref bool[] destination)
    {
        EnsureStateBuffers();

        if (destination == null || destination.Length != _windowStates.Length)
        {
            destination = new bool[_windowStates.Length];
        }

        Array.Copy(_windowStates, destination, _windowStates.Length);
    }

    private void RestoreWindowStates(bool[] sourceStates)
    {
        if (sourceStates == null || _windowStates == null || sourceStates.Length != _windowStates.Length)
        {
            return;
        }

        for (int i = 0; i < sourceStates.Length; i++)
        {
            SetWindowState(i, sourceStates[i]);
        }
    }

    public bool IsBlackoutActive => _isBlackoutActive;

    [ContextMenu("Trigger Blackout")]
    public void TriggerBlackout()
    {
        SetBlackout(true);
    }

    [ContextMenu("Restore From Blackout")]
    public void RestoreFromBlackout()
    {
        SetBlackout(false);
    }

    public void SetBlackout(bool isActive)
    {
        if (_isBlackoutActive == isActive)
        {
            return;
        }

        EnsureStateBuffers();
        _propertyBlock ??= new MaterialPropertyBlock();

        if (isActive)
        {
            CaptureBlackoutRestoreStates();
            _isBlackoutActive = true;
            StopAllRunningCoroutines();
#if UNITY_EDITOR
            _editorSimulationPhase = EditorSimulationPhase.Idle;
            _editorPhaseTimeRemaining = 0f;
#endif
            SetAllWindowsImmediate(false);
            return;
        }

        _isBlackoutActive = false;
        RestoreBlackoutStates();
        ResumeSimulationAfterBlackout();
    }

    private void CaptureBlackoutRestoreStates()
    {
        CaptureWindowStates(ref _blackoutRestoreStates);
    }

    private void RestoreBlackoutStates()
    {
        RestoreWindowStates(_blackoutRestoreStates);
    }

    private void ResumeSimulationAfterBlackout()
    {
        if (!WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            return;
        }

        bool shouldActivateNightMode = solarLightData.DaylightFactor <= _activationDaylightThreshold;
        _nightModeRequested = shouldActivateNightMode;

#if UNITY_EDITOR
        if (IsEditModeSimulationActive)
        {
            if (shouldActivateNightMode)
            {
                _startupActivationQueue.Clear();
                _targetNightLitCount = Mathf.Max(_activeWindowCount, Mathf.RoundToInt(GetValidWindowCount() * _minimumNightLitRatio));
                _nightElapsedSimulatedMinutes = _startupRushDurationMinutes;
                _editorSimulationPhase = EditorSimulationPhase.NightRunning;
                _editorPhaseTimeRemaining = GetNextNightDelaySeconds(false);
            }
            else
            {
                PrepareDayPlan();
                _editorSimulationPhase = EditorSimulationPhase.ShutdownRunning;
                _editorPhaseTimeRemaining = GetShutdownDelaySeconds();
            }

            return;
        }
#endif

        if (shouldActivateNightMode)
        {
            _startupActivationQueue.Clear();
            _targetNightLitCount = Mathf.Max(_activeWindowCount, Mathf.RoundToInt(GetValidWindowCount() * _minimumNightLitRatio));
            _nightElapsedSimulatedMinutes = _startupRushDurationMinutes;
            _nightRoutine = StartCoroutine(RunNightMode());
            return;
        }

        BeginShutdownMode();
    }

#if UNITY_EDITOR
    public bool IsEditModeSimulationActive => !Application.isPlaying && _editModeSimulationActive;

    public void StartEditModeSimulation()
    {
        if (Application.isPlaying)
        {
            return;
        }

        EnsureStateBuffers();
        _propertyBlock ??= new MaterialPropertyBlock();
        StopAllRunningCoroutines();
        _editModeSimulationActive = true;
        _editModeShutdownActive = false;
        _editorSimulationPhase = EditorSimulationPhase.Idle;
        _editorPhaseTimeRemaining = 0f;
        _nightModeRequested = false;
        _lastEditorUpdateTime = UnityEditor.EditorApplication.timeSinceStartup;

        SubscribeEditorUpdate();
        RefreshEditModeSimulationState();
    }

    public void StopEditModeSimulation()
    {
        if (Application.isPlaying)
        {
            return;
        }

        _editModeSimulationActive = false;
        StopAllRunningCoroutines();
        _editModeShutdownActive = _activeWindowCount > 0;
        _nightModeRequested = false;
        _editorSimulationPhase = _editModeShutdownActive ? EditorSimulationPhase.ShutdownRunning : EditorSimulationPhase.Idle;
        _editorPhaseTimeRemaining = _editModeShutdownActive ? GetShutdownDelaySeconds() : 0f;
        _targetDayLitCount = 0;
        _lastEditorUpdateTime = UnityEditor.EditorApplication.timeSinceStartup;

        if (_editModeShutdownActive)
        {
            SubscribeEditorUpdate();
        }
        else
        {
            UnsubscribeEditorUpdate();
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneView.RepaintAll();
    }

    private void RefreshEditModeSimulationState()
    {
        if (!_editModeSimulationActive)
        {
            return;
        }

        if (WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            bool shouldActivateNightMode = solarLightData.DaylightFactor <= _activationDaylightThreshold;
            _nightModeRequested = !shouldActivateNightMode;
            ApplySolarState(shouldActivateNightMode, true);
        }
        else
        {
            SetAllWindowsImmediate(false);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneView.RepaintAll();
    }

    private void BeginNightModeInEditor()
    {
        _editorSimulationPhase = EditorSimulationPhase.WaitingForNightStartDelay;
        _editorPhaseTimeRemaining = GetScaledDelaySeconds(_nightStartDelayRange);
    }

    private void BeginShutdownModeInEditor()
    {
        PrepareDayPlan();
        _editorSimulationPhase = EditorSimulationPhase.ShutdownRunning;
        _editorPhaseTimeRemaining = GetShutdownDelaySeconds();
    }

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
        if (this == null || !isActiveAndEnabled || Application.isPlaying || (!_editModeSimulationActive && !_editModeShutdownActive))
        {
            return;
        }

        if (_editModeSimulationActive && WeatherSolarController.TryGetCurrentSunLightState(out WeatherSolarController.SolarLightChangeData solarLightData))
        {
            bool shouldActivateNightMode = solarLightData.DaylightFactor <= _activationDaylightThreshold;
            if (shouldActivateNightMode != _nightModeRequested)
            {
                ApplySolarState(shouldActivateNightMode, true);
            }
        }

        double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Max(0f, (float)(editorTime - _lastEditorUpdateTime));
        _lastEditorUpdateTime = editorTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        bool changedState = StepEditorSimulation(deltaTime);
        if (!changedState)
        {
            return;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneView.RepaintAll();
    }

    private bool StepEditorSimulation(float deltaTime)
    {
        bool changedState = false;
        float remainingDelta = deltaTime;

        while (remainingDelta > 0f)
        {
            if (_editorPhaseTimeRemaining > 0f)
            {
                float consumed = Mathf.Min(_editorPhaseTimeRemaining, remainingDelta);
                _editorPhaseTimeRemaining -= consumed;
                remainingDelta -= consumed;

                if (_editorPhaseTimeRemaining > 0f)
                {
                    break;
                }
            }

            switch (_editorSimulationPhase)
            {
                case EditorSimulationPhase.WaitingForNightStartDelay:
                    PrepareNightPlan();
                    _editorSimulationPhase = EditorSimulationPhase.NightRunning;
                    return true;

                case EditorSimulationPhase.NightRunning:
                    if (!_nightModeRequested)
                    {
                        _editorSimulationPhase = EditorSimulationPhase.Idle;
                        break;
                    }

                    bool activatedWindow = TryApplyNightStep();
                    float nightDelay = GetNextNightDelaySeconds(activatedWindow);
                    _nightElapsedSimulatedMinutes += nightDelay * WeatherSolarController.GetActiveSimulatedMinutesPerRealSecond();
                    _editorPhaseTimeRemaining = nightDelay;
                    if (!activatedWindow)
                    {
                        return changedState;
                    }

                    return true;

                case EditorSimulationPhase.ShutdownRunning:
                    if (_nightModeRequested)
                    {
                        _editorSimulationPhase = EditorSimulationPhase.Idle;
                        break;
                    }

                    if (_activeWindowCount == _targetDayLitCount)
                    {
                        _editorSimulationPhase = EditorSimulationPhase.Idle;
                        return changedState;
                    }

                    bool shouldTurnOn = _activeWindowCount < _targetDayLitCount;
                    int windowIndex = GetRandomWindowIndexByState(!shouldTurnOn);
                    if (windowIndex < 0)
                    {
                        _editorSimulationPhase = EditorSimulationPhase.Idle;
                        _editModeShutdownActive = false;
                        return changedState;
                    }

                    SetWindowState(windowIndex, shouldTurnOn);
                    _editorPhaseTimeRemaining = GetShutdownDelaySeconds();
                    if (_activeWindowCount == 0 && !_editModeSimulationActive)
                    {
                        _editModeShutdownActive = false;
                        _editorSimulationPhase = EditorSimulationPhase.Idle;
                        UnsubscribeEditorUpdate();
                    }
                    return true;

                default:
                    return changedState;
            }
        }

        return changedState;
    }

    private enum EditorSimulationPhase
    {
        Idle,
        WaitingForNightStartDelay,
        NightRunning,
        ShutdownRunning
    }
#endif
}
