using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// An in-memory stand-in so the service's orchestration can be tested without EF. The repository
/// itself is covered separately against real SQLite in WorkflowLayoutRepositoryTests.
/// </summary>
public class FakeWorkflowLayoutRepository : IWorkflowLayoutRepository
{
    private readonly List<WorkflowLayout> _rows = new();
    private long _nextId = 1;

    public IReadOnlyList<WorkflowLayout> Rows => _rows;

    public Task<List<WorkflowLayout>> ListForUserAsync(string userId) =>
        Task.FromResult(_rows
            .Where(l => l.OwnerUserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt)
            .ToList());

    public Task<WorkflowLayout?> GetForUserAsync(long id, string userId) =>
        Task.FromResult(_rows.FirstOrDefault(
            l => l.Id == id && l.OwnerUserId == userId && !l.IsDeleted));

    public Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout)
    {
        layout.UpdatedAt = DateTime.Now;

        if (layout.Id == 0)
        {
            layout.Id = _nextId++;
            _rows.Add(layout);
        }
        else
        {
            _rows.RemoveAll(l => l.Id == layout.Id);
            _rows.Add(layout);
        }

        return Task.FromResult(layout);
    }

    public async Task<bool> SoftDeleteAsync(long id, string userId)
    {
        var row = await GetForUserAsync(id, userId);
        if (row is null) return false;
        row.IsDeleted = true;
        return true;
    }
}
