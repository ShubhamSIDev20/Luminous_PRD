// Models/ViewModels/PagedResult.cs
namespace BatteryTestingSystem.Models.ViewModels;

/// <summary>
/// Generic paged result returned by all server-side pagination queries.
/// Keeps only the current page in memory — no full list ever allocated.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalRows { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalRows / (double)PageSize) : 0;
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize = 50) =>
        new() { Page = 1, PageSize = pageSize };

    public PagedResult<T> WithItems(List<T> items, int total) =>
        new() { Items = items, TotalRows = total, Page = Page, PageSize = PageSize };
}
