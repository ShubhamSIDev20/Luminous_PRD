using System.Text.Json;
using System.Text.Json.Serialization;
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// The one place that knows how a WorkflowGraph becomes text. Enums are written as names so a
/// future reordering of NodeKind cannot silently reinterpret every saved layout.
/// </summary>
public static class WorkflowGraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,                        // keep C# casing, stable on disk
        DefaultIgnoreCondition = JsonIgnoreCondition.Never, // null is meaningful, always write it
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string Serialize(WorkflowGraph graph) =>
        JsonSerializer.Serialize(graph, Options);

    public static bool TryDeserialize(string? json, out WorkflowGraph? graph, out string? error)
    {
        graph = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The saved layout is empty.";
            return false;
        }

        WorkflowGraph? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WorkflowGraph>(json, Options);
        }
        catch (JsonException ex)
        {
            error = $"The saved layout could not be read: {ex.Message}";
            return false;
        }

        if (parsed is null)
        {
            error = "The saved layout could not be read.";
            return false;
        }

        if (parsed.Version != WorkflowGraph.CurrentVersion)
        {
            error = $"This layout was saved by a different version of the canvas "
                  + $"(schema version {parsed.Version}, this build expects "
                  + $"{WorkflowGraph.CurrentVersion}). It cannot be opened.";
            return false;
        }

        graph = parsed;
        return true;
    }
}
