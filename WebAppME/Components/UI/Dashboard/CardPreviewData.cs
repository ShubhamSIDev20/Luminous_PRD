using Blazicons;

namespace BatteryTestingSystem.Components.UI.Dashboard;

/// <summary>
/// Single source of truth for card-property metadata and preview sample values,
/// shared by <c>CardSettings.razor</c> (quick dialog) and
/// <c>Components/Pages/Settings/CardConfiguration.razor</c> (full settings page)
/// so both surfaces always offer the same properties and preview them identically —
/// they must never fall out of sync with each other or with what a real card can show.
/// </summary>
public static class CardPreviewData
{
    public record PropertyMetadata(string Label, SvgIcon Icon);

    public static readonly List<string> DefaultVisibleProperties = new()
    {
        "Voltage", "Current", "Power", "Temperature",
        "BatteryID", "ProgramID", "SessionID", "StepNumber", "OperatorCode", "StepRunningTime", "RunningTime", "Storerecordcount"
    };

    public static readonly Dictionary<string, PropertyMetadata> RealTimeProperties = new()
    {
        ["Current"] = new("Current (A)", Lucide.Activity),
        ["Voltage"] = new("Voltage (V)", Lucide.Zap),
        ["Power"] = new("Power (W)", Lucide.Battery),
        ["Temperature"] = new("Temperature (°C)", Lucide.Thermometer),
        ["AccumulatedCapacity"] = new("Accumulated Capacity (Ah)", Lucide.Gauge),
        ["ChargeCapacity"] = new("Charge Capacity (Ah)", Lucide.BatteryCharging),
        ["DischargeCapacity"] = new("Discharge Capacity (Ah)", Lucide.BatteryLow),
        ["StepCapacity"] = new("Step Capacity (Ah)", Lucide.ArrowRight),
        ["AccumulatedEnergy"] = new("Accumulated Energy (Wh)", Lucide.Gauge),
        ["ChargeEnergy"] = new("Charge Energy (Wh)", Lucide.BatteryCharging),
        ["DischargeEnergy"] = new("Discharge Energy (Wh)", Lucide.BatteryLow),
        ["StepEnergy"] = new("Step Energy (Wh)", Lucide.ArrowRight)
    };

    public static readonly Dictionary<string, PropertyMetadata> ConfigProperties = new()
    {
        ["BatteryID"] = new("Battery ID", Lucide.BatteryCharging),
        ["ProgramID"] = new("Program ID", Lucide.FileCode),
        ["SessionID"] = new("Session ID", Lucide.Hash)
    };

    public static readonly Dictionary<string, PropertyMetadata> ProgramProperties = new()
    {
        ["CycleNumber"] = new("Cycle Number", Lucide.Repeat),
        ["CycleStatus"] = new("Cycle Status", Lucide.CircleDot),
        ["CycleRunIteration"] = new("Cycle Run Iteration", Lucide.RefreshCw),
        ["TableStepNumber"] = new("Table Step", Lucide.Table),
        ["TableTotalRowNumber"] = new("Table Total Rows", Lucide.Rows4),
        ["StepNumber"] = new("Step Number", Lucide.ListOrdered),
        ["OperatorCode"] = new("Operator", Lucide.ChevronRight),
        ["StepRunningTime"] = new("Step Time", Lucide.Clock),
        ["RunningTime"] = new("Total Time", Lucide.Timer),
        ["Storerecordcount"] = new("Saved Records", Lucide.Download),
        ["Unstorerecordcount"] = new("Unsaved Records", Lucide.SaveOff),
        ["Error"] = new("Error Messages", Lucide.CircleAlert),
        ["SystemError"] = new("System Error", Lucide.ShieldAlert)
    };

    /// <summary>Sample values used only to drive the static preview cards — no device I/O.</summary>
    public static readonly Dictionary<string, double> NumericPreviewValues = new()
    {
        ["Current"] = 1.25,
        ["Voltage"] = 3.72,
        ["Power"] = 4.65,
        ["Temperature"] = 28.4,
        ["AccumulatedCapacity"] = 12.34,
        ["ChargeCapacity"] = 5.67,
        ["DischargeCapacity"] = 4.32,
        ["StepCapacity"] = 1.11,
        ["AccumulatedEnergy"] = 45.6,
        ["ChargeEnergy"] = 20.1,
        ["DischargeEnergy"] = 15.3,
        ["StepEnergy"] = 3.2
    };

    public static readonly Dictionary<string, string> TextPreviewValues = new()
    {
        ["BatteryID"] = "BAT-0042",
        ["ProgramID"] = "PRG-07",
        ["SessionID"] = "SES-1029",
        ["CycleNumber"] = "3",
        ["CycleStatus"] = "Running",
        ["CycleRunIteration"] = "1",
        ["TableStepNumber"] = "4",
        ["TableTotalRowNumber"] = "120",
        ["StepNumber"] = "12",
        ["OperatorCode"] = "OP-01",
        ["StepRunningTime"] = "00:12:34",
        ["RunningTime"] = "02:45:10",
        ["Storerecordcount"] = "1024",
        ["Unstorerecordcount"] = "0",
        ["Error"] = "None",
        ["SystemError"] = "None"
    };

    public static readonly CardConfig DefaultConfig = new()
    {
        CardSize = 210,
        FontSize = 12,
        RefreshRate = 200,
        ShowDecimal = 2,
        DisplayMode = DisplayMode.Normal,
        VisibleProperties = new List<string>(DefaultVisibleProperties)
    };

    /// <summary>All toggleable properties, grouped in display order: RealTime, then Config, then Program.</summary>
    public static IEnumerable<(string Section, KeyValuePair<string, PropertyMetadata> Property)> AllPropertiesBySection()
    {
        foreach (var p in RealTimeProperties) yield return ("RealTime Data", p);
        foreach (var p in ConfigProperties) yield return ("Configuration", p);
        foreach (var p in ProgramProperties) yield return ("Program Data", p);
    }

    public static PropertyMetadata? FindPropertyMetadata(string key)
    {
        if (RealTimeProperties.TryGetValue(key, out var rt)) return rt;
        if (ConfigProperties.TryGetValue(key, out var cfg)) return cfg;
        if (ProgramProperties.TryGetValue(key, out var prog)) return prog;
        return null;
    }
}
