using System.Globalization;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Turns raw channel telemetry into CSS-ready rows for one batched interop call.
///
/// The design constraint (spec 6): telemetry must NEVER trigger a Blazor re-render. At 64
/// channels and 5 Hz, re-rendering would be 320 component diffs a second over SignalR. Instead
/// each tick becomes a single JS call that writes CSS custom properties, and the stylesheet does
/// the colouring, glowing, flowing and filling.
///
/// The theme HSL triplets are duplicated here from app.css because a server-side component
/// cannot read a CSS variable's computed value. A test pins that every variable exists; if a
/// theme colour is ever changed, change it here too.
/// </summary>
public static class WorkflowTelemetryBridge
{
    private static readonly Dictionary<string, string> StatusHslByName = new()
    {
        ["idle"] = "0 0% 62%",
        ["charge"] = "51 100% 50%",
        ["discharging"] = "33 100% 50%",
        ["pause"] = "207 90% 54%",
        ["continue"] = "122 39% 49%",
        ["interrupt"] = "37 75% 35%",
        ["error"] = "4 90% 58%",
        ["msg"] = "187 100% 42%",
        ["offline"] = "0 0% 62%",
    };

    /// <summary>
    /// The channels worth subscribing to: placed, and not stale. Recompute whenever the graph
    /// changes — this set is the entire cost-control mechanism for the canvas.
    /// </summary>
    public static HashSet<long> OnCanvasChannelIds(
        WorkflowGraph graph, IReadOnlyCollection<string> staleNodeIds) =>
        graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel)
            .Where(n => !staleNodeIds.Contains(n.Id))
            .Where(n => n.EntityId is not null)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

    /// <summary>The exact colour BuildEntries assigns a node in this status — the legend and any
    /// status-filter chip must read this, never a second copy of the palette.</summary>
    public static string HslFor(CircuitStatus status)
    {
        var name = WorkflowStatusCss.Name(status);
        return StatusHslByName.TryGetValue(name, out var hsl) ? hsl : StatusHslByName["idle"];
    }

    /// <summary>
    /// The colour for a status honouring one user's canvas-only overrides (D-new), falling back to
    /// the built-in palette for anything they have not changed. Overrides are keyed by
    /// WorkflowStatusCss.Name, so an unrecognised key simply never matches and the default wins.
    /// Every surface that paints a status — node faces, legend swatches, filter chips, board pins
    /// — must resolve through here so a custom colour can never apply to only some of them.
    /// </summary>
    public static string HslFor(CircuitStatus status, IReadOnlyDictionary<string, string>? overrides)
    {
        var name = WorkflowStatusCss.Name(status);

        if (overrides is not null
            && overrides.TryGetValue(name, out var custom)
            && !string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        return HslFor(status);
    }

    /// <summary>
    /// Whether a telemetry push arriving at <paramref name="nowUtc"/> should be skipped because
    /// the user's configured refresh rate has not elapsed since the last one. default(DateTime)
    /// for <paramref name="lastPushUtc"/> means "never pushed yet" and is never throttled - a
    /// freshly opened canvas must not wait out the window before showing anything.
    /// </summary>
    public static bool ShouldThrottle(DateTime lastPushUtc, DateTime nowUtc, int refreshRateMs) =>
        lastPushUtc != default && (nowUtc - lastPushUtc).TotalMilliseconds < refreshRateMs;

    /// <summary>
    /// Direction of the edge's flow animation: +1 charging, -1 discharging, 0 still.
    ///
    /// Gated on the program actually running (D20) and on nothing else. It also required a
    /// non-negligible current, which silently suppressed the animation on a channel that reports
    /// Running + Charge with 0.00 A - a real state on this bench, and the reason the animation
    /// looked like it had disappeared. Direction comes from CircuitStatus, not from the sign of
    /// the current, so the reading was never needed to decide it.
    /// </summary>
    public static int FlowFor(CircuitStatus status, ProgramRunningStatus programStatus)
    {
        if (programStatus != ProgramRunningStatus.Running) return 0;

        return status switch
        {
            CircuitStatus.Charge => 1,
            CircuitStatus.Discharging => -1,
            _ => 0,
        };
    }

    /// <summary>
    /// The status actually painted, matching DeviceChannel.razor's own dashboard card exactly: a
    /// disconnected channel must never keep showing its last-known Charge/Discharge colour just
    /// because IsConnected is not part of the raw hardware CircuitStatus byte. Offline unless a
    /// program was left running when the link dropped, in which case Error (the dashboard's own
    /// "this needs attention" distinction between a clean disconnect and one mid-test).
    /// </summary>
    public static CircuitStatus EffectiveStatus(ChannelTelemetry reading) =>
        reading.IsConnected
            ? reading.Status
            : reading.ProgramStatus == ProgramRunningStatus.Running
                ? CircuitStatus.Error
                : CircuitStatus.Offline;

    public static IReadOnlyList<TelemetryEntry> BuildEntries(
        WorkflowGraph graph,
        IReadOnlyDictionary<long, ChannelTelemetry> telemetry,
        IReadOnlyList<CodeMessageDto>? errors = null,
        IReadOnlyList<CodeMessageDto>? messages = null,
        IReadOnlyDictionary<string, string>? statusColors = null)
    {
        errors ??= Array.Empty<CodeMessageDto>();
        messages ??= Array.Empty<CodeMessageDto>();
        var entries = new List<TelemetryEntry>();

        foreach (var node in graph.Nodes.Where(n => n.Kind == NodeKind.Channel))
        {
            if (node.EntityId is not { } channelId) continue;
            if (!telemetry.TryGetValue(channelId, out var reading)) continue;

            var status = EffectiveStatus(reading);
            var name = WorkflowStatusCss.Name(status);

            entries.Add(new TelemetryEntry(
                NodeId: node.Id,
                StatusHsl: HslFor(status, statusColors),
                StatusName: name,
                Soc: reading.Soc is { } soc ? Math.Clamp(soc, 0, 1) : 0d,
                SocKnown: reading.Soc is not null,
                SocText: reading.Soc is { } pct
                    ? Math.Round(Math.Clamp(pct, 0, 1) * 100).ToString("0", CultureInfo.InvariantCulture) + "%"
                    : "--%",
                BatteryLine: FormatBatteryLine(reading),
                Flow: FlowFor(status, reading.ProgramStatus),
                Voltage: reading.Voltage,
                Current: reading.Current,
                Power: reading.Power,
                Temperature: reading.Temperature,
                AccumulatedCapacity: reading.AccumulatedCapacity,
                ChargeCapacity: reading.ChargeCapacity,
                DischargeCapacity: reading.DischargeCapacity,
                StepCapacity: reading.StepCapacity,
                AccumulatedEnergy: reading.AccumulatedEnergy,
                ChargeEnergy: reading.ChargeEnergy,
                DischargeEnergy: reading.DischargeEnergy,
                StepEnergy: reading.StepEnergy,
                CycleNumber: reading.CycleNumber,
                TableProgress: $"{reading.TableStepNumber} / {reading.TableTotalRowNumber}",
                RunningTime: FormatDuration(reading.RunningTime),
                StepRunningTime: FormatDuration(reading.StepRunningTime),
                ErrorText: ResolveErrorText(
                    reading.Status, reading.ErrorId, reading.SystemErrorId, errors, messages),
                ProgramStatusText: reading.ProgramStatus == ProgramRunningStatus.Running
                    ? "Running" : "Stop",
                // default(DateTime) means no reading has ever arrived. Formatting it would render
                // 00:00:00, which reads as a real measurement taken at midnight.
                LastUpdateText: reading.TimeStamp == default
                    ? "--" : reading.TimeStamp.ToString("HH:mm:ss"),
                Properties: BuildNodeProperties(reading)));
        }

        return entries;
    }

    /// <summary>
    /// Every canvas-available property (WorkflowNodeProperties.AvailableKeys), pre-formatted with
    /// its unit — matching the established rule that JS is a dumb text-setter and all formatting
    /// happens here, the same way TableProgress/RunningTime/StepRunningTime already work.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildNodeProperties(ChannelTelemetry reading) =>
        new Dictionary<string, string>
        {
            // Units come from WorkflowNodeProperties.UnitFor, the hardware's own vocabulary
            // (OperatorConstants.ValidUnits / MeasurementData). The four capacities and four
            // energies each have a DISTINCT token - Ah/AhCha/AhDch/AhStep, Wh/WhCha/WhDch/WhStep -
            // where this previously printed all four as "Ah" and all four as "Wh", making eight
            // rows on a node face indistinguishable. The node face pairs these with
            // LabelWithoutUnit so the catalogue's looser "(Ah)" is not shown alongside.
            ["Voltage"] = WithUnit(reading.Voltage, "F2", "Voltage"),
            ["Current"] = WithUnit(reading.Current, "F2", "Current"),
            ["Power"] = WithUnit(reading.Power, "F2", "Power"),
            ["Temperature"] = WithUnit(reading.Temperature, "F1", "Temperature"),
            ["AccumulatedCapacity"] = WithUnit(reading.AccumulatedCapacity, "F3", "AccumulatedCapacity"),
            ["ChargeCapacity"] = WithUnit(reading.ChargeCapacity, "F3", "ChargeCapacity"),
            ["DischargeCapacity"] = WithUnit(reading.DischargeCapacity, "F3", "DischargeCapacity"),
            ["StepCapacity"] = WithUnit(reading.StepCapacity, "F3", "StepCapacity"),
            ["AccumulatedEnergy"] = WithUnit(reading.AccumulatedEnergy, "F3", "AccumulatedEnergy"),
            ["ChargeEnergy"] = WithUnit(reading.ChargeEnergy, "F3", "ChargeEnergy"),
            ["DischargeEnergy"] = WithUnit(reading.DischargeEnergy, "F3", "DischargeEnergy"),
            ["StepEnergy"] = WithUnit(reading.StepEnergy, "F3", "StepEnergy"),
            ["CycleNumber"] = reading.CycleNumber.ToString(),
            ["TableStepNumber"] = reading.TableStepNumber.ToString(),
            ["TableTotalRowNumber"] = reading.TableTotalRowNumber.ToString(),
            ["StepRunningTime"] = FormatDuration(reading.StepRunningTime),
            ["RunningTime"] = FormatDuration(reading.RunningTime),

            // Configuration — resolved names, matching what the dashboard card shows for these
            // keys (its "BatteryID" row renders the battery's NAME, not its id).
            ["BatteryID"] = OrPlaceholder(reading.BatteryName),
            ["ProgramID"] = OrPlaceholder(reading.ProgramName),
            ["SessionID"] = OrPlaceholder(reading.SessionId),

            // Program data.
            ["StepNumber"] = reading.StepNumber.ToString(),
            ["CycleStatus"] = reading.CycleStatus.ToString(),
            ["CycleRunIteration"] = reading.CycleRunIteration.ToString(),
            ["OperatorCode"] = OrPlaceholder(reading.OperatorName),
            ["Storerecordcount"] = reading.StoredRecordCount.ToString(),
            ["Unstorerecordcount"] = reading.UnstoredRecordCount.ToString(),

            // The dashboard exposes these as two separate selectable fields, so the picker does
            // too — distinct from the dock's single merged ErrorText line, which answers the
            // different question "what is wrong with this channel right now".
            ["Error"] = reading.ErrorId is > 0 ? reading.ErrorId.Value.ToString() : Placeholder,
            ["SystemError"] = reading.SystemErrorId is > 0
                ? reading.SystemErrorId.Value.ToString() : Placeholder,
        };

    private const string Placeholder = "--";

    /// <summary>An unset text field renders as "--" rather than blank: an empty row on a node face
    /// reads as a rendering bug, while "--" reads as "nothing assigned yet".</summary>
    /// <summary>Formats a measurement and appends the hardware's unit token for that property.
    /// Invariant culture: a de-DE machine would otherwise render 3.7 as "3,7".</summary>
    private static string WithUnit(double value, string format, string key)
    {
        var number = value.ToString(format, CultureInfo.InvariantCulture);
        var unit = WorkflowNodeProperties.UnitFor(key);
        return unit.Length == 0 ? number : $"{number} {unit}";
    }

    private static string OrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Placeholder : value;

    /// <summary>
    /// Per-device and per-board "N / M online" summaries.
    ///
    /// A channel counts as online when it has a reading AND that reading is not Offline. A
    /// missing reading counts as offline: a placed channel whose device never registered has no
    /// entry at all, and reporting it as online would claim hardware that is not there.
    ///
    /// The denominator is channels PLACED ON THE CANVAS, not every channel the device owns.
    /// Telemetry only arrives for placed channels — that subscription filter is the canvas's
    /// entire cost-control mechanism — so counting unplaced channels would report them offline
    /// when the truth is that nobody is watching them. "6 / 8 online" therefore means six of the
    /// eight channels you put on the canvas, which is the only claim the data supports.
    /// </summary>
    public static IReadOnlyList<NodeRollup> BuildRollups(
        WorkflowGraph graph,
        IReadOnlyDictionary<long, ChannelTelemetry> telemetry,
        Func<NodeKind, long, IReadOnlyCollection<long>> childChannelIds)
    {
        var placed = graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel && n.EntityId is not null)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

        var rollups = new List<NodeRollup>();

        foreach (var node in graph.Nodes)
        {
            if (node.Kind is not (NodeKind.Device or NodeKind.Board)) continue;
            if (node.EntityId is not { } entityId) continue;

            var children = childChannelIds(node.Kind, entityId).Where(placed.Contains).ToList();

            if (node.Kind == NodeKind.Board)
            {
                var anyConnected = children.Any(id =>
                    telemetry.TryGetValue(id, out var reading) && reading.IsConnected);
                rollups.Add(new NodeRollup(node.Id, anyConnected ? "Online" : "Offline", anyConnected));
                continue;
            }

            var online = children.Count(id =>
                telemetry.TryGetValue(id, out var reading) && reading.Status != CircuitStatus.Offline);

            rollups.Add(new NodeRollup(node.Id, $"{online} / {children.Count} online", true));
        }

        return rollups;
    }

    private static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    /// <summary>
    /// The attached battery's rated figures as one compact line, e.g. "50 Ah · 3.7 V · 1 cell".
    ///
    /// A non-positive figure is treated as ABSENT rather than shown as a zero. BatteryDTO's
    /// numeric fields are non-nullable and default to 0, so an unconfigured battery record is
    /// indistinguishable from a configured 0 - and "0 V" on a node face reads as a measurement
    /// the hardware took, not as a gap in the record. The whole line is null when nothing at all
    /// is known, so the cell can leave its (always-reserved) strip empty.
    ///
    /// The battery TYPE name is deliberately absent: circuit.Battery is a BatteryDTO, which
    /// carries only BatteryTypeId, and resolving a name would mean a lookup on a per-tick
    /// telemetry path.
    ///
    /// Invariant culture throughout - the line is itself separator-delimited, so a comma decimal
    /// separator would read as another item in it.
    /// </summary>
    private static string? FormatBatteryLine(ChannelTelemetry reading)
    {
        var parts = new List<string>(3);

        if (reading.NominalCapacity is { } ah && ah > 0)
            parts.Add(ah.ToString("0.##", CultureInfo.InvariantCulture) + " Ah");

        if (reading.NominalVoltage is { } v && v > 0)
            parts.Add(v.ToString("0.##", CultureInfo.InvariantCulture) + " V");

        if (reading.NumberOfCells is { } cells && cells > 0)
            parts.Add(cells + (cells == 1 ? " cell" : " cells"));

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// Mirrors DeviceChannel.razor's error/message resolution: an ErrorId under Error status reads
    /// from the "errors" catalog, any other status reads the same id from "messages"; with no
    /// ErrorId, a SystemErrorId is decoded as a SystemError bitmask. The network-connection special
    /// case in the dashboard (folding in en_ERR_NETWORK_CONN_FAIL when disconnected mid-run) is not
    /// replicated — the canvas has no connection-state concept to key it on.
    /// </summary>
    public static string? ResolveErrorText(
        CircuitStatus status,
        int? errorId,
        int? systemErrorId,
        IReadOnlyList<CodeMessageDto> errors,
        IReadOnlyList<CodeMessageDto> messages)
    {
        if (errorId is > 0)
        {
            var list = status == CircuitStatus.Error ? errors : messages;
            return list.FirstOrDefault(x => x.Index == errorId)?.Message;
        }

        if (systemErrorId is > 0)
        {
            var names = new List<string>();
            foreach (SystemError flag in Enum.GetValues(typeof(SystemError)))
            {
                if ((systemErrorId.Value & (int)flag) != 0) names.Add(flag.ToString());
            }
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        return null;
    }
}
