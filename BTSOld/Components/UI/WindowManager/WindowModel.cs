using Blazicons;
using System.Text.Json.Serialization;

namespace BatteryTestingSystem.Components.UI.WindowManager;

public class WindowModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }

    [JsonIgnore]
    public SvgIcon Icon { get; set; }

    public string IconName { get; set; } // For serialization
    public string PageUrl { get; set; } // For page routing
    public WindowState State { get; set; } = WindowState.Normal;
    public WindowPosition Position { get; set; } = new();
    public WindowSize Size { get; set; } = new() { Width = 800, Height = 600 };
    public bool IsDocked { get; set; }
    public DockPosition DockPosition { get; set; }
    public int ZIndex { get; set; }
}

public class WindowPosition
{
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
}

public class WindowSize
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double? MinWidth { get; set; } = 300;
    public double? MinHeight { get; set; } = 200;
}

public class WindowManagerState
{
    public List<WindowStateData> Windows { get; set; } = new();
    public TaskbarPosition TaskbarPosition { get; set; } = TaskbarPosition.Bottom;
    public TaskbarDisplayMode TaskbarDisplayMode { get; set; } = TaskbarDisplayMode.IconWithText;
    public int MaxZIndex { get; set; } = 1000;
}

public class WindowStateData
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string IconName { get; set; }
    public string PageUrl { get; set; }
    public WindowState State { get; set; }
    public WindowPosition Position { get; set; }
    public WindowSize Size { get; set; }
    public bool IsDocked { get; set; }
    public DockPosition DockPosition { get; set; }
    public int ZIndex { get; set; }
}

public enum WindowState { Normal, Minimized, Maximized }
public enum DockPosition { None, Left, Right, Top, Bottom }
public enum TaskbarPosition { Top, Bottom, Left, Right }
public enum TaskbarDisplayMode { IconOnly, IconWithText, TextOnly }