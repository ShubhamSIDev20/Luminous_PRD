using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI.Dashboard;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The canvas may only offer properties it can actually supply live data for — an offered key that
/// renders "--" forever is worse than not offering it.
///
/// Phase 2 satisfied that by offering only 17 of the dashboard's 28 keys: ChannelTelemetry covered
/// every RealTimeProperties key but just 5 of the 13 ProgramProperties keys and none of the 3
/// ConfigProperties keys. Sub-project C1 inverted the fix — instead of narrowing the catalog to
/// what the DTO carried, it widened the DTO to carry everything (config/operator values resolved
/// from the channel handler in PushTelemetryAsync, the rest straight off RealTimeRecord/Session).
/// So the catalog is now the full 28, and the "can we actually supply it?" guarantee is enforced
/// by BuildNodeProperties_ProducesAValueForEveryOfferedKey below rather than by exclusion.
/// </summary>
public class WorkflowNodePropertiesTests
{
    private static IEnumerable<string> DashboardKeys() =>
        CardPreviewData.RealTimeProperties.Keys
            .Concat(CardPreviewData.ConfigProperties.Keys)
            .Concat(CardPreviewData.ProgramProperties.Keys);

    [Fact]
    public void AvailableKeys_CoversEveryDashboardCardField()
    {
        // Replaces Phase 2's AvailableKeys_ContainsExactlySeventeenKeys and its inverse
        // AvailableKeys_ExcludesEveryUnsupportedDashboardProperty: C1's whole point is that
        // nothing from the dashboard card is missing any more.
        Assert.Equal(DashboardKeys().OrderBy(k => k), WorkflowNodeProperties.AvailableKeys.OrderBy(k => k));
    }

