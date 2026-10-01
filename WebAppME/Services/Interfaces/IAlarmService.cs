using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;

namespace BatteryTestingSystem.Services.Interfaces
{
    /// <summary>One request to raise an alarm. Producers build this and nothing else.</summary>
    public class AlarmRequest
    {
        /// <summary>Stable identity of the fault; repeats of the same key collapse.</summary>
        public string AlarmKey { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; } = SeverityLevel.WARNING;
        public AlarmSource Source { get; set; } = AlarmSource.NONE;
        public string? DeviceId { get; set; }
        public int? BoardNumber { get; set; }
        public int? ChannelNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public enum AlarmChangeKind
    {
        Raised = 1,
        Collapsed = 2,
        Acknowledged = 3,
        Cleared = 4
    }

    /// <summary>
    /// Fan-out payload. ShouldEscalate means "this deserves the operator's attention now";
    /// the client decides whether that becomes a toast, a sound, or both, from user prefs.
    /// </summary>
    public record AlarmChanged(AlarmLog Alarm, AlarmChangeKind Kind, bool ShouldEscalate);

    public interface IAlarmService
    {
        Task RaiseAsync(AlarmRequest req);
        Task<bool> AcknowledgeAsync(long id, string user);
        Task<int> AcknowledgeAllAsync(string user);
        Task<bool> ClearAsync(string alarmKey);
        Task<IReadOnlyList<AlarmLog>> GetActiveAsync(int take);
        Task<int> GetUnacknowledgedCountAsync();
        /// <summary>Writes any debounced repeat bumps. Called by the flush timer and on shutdown.</summary>
        Task FlushPendingAsync();
        AlarmPolicy Policy { get; }
        event Action<AlarmChanged>? OnChanged;
    }
}
