using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations;

/// <summary>
/// Every query filters on OwnerUserId as well as id — layouts are per-user in v1, and an id-only
/// lookup would quietly hand one user another user's layout.
/// Note: Repository&lt;T&gt;.AddAsync only stages the entity, so this class calls SaveChangesAsync
/// itself (see the contract pinned in RepositoryTests).
/// </summary>
public class WorkflowLayoutRepository : IWorkflowLayoutRepository
{
    private readonly WorkflowDbContext _context;

    public WorkflowLayoutRepository(WorkflowDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<WorkflowLayout>> ListForUserAsync(string userId) =>
        await _context.WorkflowLayouts
            .Where(l => l.OwnerUserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync();

    public async Task<WorkflowLayout?> GetForUserAsync(long id, string userId) =>
        await _context.WorkflowLayouts
            .FirstOrDefaultAsync(l => l.Id == id && l.OwnerUserId == userId && !l.IsDeleted);

    public async Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout)
    {
        layout.UpdatedAt = DateTime.Now;

        if (layout.Id == 0)
        {
            layout.CreatedAt = DateTime.Now;
            await _context.WorkflowLayouts.AddAsync(layout);
        }
        else
        {
            _context.WorkflowLayouts.Update(layout);
        }

        await _context.SaveChangesAsync();
        return layout;
    }

    public async Task<bool> SoftDeleteAsync(long id, string userId)
    {
        var layout = await GetForUserAsync(id, userId);
        if (layout is null) return false;

        layout.IsDeleted = true;
        layout.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }
}
