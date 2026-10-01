using Blazicons;

namespace BatteryTestingSystem.Components.Layout;


/// <summary>
/// Navigation menu item model
/// </summary>
public class NavMenuItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public SvgIcon? Icon { get; set; }
    public Type? ComponentType { get; set; }
    public Dictionary<string, dynamic>? Parameters { get; set; }
    public bool keepAlive { get; set; } = false;
    public bool Unique { get; set; } = true;

    public List<NavMenuItem>? Dropdown { get; set; }
}

/// <summary>
/// Error item model for footer
/// </summary>
public class ErrorItem
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning"; // "warning", "error"
    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// Event log item model for footer
/// </summary>
public class EventLogItem
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "info"; // "info", "warning", "error"
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Settings menu item
/// </summary>
public class SettingsMenuItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<SettingsMenuItem>? SubItems { get; set; }
}

