using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>
/// What one channel currently reads. Assembled by the page from ChannelManager. Voltage/Current
/// drive the node face and edge animation; everything from Power onward is dashboard-depth data
/// that only the properties dock shows on selection (see WorkflowTelemetryBridge.BuildEntries).
/// All trailing fields default to zero/null so existing positional test calls keep compiling.
/// </summary>
public record ChannelTelemetry(
    CircuitStatus Status,

    /// <summary>
    /// Null means UNKNOWN, not empty. Nothing in the hardware protocol carries a percentage, so
    /// this is always derived - see StateOfChargeEstimator, which returns null when there is no
    /// battery or its rated capacity is unusable.
    /// </summary>
    double? Soc,

    double Voltage,
    double Current,
    double Power = 0,
    double Temperature = 0,
    double AccumulatedCapacity = 0,
    double ChargeCapacity = 0,
    double DischargeCapacity = 0,
    double StepCapacity = 0,
    double AccumulatedEnergy = 0,
    double ChargeEnergy = 0,
    double DischargeEnergy = 0,
    double StepEnergy = 0,
    int CycleNumber = 0,
    int TableStepNumber = 0,
    int TableTotalRowNumber = 0,
    TimeSpan RunningTime = default,
    TimeSpan StepRunningTime = default,
    int? ErrorId = null,
    int? SystemErrorId = null,
    ProgramRunningStatus ProgramStatus = ProgramRunningStatus.Stop,
    DateTime TimeStamp = default,
    bool IsConnected = true,

    // Sub-project C1 — the remaining dashboard-card fields. Numeric ones come straight off
    // RealTimeRecord/Session; the text ones are RESOLVED where the IChannelCommandHandler is in
    // scope (operator name via OperatorConstants, battery/program/session off the handler's own
    // config records), for the same reason ErrorText is: this DTO must stay a plain data carrier
    // the bridge can format without reaching back into hardware.
    int StepNumber = 0,
    int CycleStatus = 0,
    int CycleRunIteration = 0,
    int StoredRecordCount = 0,
    int UnstoredRecordCount = 0,
    string? OperatorName = null,
    string? BatteryName = null,
    string? ProgramName = null,
    string? SessionId = null,

    // The attached battery's rated figures, straight off BatteryDTO. Static for the session, so
    // they ride along here and are written to the DOM once rather than recomputed per tick. They
    // feed BOTH the battery cell's spec line and the SoC derivation above.
    //
    // Nullable because the battery itself may be absent; note that BatteryDTO's own fields are
    // non-nullable and default to 0, so "unset" arrives as zero and the bridge must treat a
    // non-positive figure as absent rather than render "0 V".
    float? NominalCapacity = null,
    float? NominalVoltage = null,
    int? NumberOfCells = null);

/// <summary>
/// A device- or board-level summary line, written to the DOM the same way channel telemetry is —
/// as a text write into a data-role slot, never a Blazor parameter. Online state changes whenever
/// hardware does; rendering it server-side would re-render every node on the canvas per event.
/// </summary>
public record NodeRollup(string NodeId, string OnlineText, bool IsOnline);

/// <summary>
/// One CSS-ready row, serialized straight to JS. StatusHsl is a raw triplet such as
/// "51 100% 50%" because the stylesheet consumes it as hsl(var(--wf-status) / alpha) —
/// sending a variable name instead would produce no colour and no error.
/// Flow: 1 charging, -1 discharging, 0 still.
///
/// Power/Temperature paint the node face. Everything from AccumulatedCapacity onward paints the
/// properties dock's live-data section for whichever channel is currently selected — pre-formatted
/// here (not as raw doubles/TimeSpans) so the JS layer stays a dumb text-setter with no formatting
/// logic of its own, matching how Voltage/Current/Soc are already sent as ready-to-write strings.
/// </summary>
public record TelemetryEntry(
    string NodeId,
    string StatusHsl,
    string StatusName,

    /// <summary>
    /// 0 when unknown, so the hsl()/calc() expressions in the stylesheet always parse. Check
    /// SocKnown before believing it.
    /// </summary>
    double Soc,

    /// <summary>
    /// False when there is no battery or no usable rated capacity. The cell then renders an
    /// explicit unknown state rather than an empty one - on a bench, "we do not know" and "flat"
    /// must not look the same.
    /// </summary>
    bool SocKnown,

    /// <summary>Pre-formatted, e.g. "62%" or "--%", because the JS layer is a dumb text-setter
    /// by design.</summary>
    string SocText,

    /// <summary>The battery's rated figures as one line, e.g. "50 Ah · 3.7 V · 1 cell". Null
    /// when nothing is known.</summary>
    string? BatteryLine,

    int Flow,
    double Voltage,
    double Current,
    double Power,
    double Temperature,
    double AccumulatedCapacity,
    double ChargeCapacity,
    double DischargeCapacity,
    double StepCapacity,
    double AccumulatedEnergy,
    double ChargeEnergy,
    double DischargeEnergy,
    double StepEnergy,
    int CycleNumber,
    string TableProgress,
    string RunningTime,
    string StepRunningTime,
    string? ErrorText,
    string ProgramStatusText,
    string LastUpdateText,
    IReadOnlyDictionary<string, string> Properties);
