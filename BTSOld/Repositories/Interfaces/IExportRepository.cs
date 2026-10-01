using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Interfaces;

public interface IExportRepository
{
    Task<ExportRecord> CreateAsync(ExportRecord record);
    Task<ExportRecord?> GetByIdAsync(int id);
    Task<List<ExportRecord>> GetByUserAsync(string userName, int limit = 50);
    Task<List<ExportRecord>> GetBySessionAsync(string sessionFilePath);
    Task UpdateStatusAsync(int id, ExportStatus status, string? errorMessage = null);
    Task MarkReadyAsync(int id, string exportFilePath, long fileSizeBytes);
    Task DeleteAsync(int id);

    /// <summary>
    /// Marks an existing export record as Superseded so the user can request a
    /// fresh re-generation without polluting the history with stale Ready records.
    /// </summary>
    Task MarkSupersededAsync(int id);
}
