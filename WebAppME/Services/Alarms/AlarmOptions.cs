namespace BatteryTestingSystem.Services.Alarms
{
    /// <summary>
    /// Bound from the "Alarms" section of appsettings.json.
    /// </summary>
    public class AlarmOptions
    {
        public const string SectionName = "Alarms";

        /// <summary>How long alarms are kept before pruning.</summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>Minimum seconds between two escalations of the same alarm key.</summary>
        public int EscalationCooldownSec { get; set; } = 60;

        /// <summary>Occurrences of one key inside StormWindowMinutes that trip the storm guard.</summary>
        public int StormThreshold { get; set; } = 20;

        public int StormWindowMinutes { get; set; } = 5;

        /// <summary>How long escalation stays suppressed for a key after a storm trips.</summary>
        public int StormCooloffMinutes { get; set; } = 15;

        /// <summary>Minimum seconds between database writes for repeat bumps of one key.</summary>
        public int FlushIntervalSeconds { get; set; } = 5;

        /// <summary>How many alarms the bell loads.</summary>
        public int BellActiveTake { get; set; } = 50;
    }
}