    [Fact]
    public void AvailableKeys_ContainsEveryRealTimeProperty()
    {
        foreach (var key in CardPreviewData.RealTimeProperties.Keys)
            Assert.Contains(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Fact]
    public void AvailableKeys_ContainsEveryConfigProperty()
    {
        foreach (var key in CardPreviewData.ConfigProperties.Keys)
            Assert.Contains(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Theory]
    [InlineData("CycleNumber")]
    [InlineData("TableStepNumber")]
    [InlineData("TableTotalRowNumber")]
    [InlineData("StepRunningTime")]
    [InlineData("RunningTime")]
    [InlineData("CycleStatus")]
    [InlineData("CycleRunIteration")]
    [InlineData("StepNumber")]
    [InlineData("OperatorCode")]
    [InlineData("Storerecordcount")]
    [InlineData("Unstorerecordcount")]
    [InlineData("Error")]
    [InlineData("SystemError")]
    public void AvailableKeys_ContainsEveryProgramProperty(string key)
    {
        // Was "TheFiveSupportedProgramProperties" — all 13 are supported as of C1.
        Assert.Contains(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Fact]
    public void AvailableKeys_HasNoDuplicates()
    {
        Assert.Equal(WorkflowNodeProperties.AvailableKeys.Count,
            WorkflowNodeProperties.AvailableKeys.Distinct().Count());
    }

    [Fact]
    public void EveryAvailableKey_ResolvesRealMetadataFromTheSharedCatalog()
    {
        // Sharing CardPreviewData is the point of D11: labels can never drift between the
        // dashboard and the canvas because there is only one place they are defined.
        foreach (var key in WorkflowNodeProperties.AvailableKeys)
            Assert.NotNull(CardPreviewData.FindPropertyMetadata(key));
    }

    [Fact]
    public void Label_ComesFromTheDashboardCatalogForEveryKey()
    {
        foreach (var key in WorkflowNodeProperties.AvailableKeys)
        {
            Assert.Equal(
                CardPreviewData.FindPropertyMetadata(key)?.Label,
                WorkflowNodeProperties.Label(key));
        }
    }

    [Fact]
    public void DefaultVisibleProperties_ReproducesWhatAlreadyShipped()
    {
        // A user who never opens the picker must see no change from Phase 1 (90d9b8d / 7335b81).
        Assert.Equal(new[] { "Voltage", "Current", "Power", "Temperature" },
            WorkflowNodeProperties.DefaultVisibleProperties);
    }

    [Fact]
    public void DefaultVisibleProperties_IsWithinTheMaxVisibleCap()
    {
        Assert.True(WorkflowNodeProperties.DefaultVisibleProperties.Count <= WorkflowNodeProperties.MaxVisible);
    }

    [Fact]
    public void MaxVisible_OffersEveryKeyInTheCatalogue()
    {
        // The arbitrary limit is gone: 6, then 12, now all of them. Safe only because the card
        // height, RowHeight and the edge attachment point all derive from this number, so the
        // layout grows with the selection instead of overlapping. Tied to the catalogue rather
        // than restated, so a new CardPreviewData field can never be permanently unselectable.
        Assert.Equal(WorkflowNodeProperties.AvailableKeys.Count, WorkflowNodeProperties.MaxVisible);
        Assert.Equal(28, WorkflowNodeProperties.MaxVisible);
    }

    [Fact]
    public void Label_ReturnsTheSharedCatalogsLabel()
    {
        Assert.Equal("Voltage (V)", WorkflowNodeProperties.Label("Voltage"));
    }

    [Fact]
    public void Label_FallsBackToTheKeyItselfForAnUnknownKey()
    {
        // Defensive only - every key actually in AvailableKeys resolves (proven above); this
        // covers a caller passing something outside that set without throwing.
        Assert.Equal("NotAKey", WorkflowNodeProperties.Label("NotAKey"));
    }

    // ============================================================ C1 supply guarantee

    [Fact]
    public void BuildNodeProperties_ProducesAValueForEveryOfferedKey()
    {
        // THE test that replaces Phase 2's exclusion list. Every key the picker offers must
        // resolve to something - no key may be selectable yet unrenderable.
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0.5, 3.7, 12.5,
            Power: 45.6, Temperature: 28.4,
            AccumulatedCapacity: 1.1, ChargeCapacity: 2.2, DischargeCapacity: 3.3, StepCapacity: 4.4,
            AccumulatedEnergy: 5.5, ChargeEnergy: 6.6, DischargeEnergy: 7.7, StepEnergy: 8.8,
            CycleNumber: 3, TableStepNumber: 4, TableTotalRowNumber: 12,
            RunningTime: TimeSpan.FromSeconds(3725), StepRunningTime: TimeSpan.FromSeconds(65),
            StepNumber: 7, CycleStatus: 1, CycleRunIteration: 2,
            StoredRecordCount: 900, UnstoredRecordCount: 12,
            OperatorName: "CC-CV", BatteryName: "Li-ion", ProgramName: "Test", SessionId: "18211903");

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        foreach (var key in WorkflowNodeProperties.AvailableKeys)
        {
            Assert.True(properties.ContainsKey(key), $"'{key}' is offered but never populated.");
            Assert.False(
                string.IsNullOrWhiteSpace(properties[key]),
                $"'{key}' resolved to blank, which renders as an empty row.");
        }
    }

    [Fact]
    public void BuildNodeProperties_FormatsTheNewlyAddedFields()
    {
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0, 0, 0,
            StepNumber: 7, CycleStatus: 1, CycleRunIteration: 2,
            StoredRecordCount: 900, UnstoredRecordCount: 12,
            OperatorName: "CC-CV", BatteryName: "Li-ion", ProgramName: "Test", SessionId: "18211903");

        var p = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("7", p["StepNumber"]);
        Assert.Equal("1", p["CycleStatus"]);
        Assert.Equal("2", p["CycleRunIteration"]);
        Assert.Equal("900", p["Storerecordcount"]);
        Assert.Equal("12", p["Unstorerecordcount"]);
        Assert.Equal("CC-CV", p["OperatorCode"]);
        Assert.Equal("Li-ion", p["BatteryID"]);
        Assert.Equal("Test", p["ProgramID"]);
        Assert.Equal("18211903", p["SessionID"]);
    }

    [Fact]
    public void BuildNodeProperties_ShowsAPlaceholderRatherThanBlankForUnsetTextFields()
    {
        // A channel with no program/battery/session assigned yet must render "--", not an empty
        // row that looks like a rendering bug.
        var reading = new ChannelTelemetry(CircuitStatus.Idle, 0, 0, 0);

        var p = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("--", p["BatteryID"]);
        Assert.Equal("--", p["ProgramID"]);
        Assert.Equal("--", p["SessionID"]);
        Assert.Equal("--", p["OperatorCode"]);
    }

    // ============================================================ SectionsFor
    //
    // Section membership and row shape are read from CardPreviewData's three catalogs rather than
    // restated, so a key added to the dashboard cannot land in the wrong section here or in none.
    // TwoPerRow is a LAYOUT fact: WorkflowAutoLayout.ChannelNodeHeight multiplies by it, so the
    // component and the height formula must agree, which they do by both reading this.

    [Fact]
    public void SectionsFollowTheCatalogueOrderAndCarryTheirRowShape()
    {
        var sections = WorkflowNodeProperties.SectionsFor(
            new[] { "CycleNumber", "Voltage", "BatteryID", "Current" });

        Assert.Collection(sections,
            s =>
            {
                Assert.Equal("Primary Data", s.Title);
                Assert.True(s.TwoPerRow);
                Assert.Equal(new[] { "Current", "Voltage" }, s.Keys);   // catalogue order, not input order
            },
            s =>
            {
                Assert.Equal("Configuration", s.Title);
                Assert.False(s.TwoPerRow);
                Assert.Equal(new[] { "BatteryID" }, s.Keys);
            },
            s =>
            {
                Assert.Equal("Program Data", s.Title);
                Assert.False(s.TwoPerRow);
                Assert.Equal(new[] { "CycleNumber" }, s.Keys);
            });
    }

    [Fact]
    public void EmptySectionsAreOmittedEntirely()
    {
        var sections = WorkflowNodeProperties.SectionsFor(new[] { "Voltage" });

        Assert.Single(sections);
        Assert.Equal("Primary Data", sections[0].Title);
    }

    [Fact]
    public void AnEmptySelectionProducesNoSections()
    {
        Assert.Empty(WorkflowNodeProperties.SectionsFor(Array.Empty<string>()));
    }

    [Fact]
    public void UnknownKeysAreDroppedRatherThanCrashing()
    {
        // A WorkflowNodeConfig saved before a catalogue change can still hold a dead key.
        var sections = WorkflowNodeProperties.SectionsFor(new[] { "Voltage", "NoSuchKey" });

        Assert.Single(sections);
        Assert.Equal(new[] { "Voltage" }, sections[0].Keys);
    }

    [Fact]
    public void EveryOfferedKeyLandsInExactlyOneSection()
    {
        var sections = WorkflowNodeProperties.SectionsFor(WorkflowNodeProperties.AvailableKeys);

        Assert.Equal(3, sections.Count);
        Assert.Equal(
            WorkflowNodeProperties.AvailableKeys.Count,
            sections.Sum(s => s.Keys.Count));
        Assert.Equal(
            WorkflowNodeProperties.AvailableKeys.Count,
            sections.SelectMany(s => s.Keys).Distinct().Count());
    }
}
