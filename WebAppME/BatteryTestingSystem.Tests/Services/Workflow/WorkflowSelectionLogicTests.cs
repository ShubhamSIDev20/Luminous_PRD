using System;
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Mirrors DashboardView.UpdateSelectedCircuits's rule in canvas terms: never Offline, and once
/// something is selected every further addition must match its (CircuitStatus, ProgramStatus) -
/// the same rule that keeps a bulk action from ever receiving a mixed set.
/// </summary>
public class WorkflowSelectionLogicTests
{
    private static ChannelTelemetry Reading(
        CircuitStatus status = CircuitStatus.Idle,
        ProgramRunningStatus programStatus = ProgramRunningStatus.Stop) =>
        new(status, 0, 0, 0, ProgramStatus: programStatus);

    [Fact]
    public void CanAdd_RejectsAnOfflineCandidate()
    {
        var readings = new Dictionary<string, ChannelTelemetry> { ["chn-1"] = Reading(CircuitStatus.Offline) };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-1", readings, new List<string>()));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithNoReadingYet()
    {
        Assert.False(WorkflowSelectionLogic.CanAdd(
            "chn-1", new Dictionary<string, ChannelTelemetry>(), new List<string>()));
    }

    [Fact]
    public void CanAdd_AllowsAnyEligibleCandidateWhenNothingIsSelectedYet()
    {
        var readings = new Dictionary<string, ChannelTelemetry> { ["chn-1"] = Reading(CircuitStatus.Charge) };

        Assert.True(WorkflowSelectionLogic.CanAdd("chn-1", readings, new List<string>()));
    }

    [Fact]
    public void CanAdd_AllowsACandidateMatchingTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
        };

        Assert.True(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithADifferentCircuitStatusThanTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Discharging),
        };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithADifferentProgramStatusThanTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Stop),
        };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void ApplyBulk_AddsEveryEligibleCandidate()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Charge),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.AllAdded, result.Outcome);
        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1", "chn-2" }, selected);
    }

    [Fact]
    public void ApplyBulk_SkipsOfflineMembersAndReportsThem()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Offline),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.PartiallyAdded, result.Outcome);
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_TheFirstEligibleMemberBecomesTheAnchorForTheRest()
    {
        // Charge/Running comes first in the candidate list, so it becomes the anchor; the
        // Idle/Stop candidate that follows must be rejected even though both are individually
        // eligible in isolation.
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_ReusesAnExistingAnchorRatherThanPickingANewOne()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop),
        };
        var selected = new HashSet<string> { "chn-1" };

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.NothingEligible, result.Outcome);
        Assert.Equal(0, result.Added);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_ReportsNothingEligibleForAnEmptyCandidateList()
    {
        var result = WorkflowSelectionLogic.ApplyBulk(
            Array.Empty<string>(), new Dictionary<string, ChannelTelemetry>(), new HashSet<string>());

        Assert.Equal(SelectionOutcome.NothingEligible, result.Outcome);
    }
}
