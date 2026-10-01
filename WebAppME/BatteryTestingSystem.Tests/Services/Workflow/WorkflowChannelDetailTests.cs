using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Sub-project C2. The canvas dialog is a NEW component rather than a reuse of the dashboard
/// card's, because that one lives inside a 2,000-line DeviceChannel.razor this branch must not
/// modify (D13). The obvious risk of duplicating a surface is silently omitting fields, so the
/// field lists live in WorkflowChannelDetail where a test can count them against the DTOs instead
/// of a human diffing two large Razor files.
/// </summary>
public class WorkflowChannelDetailTests
{
    private static int PublicPropertyCount<T>() =>
        typeof(T).GetProperties().Length;

    [Fact]
    public void Manufacturing_CoversEveryFieldOnTheDto()
    {
        Assert.Equal(
            PublicPropertyCount<ManufacturingDetailDTO>(),
            WorkflowChannelDetail.Manufacturing(new ManufacturingDetailDTO()).Count);
    }

    [Fact]
    public void Factory_CoversEveryFieldOnTheDto()
    {
        Assert.Equal(
            PublicPropertyCount<FactoryConfigDetailDTO>(),
            WorkflowChannelDetail.Factory(new FactoryConfigDetailDTO()).Count);
    }

    [Fact]
    public void Manufacturing_RendersRealValues()
    {
        var rows = WorkflowChannelDetail.Manufacturing(new ManufacturingDetailDTO
        {
            MasterSWVersion = "1.2.3",
            PrimarySerialNumber = "SN-100",
            ManufactureDateTime = new DateTime(2026, 3, 4, 9, 30, 0),
        });

        Assert.Equal("1.2.3", rows.Single(r => r.Label == "Master SW Version").Value);
        Assert.Equal("SN-100", rows.Single(r => r.Label == "Primary Serial Number").Value);
        Assert.Equal("2026-03-04 09:30", rows.Single(r => r.Label == "Manufactured").Value);
    }

    [Fact]
    public void Manufacturing_ShowsAPlaceholderForAnUnreportedDate()
    {
        // default(DateTime) would otherwise render 0001-01-01, which reads as a real date.
        var rows = WorkflowChannelDetail.Manufacturing(new ManufacturingDetailDTO());

        Assert.Equal("--", rows.Single(r => r.Label == "Manufactured").Value);
        Assert.Equal("--", rows.Single(r => r.Label == "Last Synced").Value);
    }

    [Fact]
    public void Factory_FormatsPortsBooleansAndUnits()
    {
        var rows = WorkflowChannelDetail.Factory(new FactoryConfigDetailDTO
        {
            TcpClientRemotePort = 9999,
            DhcpEnabled = true,
            CircuitMaxChargeCurrent = 12.5f,
        });

        Assert.Equal("9999", rows.Single(r => r.Label == "TCP Client Port").Value);
        Assert.Equal("Enabled", rows.Single(r => r.Label == "DHCP").Value);
        Assert.Equal("12.5 A", rows.Single(r => r.Label == "Max Charge Current").Value);
    }

    [Fact]
    public void Factory_RendersDhcpDisabledRatherThanFalse()
    {
        var rows = WorkflowChannelDetail.Factory(new FactoryConfigDetailDTO { DhcpEnabled = false });

        Assert.Equal("Disabled", rows.Single(r => r.Label == "DHCP").Value);
    }

    [Fact]
    public void Battery_RendersNameProducerAndUnitedValues()
    {
        var rows = WorkflowChannelDetail.Battery(new BatteryDTO
        {
            Name = "Li-ion",
            Producer = "Acme",
            NominalVoltage = 3.7f,
            NominalCapacity = 100f,
            NumberOfCells = 12,
        });

        Assert.Equal("Li-ion", rows.Single(r => r.Label == "Name").Value);
        Assert.Equal("Acme", rows.Single(r => r.Label == "Producer").Value);
        Assert.Equal("3.7 V", rows.Single(r => r.Label == "Nominal Voltage").Value);
        Assert.Equal("100 Ah", rows.Single(r => r.Label == "Nominal Capacity").Value);
        Assert.Equal("12", rows.Single(r => r.Label == "Number Of Cells").Value);
    }

    [Fact]
    public void Battery_ShowsAPlaceholderForMissingText()
    {
        var rows = WorkflowChannelDetail.Battery(new BatteryDTO());

        Assert.Equal("--", rows.Single(r => r.Label == "Name").Value);
        Assert.Equal("--", rows.Single(r => r.Label == "Comments").Value);
    }

    [Fact]
    public void EveryBuilder_ReturnsEmptyForANullDto_RatherThanThrowing()
    {
        // The dialog can open before a fetch completes, or for a channel the device never
        // answered for. An exception there would kill the Blazor circuit.
        Assert.Empty(WorkflowChannelDetail.Manufacturing(null));
        Assert.Empty(WorkflowChannelDetail.Factory(null));
        Assert.Empty(WorkflowChannelDetail.Battery(null));
        Assert.Empty(WorkflowChannelDetail.Dbc(null));
    }

    // ============================================================ C3 — DBC signals

    [Fact]
    public void Dbc_ListsEverySignalOrderedByName()
    {
        // Stable ordering matters: an unordered dictionary reshuffles between ticks, and a list
        // that jumps several times a second cannot be read.
        var record = new DbcRecord
        {
            DbcValues = new Dictionary<string, object>
            {
                ["PackVoltage"] = 51.2,
                ["CellTemp"] = 28,
                ["ambientTemp"] = 21.5,
            },
        };

        var rows = WorkflowChannelDetail.Dbc(record);

        Assert.Equal(new[] { "ambientTemp", "CellTemp", "PackVoltage" }, rows.Select(r => r.Label));
    }

    [Fact]
    public void Dbc_FormatsNumericSignalsWithInvariantCulture()
    {
        // On a de-DE machine a raw ToString() yields "51,2" - read as 512 by everyone else.
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture =
            new System.Globalization.CultureInfo("de-DE");
        try
        {
            var record = new DbcRecord
            {
                DbcValues = new Dictionary<string, object> { ["PackVoltage"] = 51.25 },
            };

            Assert.Equal("51.25", WorkflowChannelDetail.Dbc(record).Single().Value);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Dbc_RendersBooleansAndNullsReadably()
    {
        var record = new DbcRecord
        {
            DbcValues = new Dictionary<string, object?>
            {
                ["ContactorClosed"] = true,
                ["Unreported"] = null,
            }!,
        };

        var rows = WorkflowChannelDetail.Dbc(record);

        Assert.Equal("true", rows.Single(r => r.Label == "ContactorClosed").Value);
        Assert.Equal("--", rows.Single(r => r.Label == "Unreported").Value);
    }

    [Fact]
    public void Dbc_ReturnsEmptyWhenTheChannelHasNoDecodedSignals()
    {
        Assert.Empty(WorkflowChannelDetail.Dbc(new DbcRecord()));
        Assert.Empty(WorkflowChannelDetail.Dbc(new DbcRecord
        {
            DbcValues = new Dictionary<string, object>(),
        }));
    }

    [Fact]
    public void NoRowEverHasABlankValue()
    {
        // A blank value renders as an empty row that looks like a rendering bug.
        var all = WorkflowChannelDetail.Manufacturing(new ManufacturingDetailDTO())
            .Concat(WorkflowChannelDetail.Factory(new FactoryConfigDetailDTO()))
            .Concat(WorkflowChannelDetail.Battery(new BatteryDTO()));

        Assert.All(all, r => Assert.False(string.IsNullOrWhiteSpace(r.Value), $"'{r.Label}' is blank"));
    }
}
