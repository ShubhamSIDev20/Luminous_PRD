using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Alarms
{
    /// <summary>
    /// Result of the escalation decision for one raise.
    /// Escalate = interrupt the operator (toast / sound). StormTriggered = this raise tripped
    /// the storm guard and the caller should emit one summary alarm.
    /// </summary>
    public record EscalationDecision(bool Escalate, bool StormTriggered, int WindowCount);

    /// <summary>
    /// Decides how often an operator is interrupted. Deliberately separate from the alarm row
    /// itself: the row always records every occurrence (the truth), this class only governs
    /// attention. Pure and thread-safe; no I/O.
    /// </summary>
    public class AlarmPolicy
    {
        private sealed class KeyState
        {
            public DateTime? LastEscalatedUtc;
            public DateTime? StormSuppressedUntilUtc;
            public readonly List<DateTime> Window = new();
        }

        private readonly AlarmOptions _options;
        private readonly Dictionary<string, KeyState> _keys = new();
        private readonly Dictionary<string, DateTime> _mutedDevices = new();
        private readonly object _lock = new();

        private DateTime? _mutedAllUntilUtc;

        public AlarmPolicy(AlarmOptions options)
        {
            _options = options;
        }

        public void MuteAll(DateTime untilUtc)
        {
            lock (_lock) { _mutedAllUntilUtc = untilUtc; }
        }

        public void MuteDevice(string deviceId, DateTime untilUtc)
        {
            lock (_lock) { _mutedDevices[deviceId] = untilUtc; }
        }

        public void Unmute()
        {
            lock (_lock)
            {
                _mutedAllUntilUtc = null;
                _mutedDevices.Clear();
            }
        }

        public bool IsMuted(string? deviceId, DateTime nowUtc)
        {
            lock (_lock) { return IsMutedNoLock(deviceId, nowUtc); }
        }

        private bool IsMutedNoLock(string? deviceId, DateTime nowUtc)
        {
            if (_mutedAllUntilUtc.HasValue && _mutedAllUntilUtc.Value > nowUtc)
                return true;

            return deviceId != null
                   && _mutedDevices.TryGetValue(deviceId, out var until)
                   && until > nowUtc;
        }

        public EscalationDecision Evaluate(string alarmKey, SeverityLevel severity, string? deviceId, DateTime nowUtc)
        {
            lock (_lock)
            {
                if (!_keys.TryGetValue(alarmKey, out var state))
                {
                    state = new KeyState();
                    _keys[alarmKey] = state;
                }

                // Slide the storm window.
                var windowStart = nowUtc.AddMinutes(-_options.StormWindowMinutes);
                state.Window.RemoveAll(t => t < windowStart);
                state.Window.Add(nowUtc);
                var windowCount = state.Window.Count;

                // Trip the storm guard once, on the raise that crosses the threshold.
                var stormTriggered = false;
                if (windowCount > _options.StormThreshold
                    && (state.StormSuppressedUntilUtc == null || state.StormSuppressedUntilUtc <= nowUtc))
                {
                    stormTriggered = true;
                    state.StormSuppressedUntilUtc = nowUtc.AddMinutes(_options.StormCooloffMinutes);
                }

                if (stormTriggered)
                    return new EscalationDecision(false, true, windowCount);

                if (state.StormSuppressedUntilUtc.HasValue && state.StormSuppressedUntilUtc.Value > nowUtc)
                    return new EscalationDecision(false, false, windowCount);

                // Only Error and Critical interrupt the operator.
                if (severity < SeverityLevel.ERROR)
                    return new EscalationDecision(false, false, windowCount);

                if (IsMutedNoLock(deviceId, nowUtc))
                    return new EscalationDecision(false, false, windowCount);

                if (state.LastEscalatedUtc.HasValue
                    && (nowUtc - state.LastEscalatedUtc.Value).TotalSeconds < _options.EscalationCooldownSec)
                    return new EscalationDecision(false, false, windowCount);

                state.LastEscalatedUtc = nowUtc;
                return new EscalationDecision(true, false, windowCount);
            }
        }
    }
}
