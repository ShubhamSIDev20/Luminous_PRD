using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI.Dashboard;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// The subset of the dashboard's 28 selectable properties (CardPreviewData) that the canvas can
/// actually supply live data for, via ChannelTelemetry.
///
/// This is deliberately NOT the full 28. ChannelTelemetry covers every RealTimeProperties field
/// (12) plus 5 of CardPreviewData's 13 ProgramProperties fields - CycleNumber, TableStepNumber,
/// TableTotalRowNumber, StepRunningTime, RunningTime - and none of its 3 ConfigProperties fields
/// (BatteryID/ProgramID/SessionID come from the channel's DB config record, never loaded into
/// ChannelTelemetry). Error/SystemError are also excluded: the bridge already resolves both into
/// one merged ErrorText field for the dock, with no clean 1:1 split back into the catalog's two
/// separate keys. Offering any of these in the picker would show "--" forever.
///
/// Labels are read from CardPreviewData rather than duplicated (D11): the dashboard and the
/// canvas can never show different wording for the same property.
/// </summary>
public static class WorkflowNodeProperties
{
    /// <summary>
    /// How many measurements one channel card can show at once: all of them.
    ///
    /// There is no longer an arbitrary limit. It was 6 (then briefly 12) because RowHeight was a
    /// hardcoded 120 and taller cards overlapped the row beneath; now the card height, the row
    /// spacing and the edge attachment point are all derived from this number, so the layout grows
    /// to fit whatever is selected. Tied to the catalogue rather than restated as a literal, so
    /// adding a property to CardPreviewData cannot leave a key permanently unselectable.
    ///
    /// Showing everything makes a tall card (94px + 20px per PAIR of properties), which is the
    /// user's call to make — the geometry stays correct either way.
    /// </summary>
    public static int MaxVisible => AvailableKeys.Count;

    /// <summary>
    /// Every field the dashboard card offers — all three of its catalogs, in the dashboard's own
    /// order (realtime, then configuration, then program data).
    ///
    /// This deliberately no longer filters the list. It used to expose only 17 of the 28 keys,
    /// dropping the 3 configuration fields and 8 of the program fields on the grounds that
    /// ChannelTelemetry could not supply them — sub-project C1 made it supply all of them instead
    /// (the config/operator values are resolved from the channel handler in PushTelemetryAsync,
    /// the rest come off RealTimeRecord/Session directly). WorkflowNodePropertiesTests pins both
    /// halves of the promise: the catalogs match, AND every offered key resolves to a real value.
    /// </summary>
    public static readonly IReadOnlyList<string> AvailableKeys = new List<string>(
        CardPreviewData.RealTimeProperties.Keys
            .Concat(CardPreviewData.ConfigProperties.Keys)
            .Concat(CardPreviewData.ProgramProperties.Keys));

    public static readonly IReadOnlyList<string> DefaultVisibleProperties =
        new List<string> { "Voltage", "Current", "Power", "Temperature" };

    public static string Label(string key) =>
        CardPreviewData.FindPropertyMetadata(key)?.Label ?? key;

    /// <summary>
    /// The hardware's own unit token for a measurement, from OperatorConstants.ValidUnits and the
    /// per-measurement mapping documented on MeasurementData (0x26 Ah, 0x27 AhCha, 0x28 AhDch,
    /// 0x29 AhStep, 0x2A Wh, 0x2B WhCha, 0x2C WhDch, 0x2D WhStep).
    ///
    /// These four capacities and four energies are NOT all "Ah" and "Wh": the dashboard's shared
    /// catalogue labels them that way ("Charge Capacity (Ah)"), which loses the distinction, and
    /// the canvas repeated that. Charge, discharge and step each have their own token, and showing
    /// eight rows as four Ah and four Wh made them unreadable on a node face.
    ///
    /// Empty string for counts, ids and names, which carry no unit.
    /// </summary>
    public static string UnitFor(string key) => key switch
    {
        "Current" => "A",
        "Voltage" => "V",
        "Power" => "W",
        "Temperature" => "°C",

        "AccumulatedCapacity" => "Ah",
        "ChargeCapacity" => "AhCha",
        "DischargeCapacity" => "AhDch",
        "StepCapacity" => "AhStep",

        "AccumulatedEnergy" => "Wh",
        "ChargeEnergy" => "WhCha",
        "DischargeEnergy" => "WhDch",
        "StepEnergy" => "WhStep",

        _ => string.Empty,
    };

    /// <summary>
    /// The label with any parenthesised unit stripped, for surfaces that render the unit on the
    /// value instead. Without this the node face reads "Charge Capacity (Ah)  2.347 AhCha" — the
    /// catalogue's imprecise unit next to the hardware's correct one.
    /// </summary>
    public static string LabelWithoutUnit(string key)
    {
        var label = Label(key);
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 && label.EndsWith(')') ? label[..open] : label;
    }

    /// <summary>
    /// One rendered group on a channel battery cell.
    ///
    /// TwoPerRow is a LAYOUT fact, not a presentation preference:
    /// WorkflowAutoLayout.ChannelNodeHeight multiplies by it, so the component and the height
    /// formula must agree or edges attach off-centre and rows overlap. Both read it from here
    /// rather than each deciding for itself.
    /// </summary>
    public sealed record PropertySection(string Title, IReadOnlyList<string> Keys, bool TwoPerRow);

    /// <summary>
    /// The selected keys grouped into the dashboard card's three sections, in the catalogue's own
    /// order, with empty sections omitted entirely (no heading for a section with no keys).
    ///
    /// Membership is read from CardPreviewData's three dictionaries rather than restated, for the
    /// same reason AvailableKeys concatenates them: a key added to the dashboard's catalogue must
    /// not be able to land in the wrong section here, or in none at all.
    ///
    /// Primary Data packs two per row because its twelve realtime measurements are
    /// self-identifying through their unit token (Ah vs AhCha vs AhDch vs AhStep), and labelling
    /// them one-per-row would roughly double the cell's height. Configuration and Program Data
    /// values carry no unit and are meaningless without a name, so they get labelled rows.
    ///
    /// Keys present in no catalogue are dropped: a WorkflowNodeConfig saved before a catalogue
    /// change can still hold one, and the node face must render rather than throw.
    /// </summary>
    public static IReadOnlyList<PropertySection> SectionsFor(IReadOnlyList<string> selected)
    {
        var chosen = new HashSet<string>(selected);
        var sections = new List<PropertySection>(3);

        void Add(string title, IEnumerable<string> catalogueKeys, bool twoPerRow)
        {
            var keys = catalogueKeys.Where(chosen.Contains).ToList();
            if (keys.Count > 0) sections.Add(new PropertySection(title, keys, twoPerRow));
        }

        Add("Primary Data", CardPreviewData.RealTimeProperties.Keys, twoPerRow: true);
        Add("Configuration", CardPreviewData.ConfigProperties.Keys, twoPerRow: false);
        Add("Program Data", CardPreviewData.ProgramProperties.Keys, twoPerRow: false);

        return sections;
    }
}
