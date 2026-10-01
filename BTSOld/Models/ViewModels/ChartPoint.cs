// Models/ViewModels/ChartPoint.cs
namespace BatteryTestingSystem.Models.ViewModels;

/// <summary>
/// Lightweight point used for chart downsampling results.
/// Only carries the numeric fields needed by BmsChart — no DBC data, no heavy strings.
/// </summary>
public class ChartPoint
{
    public long   Id                   { get; init; }
    public long   ProgramRunningTimeMs { get; init; }   // x-axis default (ms)
    public DateTime DateTime           { get; init; }   // x-axis when time mode
    public int    StepNumber           { get; init; }
    public float? Current              { get; init; }
    public float? Voltage              { get; init; }
    public float? Temperature          { get; init; }
    public float? Power                { get; init; }
    public float? AccumulatedCapacity  { get; init; }
    public float? ChargeCapacity       { get; init; }
    public float? DischargeCapacity    { get; init; }
    public float? StepCapacity         { get; init; }
    public float? AccumulatedEnergy    { get; init; }
    public float? ChargeEnergy         { get; init; }
    public float? DischargeEnergy      { get; init; }
    public float? StepEnergy           { get; init; }
}
