namespace BatteryTestingSystem.Components.UI.Circuits;

public class CircuitCardModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public CircuitCardModel(double x, double y)
    {
        X = x;
        Y = y;
    }
   
}
