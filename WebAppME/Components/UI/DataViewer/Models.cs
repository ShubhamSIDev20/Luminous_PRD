// Models.cs
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Utils;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace BatteryTestingSystem.Components.UI.DataViewer;


// ─── ChartField + Helper ─────────────────────────────────────────
public record ChartField(string PropertyName, string Label, string Color, string Unit);

public static class ChartMetaHelper
{
    // Colors pulled from status vars — still theme-aware but we need
    // concrete hex/hsl strings here since Highcharts runs in JS.
    // These match your CSS variable palette semantically.

    private static readonly string[] Palette =
    [
        "hsl(199,89%,42%)",  // primary   → Voltage
        "hsl(142,76%,36%)",  // success   → Current
        "hsl(38,92%,50%)",   // warning   → Temperature
        "hsl(33,100%,50%)",  // discharging → Power
        "hsl(207,90%,54%)",  // pause     → AccumCapacity
        "hsl(122,39%,49%)",  // continue  → ChargeCapacity
        "hsl(37,75%,35%)",   // interrupt → DischargeCapacity
        "hsl(187,100%,42%)", // msg       → StepCapacity
        "hsl(199,89%,62%)",  // primary light → AccumEnergy
        "hsl(51,100%,50%)",  // charge    → ChargeEnergy
        "hsl(0,84%,60%)",    // destructive → DischargeEnergy
        "hsl(215,16%,47%)",  // muted-fg  → StepEnergy
    ];

    private static List<ChartField>? _cache;

    public static List<ChartField> GetChartFields()
    {
        if (_cache is not null) return _cache;

        var props = typeof(MeasurementData)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<UseChartAttribute>() != null)
            .ToList();

        _cache = props.Select((p, i) =>
        {
            var (label, unit) = GetLabelUnit(p.Name);
            return new ChartField(p.Name, label, Palette[i % Palette.Length], unit);
        }).ToList();

        return _cache;
    }

    public static float? GetValue(MeasurementData d, string propertyName) => propertyName switch
    {
        "ProgramRunningTime" => (float?)d.ProgramRunningTime,
        "StepNumber" => (float?)d.StepNumber,
        _ => (float?)typeof(MeasurementData).GetProperty(propertyName)?.GetValue(d)
    };


    private static (string Label, string Unit) GetLabelUnit(string name) => name switch
    {
        nameof(MeasurementData.Current)             => ("Current",          "A"),
        nameof(MeasurementData.Voltage)             => ("Voltage",          "V"),
        nameof(MeasurementData.Temperature)         => ("Temperature",      "°C"),
        nameof(MeasurementData.Power)               => ("Power",            "W"),
        nameof(MeasurementData.AccumulatedCapacity) => ("Accum. Cap.",      "Ah"),
        nameof(MeasurementData.ChargeCapacity)      => ("Charge Cap.",      "Ah"),
        nameof(MeasurementData.DischargeCapacity)   => ("Discharge Cap.",   "Ah"),
        nameof(MeasurementData.StepCapacity)        => ("Step Cap.",        "Ah"),
        nameof(MeasurementData.AccumulatedEnergy)   => ("Accum. Energy",    "Wh"),
        nameof(MeasurementData.ChargeEnergy)        => ("Charge Energy",    "Wh"),
        nameof(MeasurementData.DischargeEnergy)     => ("Discharge Energy", "Wh"),
        nameof(MeasurementData.StepEnergy)          => ("Step Energy",      "Wh"),
        _ => (name, ""),
    };
}
