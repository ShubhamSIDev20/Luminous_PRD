using Microsoft.AspNetCore.Components;
namespace BatteryTestingSystem.Components.UI.Form;


public class FormFieldContext
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FormFieldContextData<TValue>
{
    public string FieldId { get; set; } = string.Empty;
    public TValue? Value { get; set; }
    public EventCallback<TValue> ValueChanged { get; set; }
    public bool IsInvalid { get; set; }
}