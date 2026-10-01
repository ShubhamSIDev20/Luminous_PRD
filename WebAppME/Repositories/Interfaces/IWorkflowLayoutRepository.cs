using BatteryTestingSystem.Models.Entities;

namespace BatteryTestingSystem.Repositories.Interfaces;

public interface IWorkflowLayoutRepository
{
    Task<List<WorkflowLayout>> ListForUserAsync(string userId);

    Task<WorkflowLayout?> GetForUserAsync(long id, string userId);

    Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout);

    Task<bool> SoftDeleteAsync(long id, string userId);
}
