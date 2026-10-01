namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Defaults deliberately reproduce main's behaviour: canvas hidden, legacy dashboard visible.
/// A deployment that never heard of this feature must behave exactly as it did before.
/// </summary>
public class WorkflowFeatureOptions
{
    public const string SectionName = "Features";

    public bool WorkflowCanvas { get; set; } = false;

    public bool LegacyDashboard { get; set; } = true;
}
