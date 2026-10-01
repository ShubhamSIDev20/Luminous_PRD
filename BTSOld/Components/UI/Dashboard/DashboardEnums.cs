namespace BatteryTestingSystem.Components.UI.Dashboard;
public enum DisplayMode
{
    Mini,
    Compact,
    Normal,
    Chart,
    List
}

public class ChartDataPoint
{
    public DateTime TimeStamp { get; set; }
    public float Voltage { get; set; }
    public float Current { get; set; }
    public float Temperature { get; set; }
}

public class CardConfig
{
    public double CardSize { get; set; }
    public double FontSize { get; set; }
    public int RefreshRate { get; set; }
    public int ShowDecimal { get; set; }
    public DisplayMode DisplayMode { get; set; }
    public List<string> VisibleProperties { get; set; } = new();

}