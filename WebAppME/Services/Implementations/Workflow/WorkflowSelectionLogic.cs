using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public enum SelectionOutcome { NothingEligible, AllAdded, PartiallyAdded }

public readonly record struct SelectionResult(SelectionOutcome Outcome, int Added, int Skipped);

/// <summary>
/// Canvas-native mirror of DashboardView.UpdateSelectedCircuits's rule: a channel can never be
/// selected while Offline, and once one channel is selected every further addition must match
/// its (CircuitStatus, ProgramStatus) exactly - the rule that keeps a marquee or bulk action from
/// ever producing a mixed selection no action could run on uniformly.
///
/// Kept separate from CircuitSelectionLogic (the dashboard's own version) rather than
/// generalizing both onto one signature: the dashboard is keyed by (DeviceID, Board, Channel)
/// tuples over live IChannelCommandHandler objects; the canvas is keyed by string node ids over
/// ChannelTelemetry readings. Forcing a shared abstraction over that mismatch would cost more
/// than the ~30 lines of duplication it would save.
/// </summary>
public static class WorkflowSelectionLogic
{
    public static bool CanAdd(
        string candidateNodeId,
        IReadOnlyDictionary<string, ChannelTelemetry> readings,
        IReadOnlyCollection<string> selected)
    {
        if (!readings.TryGetValue(candidateNodeId, out var reading)) return false;
        if (reading.Status == CircuitStatus.Offline) return false;

        if (selected.Count == 0) return true;

        var anchorId = selected.First();
        if (!readings.TryGetValue(anchorId, out var anchor)) return true; // anchor has no reading yet

        return reading.Status == anchor.Status && reading.ProgramStatus == anchor.ProgramStatus;
    }

    /// <summary>Applies a marquee or bulk selection: every candidate that passes CanAdd joins
    /// `selected` (mutated in place); everything else is counted as skipped. The first eligible
    /// candidate becomes the anchor for the rest of THIS call if `selected` started empty -
    /// matching CircuitSelectionLogic.SelectAll's "reuse an existing anchor, else pick the first
    /// eligible member" rule.</summary>
    public static SelectionResult ApplyBulk(
        IReadOnlyCollection<string> candidateNodeIds,
        IReadOnlyDictionary<string, ChannelTelemetry> readings,
        HashSet<string> selected)
    {
        if (candidateNodeIds.Count == 0) return new SelectionResult(SelectionOutcome.NothingEligible, 0, 0);

        int added = 0, skipped = 0;
        foreach (var id in candidateNodeIds)
        {
            if (CanAdd(id, readings, selected))
            {
                if (selected.Add(id)) added++;
            }
            else
            {
                skipped++;
            }
        }

        if (added == 0) return new SelectionResult(SelectionOutcome.NothingEligible, 0, skipped);
        return new SelectionResult(
            skipped > 0 ? SelectionOutcome.PartiallyAdded : SelectionOutcome.AllAdded, added, skipped);
    }
}
