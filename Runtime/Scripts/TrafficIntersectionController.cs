using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TrafficIntersectionController : MonoBehaviour
{
    [Serializable]
    public struct ChannelStateOverride
    {
        public string channelId;
        public TrafficSignalUnit.SignalState state;
    }

    [Serializable]
    public struct TrafficTiming
    {
        public string channelId;
        [Min(0.1f)] public float greenDurationSeconds;
        [Min(0.1f)] public float yellowDurationSeconds;
        [Min(0f)] public float allRedDurationSeconds;
        public ChannelStateOverride[] greenOverrides;
        public ChannelStateOverride[] yellowOverrides;
        public ChannelStateOverride[] allRedOverrides;
    }

    private enum CycleStage
    {
        Green,
        Yellow,
        AllRed
    }

    [Header("Cycle")]
    [SerializeField] private bool _playOnEnable = true;
    [SerializeField] private bool _loop = true;
    [SerializeField] [Min(0f)] private float _startOffsetSeconds;
    [SerializeField] private TrafficTiming[] _timings = Array.Empty<TrafficTiming>();

    [Header("Auto Setup")]
    [SerializeField] private bool _autoCollectUnitsFromChildren = true;
    [SerializeField] private List<TrafficSignalUnit> _units = new List<TrafficSignalUnit>();

    [Header("Debug")]
    [SerializeField] private bool _isRunning;
    [SerializeField] private int _currentTimingIndex;
    [SerializeField] private string _currentChannelId;
    [SerializeField] private float _stageElapsedSeconds;
    [SerializeField] private string _currentStage = "Green";

    public bool IsRunning => _isRunning;
    public int CurrentTimingIndex => _currentTimingIndex;
    public IReadOnlyList<TrafficSignalUnit> Units => _units;

    private CycleStage _cycleStage;

    private void Reset()
    {
        RefreshUnits();
    }

    private void OnValidate()
    {
        if (_autoCollectUnitsFromChildren)
        {
            RefreshUnits();
        }

        for (int index = 0; index < _timings.Length; index++)
        {
            _timings[index].greenDurationSeconds = Mathf.Max(0.1f, _timings[index].greenDurationSeconds);
            _timings[index].yellowDurationSeconds = Mathf.Max(0.1f, _timings[index].yellowDurationSeconds);
            _timings[index].allRedDurationSeconds = Mathf.Max(0f, _timings[index].allRedDurationSeconds);
        }

        SyncDebugFields();
    }

    private void OnEnable()
    {
        if (_autoCollectUnitsFromChildren)
        {
            RefreshUnits();
        }

        if (_playOnEnable)
        {
            Play();
            if (_startOffsetSeconds > 0f)
            {
                AdvanceBy(_startOffsetSeconds);
            }
        }
        else
        {
            ApplyCurrentStage();
        }
    }

    private void Update()
    {
        if (!_isRunning || _timings == null || _timings.Length == 0)
        {
            return;
        }

        AdvanceBy(Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime);
    }

    [ContextMenu("Play")]
    public void Play()
    {
        _isRunning = true;
        _currentTimingIndex = Mathf.Clamp(_currentTimingIndex, 0, Mathf.Max(0, _timings.Length - 1));
        _cycleStage = CycleStage.Green;
        _stageElapsedSeconds = 0f;
        ApplyCurrentStage();
    }

    [ContextMenu("Pause")]
    public void Pause()
    {
        _isRunning = false;
    }

    [ContextMenu("Stop")]
    public void Stop()
    {
        _isRunning = false;
        _currentTimingIndex = 0;
        _cycleStage = CycleStage.Green;
        _stageElapsedSeconds = 0f;
        ApplyCurrentStage();
    }

    [ContextMenu("Next Timing")]
    public void NextTiming()
    {
        if (_timings == null || _timings.Length == 0)
        {
            return;
        }

        _currentTimingIndex = (_currentTimingIndex + 1) % _timings.Length;
        _cycleStage = CycleStage.Green;
        _stageElapsedSeconds = 0f;
        ApplyCurrentStage();
    }

    [ContextMenu("Refresh Units")]
    public void RefreshUnits()
    {
        _units ??= new List<TrafficSignalUnit>();
        _units.Clear();

        TrafficSignalUnit[] foundUnits = GetComponentsInChildren<TrafficSignalUnit>(includeInactive: true);
        for (int index = 0; index < foundUnits.Length; index++)
        {
            TrafficSignalUnit unit = foundUnits[index];
            if (unit == null)
            {
                continue;
            }

            _units.Add(unit);
        }
    }

    private void AdvanceBy(float deltaTime)
    {
        if (deltaTime <= 0f || _timings == null || _timings.Length == 0)
        {
            return;
        }

        float remaining = deltaTime;
        while (remaining > 0f)
        {
            float duration = GetCurrentStageDuration();
            float timeLeft = Mathf.Max(0f, duration - _stageElapsedSeconds);

            if (remaining < timeLeft)
            {
                _stageElapsedSeconds += remaining;
                SyncDebugFields();
                return;
            }

            remaining -= timeLeft;
            _stageElapsedSeconds = 0f;
            AdvanceStage();
            ApplyCurrentStage();
        }
    }

    private void AdvanceStage()
    {
        TrafficTiming timing = _timings[Mathf.Clamp(_currentTimingIndex, 0, _timings.Length - 1)];

        switch (_cycleStage)
        {
            case CycleStage.Green:
                _cycleStage = CycleStage.Yellow;
                break;

            case CycleStage.Yellow:
                if (timing.allRedDurationSeconds > 0f)
                {
                    _cycleStage = CycleStage.AllRed;
                }
                else
                {
                    AdvanceToNextTiming();
                }
                break;

            default:
                AdvanceToNextTiming();
                break;
        }
    }

    private void AdvanceToNextTiming()
    {
        if (_currentTimingIndex >= _timings.Length - 1)
        {
            if (!_loop)
            {
                _isRunning = false;
                _currentTimingIndex = Mathf.Clamp(_currentTimingIndex, 0, Mathf.Max(0, _timings.Length - 1));
                _cycleStage = CycleStage.Green;
                return;
            }

            _currentTimingIndex = 0;
        }
        else
        {
            _currentTimingIndex++;
        }

        _cycleStage = CycleStage.Green;
    }

    private float GetCurrentStageDuration()
    {
        if (_timings == null || _timings.Length == 0)
        {
            return 0f;
        }

        TrafficTiming timing = _timings[Mathf.Clamp(_currentTimingIndex, 0, _timings.Length - 1)];
        return _cycleStage switch
        {
            CycleStage.Green => Mathf.Max(0.1f, timing.greenDurationSeconds),
            CycleStage.Yellow => Mathf.Max(0.1f, timing.yellowDurationSeconds),
            _ => Mathf.Max(0f, timing.allRedDurationSeconds)
        };
    }

    private void ApplyCurrentStage()
    {
        if (_timings == null || _timings.Length == 0)
        {
            return;
        }

        if (_autoCollectUnitsFromChildren && (_units == null || _units.Count == 0))
        {
            RefreshUnits();
        }

        TrafficTiming timing = _timings[Mathf.Clamp(_currentTimingIndex, 0, _timings.Length - 1)];

        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
        {
            TrafficSignalUnit unit = _units[unitIndex];
            if (unit == null)
            {
                continue;
            }

            unit.ApplyState(ResolveStateForUnit(unit, timing));
        }

        SyncDebugFields();
    }

    private TrafficSignalUnit.SignalState ResolveStateForUnit(TrafficSignalUnit unit, TrafficTiming timing)
    {
        if (unit == null)
        {
            return TrafficSignalUnit.SignalState.Red;
        }

        if (_cycleStage == CycleStage.Green)
        {
            if (string.Equals(unit.ChannelId, timing.channelId, StringComparison.Ordinal))
            {
                return unit.Kind == TrafficSignalUnit.SignalKind.Pedestrian
                    ? TrafficSignalUnit.SignalState.Walk
                    : TrafficSignalUnit.SignalState.Green;
            }

            if (TryResolveOverride(unit, timing.greenOverrides, out TrafficSignalUnit.SignalState greenOverride))
            {
                return greenOverride;
            }
        }
        else if (_cycleStage == CycleStage.Yellow)
        {
            if (string.Equals(unit.ChannelId, timing.channelId, StringComparison.Ordinal))
            {
                return unit.Kind == TrafficSignalUnit.SignalKind.Pedestrian
                    ? TrafficSignalUnit.SignalState.FlashingDontWalk
                    : TrafficSignalUnit.SignalState.Yellow;
            }

            if (TryResolveOverride(unit, timing.yellowOverrides, out TrafficSignalUnit.SignalState yellowOverride))
            {
                return yellowOverride;
            }
        }
        else if (TryResolveOverride(unit, timing.allRedOverrides, out TrafficSignalUnit.SignalState allRedOverride))
        {
            return allRedOverride;
        }

        return GetDefaultRestState(unit);
    }

    private static bool TryResolveOverride(TrafficSignalUnit unit, ChannelStateOverride[] overrides, out TrafficSignalUnit.SignalState state)
    {
        if (unit != null && overrides != null)
        {
            for (int index = 0; index < overrides.Length; index++)
            {
                ChannelStateOverride entry = overrides[index];
                if (string.Equals(entry.channelId, unit.ChannelId, StringComparison.Ordinal))
                {
                    state = entry.state;
                    return true;
                }
            }
        }

        state = default;
        return false;
    }

    private static TrafficSignalUnit.SignalState GetDefaultRestState(TrafficSignalUnit unit)
    {
        return unit.Kind == TrafficSignalUnit.SignalKind.Pedestrian
            ? TrafficSignalUnit.SignalState.DontWalk
            : TrafficSignalUnit.SignalState.Red;
    }

    private void SyncDebugFields()
    {
        if (_timings == null || _timings.Length == 0)
        {
            _currentChannelId = string.Empty;
            _currentStage = CycleStage.Green.ToString();
            return;
        }

        TrafficTiming timing = _timings[Mathf.Clamp(_currentTimingIndex, 0, _timings.Length - 1)];
        _currentChannelId = timing.channelId;
        _currentStage = _cycleStage.ToString();
    }
}
