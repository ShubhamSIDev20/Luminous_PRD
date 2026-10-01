using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Exact mirror of DashboardView.CanContextAction, minus "calibration" - the canvas has no
/// calibration entry point (D9 lists only Start/Stop/Pause/Continue/Transfer).
/// </summary>
public class WorkflowActionRulesTests
{
    private static ChannelTelemetry Reading(
        CircuitStatus status, ProgramRunningStatus programStatus, bool isConnected = true) =>
        new(status, 0, 0, 0, ProgramStatus: programStatus, IsConnected: isConnected);

    [Fact]
    public void Offline_DisablesEveryAction()
    {
        var reading = Reading(CircuitStatus.Offline, ProgramRunningStatus.Stop);

        foreach (var action in new[] { "start", "stop", "pause", "continue", "transfer" })
            Assert.False(WorkflowActionRules.CanPerform(reading, action));
    }

    [Theory]
    [InlineData(ProgramRunningStatus.Stop, true)]
    [InlineData(ProgramRunningStatus.Running, false)]
    public void Start_RequiresProgramStopped(ProgramRunningStatus programStatus, bool expected)
    {
        var reading = Reading(CircuitStatus.Idle, programStatus);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "start"));
    }

    [Fact]
    public void Start_RequiresTcpConnected()
    {
        var reading = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop, isConnected: false);

        Assert.False(WorkflowActionRules.CanPerform(reading, "start"));
    }

    [Fact]
    public void Stop_RequiresOnlyTcpConnected_NotAnyParticularProgramState()
    {
        var running = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running);
        var stopped = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);

        Assert.True(WorkflowActionRules.CanPerform(running, "stop"));
        Assert.True(WorkflowActionRules.CanPerform(stopped, "stop"));
    }

    [Theory]
    [InlineData(ProgramRunningStatus.Running, true)]
    [InlineData(ProgramRunningStatus.Stop, false)]
    public void Pause_RequiresProgramRunning(ProgramRunningStatus programStatus, bool expected)
    {
        var reading = Reading(CircuitStatus.Charge, programStatus);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "pause"));
    }

    [Theory]
    [InlineData(CircuitStatus.Pause, true)]
    [InlineData(CircuitStatus.Interrupt, true)]
    [InlineData(CircuitStatus.Countinue, true)]
    [InlineData(CircuitStatus.Error, true)]
    [InlineData(CircuitStatus.Msg, true)]
    [InlineData(CircuitStatus.Charge, false)]
    public void Continue_RequiresProgramRunningAndOneOfTheResumableCircuitStatuses(
        CircuitStatus circuitStatus, bool expected)
    {
        var reading = Reading(circuitStatus, ProgramRunningStatus.Running);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "continue"));
    }

    [Fact]
    public void Transfer_RequiresIdleAndProgramStopped()
    {
        var eligible = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);
        var ineligible = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running);

        Assert.True(WorkflowActionRules.CanPerform(eligible, "transfer"));
        Assert.False(WorkflowActionRules.CanPerform(ineligible, "transfer"));
    }

    [Fact]
    public void UnknownAction_IsAlwaysDisabled()
    {
        var reading = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);

        Assert.False(WorkflowActionRules.CanPerform(reading, "calibration"));
    }
}
