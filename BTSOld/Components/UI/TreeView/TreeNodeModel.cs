using Blazicons;
using Microsoft.AspNetCore.Components;

namespace BatteryTestingSystem.Components.UI.TreeView;

// Models/TreeNodeModel.cs
public class TreeNodeModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; }
    public SvgIcon Icon { get; set; }
    public object Data { get; set; } // Store any custom data
    public List<TreeNodeModel>? Children { get; set; }
}
