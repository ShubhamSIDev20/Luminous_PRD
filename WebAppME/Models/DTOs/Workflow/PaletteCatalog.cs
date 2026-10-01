namespace BatteryTestingSystem.Models.DTOs.Workflow;

public record PaletteProgram(int Id, string Name);

public record PaletteDbc(int Id, string Name);

public record PaletteBatteryType(int Id, string Name);

/// <summary>Everything the right-hand rail can offer, other than hardware topology.</summary>
public record PaletteCatalog(
    IReadOnlyList<PaletteProgram> Programs,
    IReadOnlyList<PaletteDbc> DbcFiles,
    IReadOnlyList<PaletteBatteryType> BatteryTypes)
{
    public static PaletteCatalog Empty => new(
        Array.Empty<PaletteProgram>(),
        Array.Empty<PaletteDbc>(),
        Array.Empty<PaletteBatteryType>());
}
