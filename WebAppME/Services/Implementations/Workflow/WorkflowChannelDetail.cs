using System.Globalization;
using BatteryTestingSystem.Models.DTOs;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>One label/value row in the channel detail dialog.</summary>
public readonly record struct DetailRow(string Label, string Value);

/// <summary>
/// Flattens the DTOs behind the channel detail dialog into label/value rows.
///
/// The canvas has its own dialog rather than reusing the dashboard card's (which lives inside a
/// 2,000-line DeviceChannel.razor that this branch must not modify — D13). Keeping the field lists
/// here rather than in markup means "does the canvas show every field the dashboard shows?" is a
/// unit test instead of a manual read of two big Razor files.
/// </summary>
public static class WorkflowChannelDetail
{
    private const string Placeholder = "--";

    public static IReadOnlyList<DetailRow> Manufacturing(ManufacturingDetailDTO? d) =>
        d is null ? Array.Empty<DetailRow>() : new[]
        {
            new DetailRow("Master SW Version", Text(d.MasterSWVersion)),
            new DetailRow("Comm SW Version", Text(d.ComSWVersion)),
            new DetailRow("Secondary SW Version", Text(d.SecondarySWVersion)),
            new DetailRow("Primary Serial Number", Text(d.PrimarySerialNumber)),
            new DetailRow("Secondary Serial Number", Text(d.SecondarySerialNumber)),
            new DetailRow("Manufactured", Date(d.ManufactureDateTime)),
            new DetailRow("Commissioned", Date(d.CommissioningDateTime)),
            new DetailRow("Primary PCB Assembled", Date(d.PrimaryPCBAssemblyDateTime)),
            new DetailRow("Secondary PCB Assembled", Date(d.SecondaryPCBAssemblyDateTime)),
            new DetailRow("Last Synced", d.LastSyncedAt is { } s ? Date(s) : Placeholder),
        };

    public static IReadOnlyList<DetailRow> Factory(FactoryConfigDetailDTO? d) =>
        d is null ? Array.Empty<DetailRow>() : new[]
        {
            new DetailRow("MAC ID", Text(d.MacID)),
            new DetailRow("Device IP", Text(d.DeviceIPAddress)),
            new DetailRow("Client Remote IP", Text(d.ClientRemoteIPAddress)),
            new DetailRow("TCP Client Port", Num(d.TcpClientRemotePort)),
            new DetailRow("UDP Client Port", Num(d.UdpClientRemotePort)),
            new DetailRow("UDP Store Port", Num(d.UdpStoreRemotePort)),
            new DetailRow("DHCP", d.DhcpEnabled ? "Enabled" : "Disabled"),
            new DetailRow("Circuit Type", Text(d.CircuitType)),
            new DetailRow("Circuit Number", Num(d.CircuitNumber)),
            new DetailRow("ZNT Max Voltage", Unit(d.ZntMaxVoltage, "V")),
            new DetailRow("LNT Max Voltage", Unit(d.LntMaxVoltage, "V")),
            new DetailRow("Circuit Max Voltage", Unit(d.CircuitMaxVoltage, "V")),
            new DetailRow("Circuit Min Voltage", Unit(d.CircuitMinVoltage, "V")),
            new DetailRow("Max Charge Current", Unit(d.CircuitMaxChargeCurrent, "A")),
            new DetailRow("Max Discharge Current", Unit(d.CircuitMaxDischargeCurrent, "A")),
            new DetailRow("Last Synced", d.LastSyncedAt is { } s ? Date(s) : Placeholder),
        };

    public static IReadOnlyList<DetailRow> Battery(BatteryDTO? d) =>
        d is null ? Array.Empty<DetailRow>() : new[]
        {
            new DetailRow("Name", Text(d.Name)),
            new DetailRow("Producer", Text(d.Producer)),
            new DetailRow("Quantity", Num(d.Quantity)),
            new DetailRow("Number Of Cells", Num(d.NumberOfCells)),
            new DetailRow("Nominal Voltage", Unit(d.NominalVoltage, "V")),
            new DetailRow("Nominal Current", Unit(d.NominalCurrent, "A")),
            new DetailRow("Nominal Capacity", Unit(d.NominalCapacity, "Ah")),
            new DetailRow("Maximum Voltage", Unit(d.MaximumVoltage, "V")),
            new DetailRow("Gassing Voltage", Unit(d.GassingVoltage, "V")),
            new DetailRow("Break Voltage", Unit(d.BreakVoltage, "V")),
            new DetailRow("Charge Factor", Unit(d.ChargeFactor, "")),
            new DetailRow("Impedance", Unit(d.Impedance, "mΩ")),
            new DetailRow("Energy Density", Unit(d.EnergyDensity, "Wh/kg")),
            new DetailRow("Cold Cranking Current", Unit(d.ColdCrankingCurrent, "A")),
            new DetailRow("Comments", Text(d.Comments)),
        };

    /// <summary>
    /// The live decoded DBC signals for a channel (sub-project C3). Values arrive as boxed
    /// objects straight from the decoder, so each is formatted here rather than relying on a
    /// culture-sensitive ToString() in markup — a comma decimal separator in a signal value is
    /// how you get "1,5" where a European operator expects "1.5" and everyone else reads 15.
    ///
    /// Signals are ordered by name so the list does not reshuffle between ticks; a jumping list
    /// is unreadable when it updates several times a second.
    /// </summary>
    public static IReadOnlyList<DetailRow> Dbc(DbcRecord? record)
    {
        if (record?.DbcValues is not { Count: > 0 } values) return Array.Empty<DetailRow>();

        return values
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new DetailRow(kv.Key, Signal(kv.Value)))
            .ToList();
    }

    private static string Signal(object? value) => value switch
    {
        null => Placeholder,
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.###", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => Text(value.ToString()),
    };

    private static string Text(string? v) => string.IsNullOrWhiteSpace(v) ? Placeholder : v;

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Unit(float v, string unit) =>
        unit.Length == 0
            ? v.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{v:0.###} {unit}");

    /// <summary>default(DateTime) means the device never reported one — rendering 01/01/0001 would
    /// read as a real (absurd) date, so it shows the same placeholder as any other missing value.
    /// </summary>
    private static string Date(DateTime v) =>
        v == default ? Placeholder : v.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
