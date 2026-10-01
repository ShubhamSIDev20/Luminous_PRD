namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// State of charge, derived. No SoC percentage exists anywhere in this schema - RealTimeRecord
/// tracks capacity in Ah - so the canvas computed nothing and the battery fill rendered empty in
/// live mode until this existed (WorkflowCanvasPage passed a hardcoded 0.0, with a comment saying
/// so).
///
/// Net coulomb count against the battery's rated capacity:
///
///     soc = clamp((ChargeCapacity - DischargeCapacity) / NominalCapacity, 0, 1)
///
/// Chosen because it uses charge the hardware actually measured moving, and because it is
/// direction-correct: it rises while charging and falls while discharging, which is exactly what
/// the cell's march animation depicts.
///
/// KNOWN LIMITS, deliberately not papered over:
///  - It assumes the cell began the session near empty. If it started part-charged, the reading is
///    low by that offset. This measures charge moved THIS SESSION, not absolute SoC.
///  - It resets whenever the hardware clears its accumulators.
///
/// Returns null - not 0 - when there is no battery or its rated capacity is unusable. On a test
/// bench "unknown" and "flat" must not look the same, and BatteryDTO.NominalCapacity defaults to
/// 0, so an unconfigured battery record would otherwise divide by zero into a confident wrong
/// answer.
///
/// Pure and DI-free so it is testable without hardware, and isolated so the formula above can be
/// replaced without touching any rendering code - the limits make that likely.
/// </summary>
public static class StateOfChargeEstimator
{
    public static double? Estimate(double chargeCapacity, double dischargeCapacity, float? nominalCapacity)
    {
        if (nominalCapacity is not { } nominal || nominal <= 0) return null;

        return Math.Clamp((chargeCapacity - dischargeCapacity) / nominal, 0d, 1d);
    }
}
