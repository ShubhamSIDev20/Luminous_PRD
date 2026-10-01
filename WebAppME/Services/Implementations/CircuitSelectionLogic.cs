using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations;

public enum SelectAllOutcome
{
    /// <summary>The filtered view itself was empty - nothing to select, and nothing to warn about.</summary>
    NothingToSelect,

    /// <summary>The filtered view had circuits, but none were online enough to anchor a selection.</summary>
    NoOnlineCircuit,

    /// <summary>Every eligible circuit in the filtered view matched the anchor's state.</summary>
    AllSelected,

    /// <summary>At least one circuit was skipped - Offline, or a different (CircuitStatus, ProgramStatus) than the anchor.</summary>
    PartiallySelected
}

public readonly record struct SelectAllResult(
    SelectAllOutcome Outcome,
    int Added,
    int Skipped,
    CircuitStatus? AnchorCircuitStatus,
    ProgramRunningStatus? AnchorProgramStatus);

/// <summary>
/// DI-free extraction of DashboardView's circuit-filtering and bulk-selection rules (T-38),
/// mirroring the same isolation DashboardRenderBatcher already gets. DashboardView.razor
/// injects ChannelManager - a BackgroundService owning the real TCP/UDP hardware listeners -
/// which makes the page itself unsafe to render in a unit test. Pulling the pure decision
/// logic out here means it can be tested directly, without touching Razor, DI, or hardware.
///
/// Every method is static and takes its inputs as parameters rather than owning state: the
/// page still owns `circuits`, `selectedCircuits`, and the filter fields, and is responsible
/// for side effects (Toast, StateHasChanged) based on the result returned here.
/// </summary>
public static class CircuitSelectionLogic
{
    /// <summary>
    /// The single definition of "visible" - access, current visibility set, status chip, and
    /// the Online-only toggle. VirtualRows and SelectAll must both filter through this, or a
    /// bulk action can silently diverge from what's actually rendered on screen.
    /// </summary>
    public static List<IChannelCommandHandler> Filter(
        IEnumerable<IChannelCommandHandler> circuits,
        HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelID)> accessCircuits,
        HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelID)> visibleCircuits,
        CircuitStatus? statusChip,
        bool onlineOnly)
    {
        return circuits.Where(dev =>
        {
            var key = (dev.Channel.DeviceID, dev.Channel.SecondaryBoardNumber, dev.Channel.ChannelNumber);
            bool inAccess = accessCircuits.Contains(key);
            bool inVisible = visibleCircuits.Count == 0 || visibleCircuits.Contains(key);
            bool inChip = statusChip == null || dev.RealTime?.RealTimeRecord?.CircuitStatus == statusChip.Value;
            bool inOnline = !onlineOnly || dev.RealTime?.RealTimeRecord?.CircuitStatus != CircuitStatus.Offline;
            return inAccess && inVisible && inChip && inOnline;
        }).ToList();
    }

    /// <summary>
    /// Resolves the anchor circuit - the FIRST circuit that was selected. All new selections
    /// must match its CircuitStatus + ProgramStatus.
    /// </summary>
    public static IChannelCommandHandler? ResolveAnchor(
        IEnumerable<IChannelCommandHandler> circuits,
        HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelID)> selectedCircuits)
    {
        if (selectedCircuits.Count == 0) return null;

        var key = selectedCircuits.First();
        return circuits.FirstOrDefault(c =>
            c.Channel.DeviceID == key.DeviceID &&
            c.Channel.SecondaryBoardNumber == key.SecondaryBoardNumber &&
            c.Channel.ChannelNumber == key.ChannelID);
    }

    /// <summary>
    /// Selects every circuit in <paramref name="filtered"/> that shares the anchor's
    /// CircuitStatus + ProgramStatus, mutating <paramref name="selectedCircuits"/> in place
    /// (mirroring the per-card rule so a bulk action can never produce a mixed selection).
    ///
    /// An existing anchor is reused rather than re-picked, or Select All could widen a
    /// homogeneous selection into a mixed one; only fall back to the first eligible circuit
    /// when nothing is selected yet. Offline is never eligible for the anchor or as a member -
    /// the per-card path rejects Offline outright, so anchoring on one would select a set no
    /// action could run on.
    /// </summary>
    public static SelectAllResult SelectAll(
        List<IChannelCommandHandler> filtered,
        IChannelCommandHandler? existingAnchor,
        HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelID)> selectedCircuits)
    {
        if (filtered.Count == 0)
            return new SelectAllResult(SelectAllOutcome.NothingToSelect, 0, 0, null, null);

        var anchor = existingAnchor
                     ?? filtered.FirstOrDefault(c => c.RealTime?.RealTimeRecord?.CircuitStatus != CircuitStatus.Offline);

        if (anchor == null)
            return new SelectAllResult(SelectAllOutcome.NoOnlineCircuit, 0, 0, null, null);

        var anchorCS = anchor.RealTime.RealTimeRecord.CircuitStatus;
        var anchorPS = anchor.RealTime.RealTimeRecord.ProgramStatus;

        int added = 0, skipped = 0;

        foreach (var dev in filtered)
        {
            if (dev.RealTime?.RealTimeRecord?.CircuitStatus == CircuitStatus.Offline)
            {
                skipped++;
                continue;
            }

            if (dev.RealTime?.RealTimeRecord?.CircuitStatus == anchorCS &&
                dev.RealTime?.RealTimeRecord?.ProgramStatus == anchorPS)
            {
                if (selectedCircuits.Add((dev.Channel.DeviceID, dev.Channel.SecondaryBoardNumber, dev.Channel.ChannelNumber)))
                    added++;
            }
            else
            {
                skipped++;
            }
        }

        return new SelectAllResult(
            skipped > 0 ? SelectAllOutcome.PartiallySelected : SelectAllOutcome.AllSelected,
            added, skipped, anchorCS, anchorPS);
    }
}
