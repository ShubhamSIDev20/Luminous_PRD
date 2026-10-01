namespace BatteryTestingSystem.Components.UI.DynamicTable;

// Supporting Classes
public class TableRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public string SortColumn { get; set; } = string.Empty;
    public bool SortDescending { get; set; }
}

public class TableResponse<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public int TotalRecords { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
