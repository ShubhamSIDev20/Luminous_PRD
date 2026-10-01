// Models/ViewModels/PageChangeRequest.cs
namespace BatteryTestingSystem.Models.ViewModels;

/// <summary>
/// Raised by BmsDataTable when the user navigates to a different page or changes page size.
/// BmsDashboard handles this by calling ISqliteBulkDatabaseManager.ReadSessionPageAsync.
/// </summary>
public record PageChangeRequest(int Page, int PageSize);
