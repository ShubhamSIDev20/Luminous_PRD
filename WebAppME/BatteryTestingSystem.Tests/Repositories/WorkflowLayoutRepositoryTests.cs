using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BatteryTestingSystem.Tests.Repositories;

/// <summary>
/// Same approach as the existing RepositoryTests: a real SQLite in-memory provider rather than a
/// fake DbSet, because the bugs worth catching here are ownership filtering and whether
/// SaveChangesAsync actually ran.
/// </summary>
public class WorkflowLayoutRepositoryTests
{
    private static (SqliteConnection Conn, WorkflowDbContext Ctx) NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var ctx = new WorkflowDbContext(
            new DbContextOptionsBuilder<WorkflowDbContext>().UseSqlite(conn).Options);
        ctx.Database.EnsureCreated();

        return (conn, ctx);
    }

    private static WorkflowLayout Layout(string name, string owner, string json = "{}") => new()
    {
        Name = name,
        OwnerUserId = owner,
        LayoutJson = json,
        SchemaVersion = 1,
    };

    [Fact]
    public async Task UpsertAsync_PersistsANewLayout_AndAssignsAnId()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a"));

        Assert.True(saved.Id > 0);
        // A second context over the same connection proves it really hit the database rather
        // than only the change tracker.
        using var verify = new WorkflowDbContext(
            new DbContextOptionsBuilder<WorkflowDbContext>().UseSqlite(conn).Options);
        Assert.Equal("First", verify.WorkflowLayouts.Single().Name);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesAnExistingLayoutInPlace_RatherThanInsertingASecondRow()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a", "{\"v\":1}"));
        saved.LayoutJson = "{\"v\":2}";
        await repo.UpsertAsync(saved);

        Assert.Equal(1, await ctx.WorkflowLayouts.CountAsync());
        Assert.Equal("{\"v\":2}", (await ctx.WorkflowLayouts.SingleAsync()).LayoutJson);
    }

    [Fact]
    public async Task UpsertAsync_StampsUpdatedAt()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a"));
        var firstStamp = saved.UpdatedAt;

        saved.Name = "Renamed";
        var updated = await repo.UpsertAsync(saved);

        Assert.True(updated.UpdatedAt >= firstStamp);
    }

    [Fact]
    public async Task ListForUserAsync_ReturnsOnlyThatUsersLayouts()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        await repo.UpsertAsync(Layout("Mine", "user-a"));
        await repo.UpsertAsync(Layout("Theirs", "user-b"));

        var mine = await repo.ListForUserAsync("user-a");

        Assert.Single(mine);
        Assert.Equal("Mine", mine[0].Name);
    }

    [Fact]
    public async Task GetForUserAsync_RefusesAnotherUsersLayout()
    {
        // Layouts are per-user in v1. Returning someone else's by id would be a quiet
        // authorization hole the UI has no reason to guard against.
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var theirs = await repo.UpsertAsync(Layout("Theirs", "user-b"));

        Assert.Null(await repo.GetForUserAsync(theirs.Id, "user-a"));
        Assert.NotNull(await repo.GetForUserAsync(theirs.Id, "user-b"));
    }

    [Fact]
    public async Task SoftDeleteAsync_HidesTheLayoutFromListAndGet_ButKeepsTheRow()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("Doomed", "user-a"));

        Assert.True(await repo.SoftDeleteAsync(saved.Id, "user-a"));

        Assert.Empty(await repo.ListForUserAsync("user-a"));
        Assert.Null(await repo.GetForUserAsync(saved.Id, "user-a"));
        Assert.Equal(1, await ctx.WorkflowLayouts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsFalseForAnotherUsersLayout()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var theirs = await repo.UpsertAsync(Layout("Theirs", "user-b"));

        Assert.False(await repo.SoftDeleteAsync(theirs.Id, "user-a"));
        Assert.Single(await repo.ListForUserAsync("user-b"));
    }

    [Fact]
    public async Task ListForUserAsync_ReturnsMostRecentlyUpdatedFirst()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var older = await repo.UpsertAsync(Layout("Older", "user-a"));
        older.UpdatedAt = older.UpdatedAt.AddHours(-2);
        await repo.UpsertAsync(older);
        await repo.UpsertAsync(Layout("Newer", "user-a"));

        var list = await repo.ListForUserAsync("user-a");

        Assert.Equal("Newer", list[0].Name);
    }
}
