using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations;

public class ExportRepository : IExportRepository
{
    private readonly AppDbContext _db;

    public ExportRepository(AppDbContext db) => _db = db;

    public async Task<ExportRecord> CreateAsync(ExportRecord record)
    {
        _db.ExportRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public Task<ExportRecord?> GetByIdAsync(int id) =>
        _db.ExportRecords.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public Task<List<ExportRecord>> GetByUserAsync(string userName, int limit = 50) =>
        _db.ExportRecords
           .AsNoTracking()
           .Where(e => e.RequestedBy == userName && !e.IsDeleted)
           .OrderByDescending(e => e.RequestedAt)
           .Take(limit)
           .ToListAsync();

    public Task<List<ExportRecord>> GetBySessionAsync(string sessionFilePath) =>
        _db.ExportRecords
           .AsNoTracking()
           .Where(e => e.SessionFilePath == sessionFilePath && !e.IsDeleted)
           .OrderByDescending(e => e.RequestedAt)
           .ToListAsync();

    public async Task UpdateStatusAsync(int id, ExportStatus status, string? errorMessage = null)
    {
        var rec = await _db.ExportRecords.FindAsync(id);
        if (rec == null) return;
        rec.Status = status;
        rec.ErrorMessage = errorMessage;
        rec.UpdatedAt = DateTime.Now;
        if (status is ExportStatus.Failed or ExportStatus.Ready)
            rec.CompletedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task MarkReadyAsync(int id, string exportFilePath, long fileSizeBytes)
    {
        var rec = await _db.ExportRecords.FindAsync(id);
        if (rec == null) return;
        rec.Status = ExportStatus.Ready;
        rec.ExportFilePath = exportFilePath;
        rec.FileSizeBytes = fileSizeBytes;
        rec.CompletedAt = DateTime.Now;
        rec.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var rec = await _db.ExportRecords.FindAsync(id);
        if (rec == null) return;
        rec.IsDeleted = true;
        rec.Status = ExportStatus.Deleted;
        rec.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task MarkSupersededAsync(int id)
    {
        var rec = await _db.ExportRecords.FindAsync(id);
        if (rec == null) return;
        rec.Status = ExportStatus.Superseded;
        rec.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }
}
