namespace BatteryTestingSystem.Components.UI;

public class StepModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int StepNumber { get; set; }
    public byte OperatorCode { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public List<string> NominalValues { get; set; } = new();
    public List<string> Limits { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> Registrations { get; set; } = new();
}

public class Operator
{
    public byte Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ValidationError
{
    public string StepId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class GlobalVariable
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string StepId { get; set; } = string.Empty;
}
