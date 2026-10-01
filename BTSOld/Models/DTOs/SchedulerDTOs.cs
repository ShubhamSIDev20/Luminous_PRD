namespace BatteryTestingSystem.Models.DTOs;

public class ScheduleCircuitTarget
{
    public int DeviceId { get; set; }
    public int CircuitId { get; set; }
}

public class ProgramScheduleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public long BatteryId { get; set; }
    public string BatteryName { get; set; } = string.Empty;
    public long? Port1DbcFileId { get; set; }
    public string? Port1DbcFileName { get; set; }
    public long? Port2DbcFileId { get; set; }
    public string? Port2DbcFileName { get; set; }
    public long? Port3DbcFileId { get; set; }
    public string? Port3DbcFileName { get; set; }
    public DateTime ScheduledAt { get; set; }
    public List<ScheduleCircuitTarget> TargetCircuits { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string? HangfireJobId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public long ProgramId { get; set; }
    public long BatteryId { get; set; }
    public long? Port1DbcFileId { get; set; }
    public long? Port2DbcFileId { get; set; }
    public long? Port3DbcFileId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public List<ScheduleCircuitTarget> TargetCircuits { get; set; } = new();
}

public class ScheduleExecutionLogDto
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public int DeviceId { get; set; }
    public int CircuitId { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailReason { get; set; }
}
