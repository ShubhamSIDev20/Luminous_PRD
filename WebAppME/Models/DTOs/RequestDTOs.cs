using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Services;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs;

#region Device Controls
public class CommandRequest
{
    public StartByte Start { get; set; }
    public int DeviceId { get; set; }
    public int SecondaryBoardNumber { get; set; }
    public int ChannelId { get; set; }
    public byte QueryId { get; set; }
    public byte? Range { get; set; }

    // Optional payload (e.g., Epoch time, program metadata, program steps)
    public byte[]? Data { get; set; }
    public byte? SingleByte { get; set; }

}

public class recordStoreRequest 
{
    public string filePath { get; set; } = string.Empty;
    public List<MeasurementData> RealStoreRecord { get; set; } = new();
    //public List<Dictionary<string, object?>>? DbcValues { get; set; }

}

public class recordRequest
{
    public RealTimeRecordDto RealTimeRecord { get; set; } = new();
    public string filePath { get; set; } = string.Empty;
    public Dictionary<string, object>? DbcValues { get; set; }
    public List<IOStatus> IOStatus { get; set; } = new();

    public event Action? OnDataChanged;

    public void NotifyDataChanged(RealTimeRecordDto data)
    {
        RealTimeRecord = data;
        OnDataChanged?.Invoke();
    }
}

public class DbcRecord
{
    public Dictionary<string, object>? DbcValues { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class SessionRequest
{
    public string filePath { get; set; } = string.Empty;
    public ProgramDTO? Program{ get; set; }
    public BatteryDTO? Battery { get; set; }
    public DbcDatabase? dbcDatabase { get; set; }

    // PRODUCER feature: referenced sub-programs to dump into session for self-contained playback
    public List<ProgramDTO>? ProducerPrograms { get; set; }

    // TABLE feature: table file contents dumped into session for self-contained playback
    // key = fileName, value = file lines
    public Dictionary<string, string[]>? TableFileData { get; set; }

    // Expanded step list (PRODUCER steps inlined) — used for correct step-number mapping
    // between hardware reports and the UI (hardware uses expanded numbering)
    public List<StepModel>? ExpandedProgramSteps { get; set; }
}
#endregion




#region ProgramBuilder
public class CreateProgramRequest
{
    public long? id { get; set; }

    [Required]
    public string Name { get; set; }
    public string? Description { get; set; }
    public float? MaxAh { get; set; }
    public long? ProgramTimeTicks { get; set; }
    public string? ProgramJson { get; set; }


}

#endregion