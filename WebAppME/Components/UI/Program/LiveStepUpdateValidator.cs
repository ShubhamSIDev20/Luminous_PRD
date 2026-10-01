namespace BatteryTestingSystem.Components.UI;

/// <summary>
/// Client-side echo of the Q9 checks the Primary makes before ever putting a frame on the wire
/// (bm_program_v3.2.md REASON 0x01-0x04) - lets the dialog reject obviously-invalid edits before
/// a round trip, and lets ChannelCommandHandler.LiveStepUpdateAsync reject the same way for callers
/// that skip the dialog (e.g. MCP tools). The hardware remains authoritative: these mirror, but
/// never replace, the wire-level REASON the Secondary/Primary can still return.
/// </summary>
public static class LiveStepUpdateValidator
{
    public static bool IsEligibleOperator(byte operatorCode) =>
        OperatorConstants.LiveStepUpdateAllowedOperators.Contains(operatorCode);

    /// <summary>
    /// ExpandedProgramSteps is only populated when the program contains PRODUCER sub-steps -
    /// ChannelCommandHandler.SetProgramAsync explicitly leaves it empty ("new()") for every other
    /// program, since there is nothing to expand. programStepModel already carries the correct
    /// hardware step numbers in that (much more common) case. Same fallback convention already
    /// used by DeviceChannel.razor / WorkflowCanvasPage.razor / BmsDashboard.razor - centralized
    /// here so the Q9 dialog and ChannelCommandHandler.LiveStepUpdateAsync can't drift apart on it.
    /// </summary>
    public static List<StepModel> ResolveFullProgramSteps(List<StepModel>? expandedProgramSteps, List<StepModel>? programStepModel) =>
        expandedProgramSteps?.Count > 0 ? expandedProgramSteps : (programStepModel ?? new List<StepModel>());

    /// <summary>
    /// Q9 is session-only - a successful amendment is never written back to Program /
    /// ExpandedProgramSteps, so residentStep alone would keep showing the pre-edit values if the
    /// dialog is reopened for the same still-executing step. Prefer lastLiveStepUpdate whenever it
    /// is for the exact step currently executing; a step number mismatch (the program advanced, or
    /// nothing was ever amended yet) falls back to the true resident step.
    /// </summary>
    public static StepModel ResolveEditSource(StepModel residentStep, StepModel? lastLiveStepUpdate, int currentHardwareStepNumber) =>
        lastLiveStepUpdate?.StepNumber == currentHardwareStepNumber ? lastLiveStepUpdate : residentStep;

    /// <summary>
    /// residentStep is the step as currently loaded/running (from ExpandedProgramSteps); editedStep
    /// is the user's edited copy about to be sent. Returns null when valid, otherwise the message to
    /// surface - each mirrors one Primary-only REASON code (0x02/0x03/0x04) from the protocol doc.
    /// </summary>
    public static string? Validate(StepModel? residentStep, StepModel editedStep, int currentHardwareStepNumber)
    {
        if (residentStep == null)
            return "Step number is not the currently executing step.";

        if (residentStep.StepNumber != currentHardwareStepNumber || editedStep.StepNumber != currentHardwareStepNumber)
            return "Step number is not the currently executing step.";

        if (editedStep.OperatorCode != residentStep.OperatorCode)
            return "Operator change not permitted - parameters only.";

        if (!IsEligibleOperator(editedStep.OperatorCode))
            return "Operator not eligible for live update (TABLE / BEG / CYC / GOTO and other non-regulating operators).";

        return null;
    }
}
