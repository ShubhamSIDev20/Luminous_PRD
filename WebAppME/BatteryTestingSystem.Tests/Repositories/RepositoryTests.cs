using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BatteryTestingSystem.Tests.Repositories;

/// <summary>
/// Pass 2 of the test-project build-out: repository tests against a real EF Core provider
/// (SQLite in-memory), not a fake DbSet. The point is to catch what a fake can't - wrong
/// ordering, wrong filter predicates translated to SQL, and whether SaveChangesAsync was
/// actually called - while staying fast and hermetic (fresh DB per test, no shared state).
/// </summary>
public class RepositoryTests
{
    private static (SqliteConnection Conn, AppDbContext Ctx) NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        ctx.Database.EnsureCreated();

        return (conn, ctx);
    }

    // ============================================================ Repository<T> (generic base)

    public class GenericRepositoryTests
    {
        [Fact]
        public async Task AddAsync_TracksTheEntity_ButDoesNotPersistUntilSaveChangesIsCalledSeparately()
        {
            // Repository<T>.AddAsync only stages the entity (_dbSet.AddAsync) - it never calls
            // SaveChangesAsync itself. Callers who forget the follow-up SaveChangesAsync silently
            // lose the write. Pinning this so it stays a known contract, not a surprise.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var schedule = new ProgramSchedule { Name = "never saved", ScheduledAt = DateTime.UtcNow };

                await repo.AddAsync(schedule);

                using var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
                Assert.Empty(await freshCtx.ProgramSchedules.ToListAsync());

                await repo.SaveChangesAsync();
                Assert.Single(await freshCtx.ProgramSchedules.ToListAsync());
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task DeleteAsync_AlsoRequiresAnExplicitSaveChanges()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var schedule = new ProgramSchedule { Name = "to delete", ScheduledAt = DateTime.UtcNow };
                await repo.AddAsync(schedule);
                await repo.SaveChangesAsync();

                await repo.DeleteAsync(schedule);
                using (var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options))
                {
                    Assert.Single(await freshCtx.ProgramSchedules.ToListAsync());
                }

                await repo.SaveChangesAsync();
                using (var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options))
                {
                    Assert.Empty(await freshCtx.ProgramSchedules.ToListAsync());
                }
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetByIdAsync_Long_ReturnsNull_WhenNotFound()
        {
            // ProgramSchedule.Id is `long` (DatabaseGeneratedOption.Identity), so this exercises
            // Repository<T>'s long-keyed overload rather than the int-keyed one.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                Assert.Null(await repo.GetByIdAsync(999L));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }
    }

    // ================================================================== SchedulerRepository

    public class SchedulerRepositoryTests
    {
        [Fact]
        public async Task GetActiveSchedulesAsync_ExcludesDeletedAndInactive_OrderedNewestFirst()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var older = new ProgramSchedule { Name = "older-active", IsActive = true, ScheduledAt = DateTime.UtcNow.AddHours(-2) };
                var newer = new ProgramSchedule { Name = "newer-active", IsActive = true, ScheduledAt = DateTime.UtcNow.AddHours(-1) };
                var inactive = new ProgramSchedule { Name = "inactive", IsActive = false, ScheduledAt = DateTime.UtcNow };
                var deleted = new ProgramSchedule { Name = "deleted", IsActive = true, ScheduledAt = DateTime.UtcNow, IsDeleted = true };

                ctx.ProgramSchedules.AddRange(older, newer, inactive, deleted);
                await ctx.SaveChangesAsync();

                var result = await repo.GetActiveSchedulesAsync();

                Assert.Equal(new[] { "newer-active", "older-active" }, result.Select(s => s.Name));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetLogsByScheduleIdAsync_FiltersByScheduleId_OrderedNewestFirst_RespectsLimit()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var schedule = new ProgramSchedule { Name = "s1", ScheduledAt = DateTime.UtcNow };
                var otherSchedule = new ProgramSchedule { Name = "s2", ScheduledAt = DateTime.UtcNow };
                ctx.ProgramSchedules.AddRange(schedule, otherSchedule);
                await ctx.SaveChangesAsync();

                for (int i = 0; i < 5; i++)
                {
                    await repo.AddLogAsync(new ScheduleExecutionLog
                    {
                        ScheduleId = schedule.Id,
                        ExecutedAt = DateTime.UtcNow.AddMinutes(i),
                        Status = "Success"
                    });
                }
                await repo.AddLogAsync(new ScheduleExecutionLog { ScheduleId = otherSchedule.Id, Status = "Success" });

                var result = await repo.GetLogsByScheduleIdAsync(schedule.Id, limit: 3);

                Assert.Equal(3, result.Count);
                Assert.All(result, l => Assert.Equal(schedule.Id, l.ScheduleId));
                Assert.True(result[0].ExecutedAt > result[1].ExecutedAt);
                Assert.True(result[1].ExecutedAt > result[2].ExecutedAt);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetAllLogsAsync_IncludesTheParentSchedule_AcrossAllSchedules()
        {
            // Distinguishes GetAllLogsAsync from GetLogsByScheduleIdAsync: the former Includes
            // the Schedule navigation property (used to render a schedule name against each
            // log row) and is not scoped to one schedule.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var schedule = new ProgramSchedule { Name = "named-schedule", ScheduledAt = DateTime.UtcNow };
                ctx.ProgramSchedules.Add(schedule);
                await ctx.SaveChangesAsync();

                await repo.AddLogAsync(new ScheduleExecutionLog { ScheduleId = schedule.Id, Status = "Success" });

                using var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
                var freshRepo = new SchedulerRepository(freshCtx);

                var result = await freshRepo.GetAllLogsAsync(limit: 10);

                Assert.Single(result);
                Assert.NotNull(result[0].Schedule);
                Assert.Equal("named-schedule", result[0].Schedule!.Name);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task AddLogAsync_PersistsImmediately_UnlikeTheGenericBaseRepository()
        {
            // Unlike Repository<T>.AddAsync, this override calls SaveChangesAsync itself - the
            // subtype-vs-base inconsistency is exactly the kind of thing worth pinning.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new SchedulerRepository(ctx);
                var schedule = new ProgramSchedule { Name = "s", ScheduledAt = DateTime.UtcNow };
                ctx.ProgramSchedules.Add(schedule);
                await ctx.SaveChangesAsync();

                await repo.AddLogAsync(new ScheduleExecutionLog { ScheduleId = schedule.Id, Status = "Success" });

                using var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
                Assert.Single(await freshCtx.ScheduleExecutionLogs.ToListAsync());
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }
    }

    // =================================================================== ExportRepository

    public class ExportRepositoryTests
    {
        private static ExportRecord NewRecord(string user = "alice", string session = "session.db", DateTime? requestedAt = null) => new()
        {
            RequestedBy = user,
            SessionFilePath = session,
            RequestedAt = requestedAt ?? DateTime.Now,
            Status = ExportStatus.Pending
        };

        [Fact]
        public async Task CreateAsync_PersistsImmediately_AndReturnsTheSameEntity()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var record = await repo.CreateAsync(NewRecord());

                Assert.NotEqual(0, record.Id);

                using var freshCtx = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
                Assert.Single(await freshCtx.ExportRecords.ToListAsync());
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetByUserAsync_ExcludesSoftDeleted_AndOtherUsers_OrderedNewestFirst()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var older = await repo.CreateAsync(NewRecord(requestedAt: DateTime.Now.AddHours(-2)));
                var newer = await repo.CreateAsync(NewRecord(requestedAt: DateTime.Now.AddHours(-1)));
                var otherUser = await repo.CreateAsync(NewRecord(user: "bob"));
                var deleted = await repo.CreateAsync(NewRecord());
                await repo.DeleteAsync(deleted.Id);

                var result = await repo.GetByUserAsync("alice");

                Assert.Equal(new[] { newer.Id, older.Id }, result.Select(r => r.Id));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetBySessionAsync_FiltersBySessionPath_ExcludingSoftDeleted()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var matching = await repo.CreateAsync(NewRecord(session: "target.db"));
                await repo.CreateAsync(NewRecord(session: "other.db"));
                var matchingDeleted = await repo.CreateAsync(NewRecord(session: "target.db"));
                await repo.DeleteAsync(matchingDeleted.Id);

                var result = await repo.GetBySessionAsync("target.db");

                Assert.Equal(new[] { matching.Id }, result.Select(r => r.Id));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Theory]
        [InlineData(ExportStatus.Ready, true)]
        [InlineData(ExportStatus.Failed, true)]
        [InlineData(ExportStatus.Processing, false)]
        public async Task UpdateStatusAsync_SetsCompletedAt_OnlyForTerminalStatuses(ExportStatus status, bool expectCompletedAt)
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var record = await repo.CreateAsync(NewRecord());

                await repo.UpdateStatusAsync(record.Id, status, errorMessage: status == ExportStatus.Failed ? "boom" : null);

                var reloaded = await repo.GetByIdAsync(record.Id);
                Assert.Equal(status, reloaded!.Status);
                Assert.Equal(expectCompletedAt, reloaded.CompletedAt.HasValue);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task MarkReadyAsync_SetsFilePathSizeAndCompletedAt()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var record = await repo.CreateAsync(NewRecord());

                await repo.MarkReadyAsync(record.Id, "C:/exports/out.xlsx", 4096);

                var reloaded = await repo.GetByIdAsync(record.Id);
                Assert.Equal(ExportStatus.Ready, reloaded!.Status);
                Assert.Equal("C:/exports/out.xlsx", reloaded.ExportFilePath);
                Assert.Equal(4096, reloaded.FileSizeBytes);
                Assert.NotNull(reloaded.CompletedAt);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task DeleteAsync_IsSoft_TheRowStillExistsForGetByIdAsync()
        {
            // GetByIdAsync has no IsDeleted filter, but GetByUserAsync/GetBySessionAsync do -
            // a "deleted" export is invisible in list views yet still directly reachable by id.
            // Documenting the asymmetry rather than assuming it's a bug: direct-id lookups are
            // used by export-download links that may still need to report "this was deleted".
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var record = await repo.CreateAsync(NewRecord());

                await repo.DeleteAsync(record.Id);

                var reloaded = await repo.GetByIdAsync(record.Id);
                Assert.NotNull(reloaded);
                Assert.True(reloaded!.IsDeleted);
                Assert.Equal(ExportStatus.Deleted, reloaded.Status);

                Assert.Empty(await repo.GetByUserAsync("alice"));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task MarkSupersededAsync_SetsStatus()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                var record = await repo.CreateAsync(NewRecord());

                await repo.MarkSupersededAsync(record.Id);

                Assert.Equal(ExportStatus.Superseded, (await repo.GetByIdAsync(record.Id))!.Status);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Theory]
        [MemberData(nameof(NoOpMutators))]
        public async Task MutatingMethods_ForANonExistentId_SilentlyNoOp(Func<ExportRepository, Task> mutate)
        {
            // Every mutator does `if (rec == null) return;` with no error surfaced anywhere.
            // Documented deliberately: a caller passing a stale/bad id gets no signal at all.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new ExportRepository(ctx);
                await mutate(repo);
                // No exception is the assertion; nothing else to check against an empty table.
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        public static IEnumerable<object[]> NoOpMutators()
        {
            yield return new object[] { (Func<ExportRepository, Task>)(r => r.UpdateStatusAsync(999, ExportStatus.Ready)) };
            yield return new object[] { (Func<ExportRepository, Task>)(r => r.MarkReadyAsync(999, "x", 1)) };
            yield return new object[] { (Func<ExportRepository, Task>)(r => r.DeleteAsync(999)) };
            yield return new object[] { (Func<ExportRepository, Task>)(r => r.MarkSupersededAsync(999)) };
        }
    }

    // ============================================================ UserCircuitAccessRepository

    public class UserCircuitAccessRepositoryTests
    {
        // UserCircuitAccess.UserId is a foreign key onto Identity's AspNetUsers table, and
        // Sqlite's EF Core provider enforces foreign keys by default - so every UserId used
        // here must correspond to a real seeded ApplicationUser row, or SetUserCircuitAccessAsync
        // fails its (caught, silent) SaveChangesAsync and returns Success = false.
        private static async Task SeedUserAsync(AppDbContext ctx, string userId)
        {
            ctx.Users.Add(new global::BatteryTestingSystem.Models.Entities.ApplicationUser { Id = userId, UserName = userId, FullName = userId });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task SetUserCircuitAccessAsync_ReplacesAllOfTheUsersExistingRows()
        {
            var (conn, ctx) = NewContext();
            try
            {
                await SeedUserAsync(ctx, "user-1");
                var repo = new UserCircuitAccessRepository(ctx);

                var first = await repo.SetUserCircuitAccessAsync("user-1", new() { (1, 1), (1, 2) });
                Assert.True(first.Success);

                var second = await repo.SetUserCircuitAccessAsync("user-1", new() { (2, 5) });
                Assert.True(second.Success);

                var result = await repo.GetByUserIdAsync("user-1");

                Assert.True(result.Success);
                Assert.Single(result.Data!);
                Assert.Equal((2, 5), (result.Data![0].DeviceId, result.Data[0].CircuitId));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task SetUserCircuitAccessAsync_NeverTouchesOtherUsersRows()
        {
            var (conn, ctx) = NewContext();
            try
            {
                await SeedUserAsync(ctx, "user-1");
                await SeedUserAsync(ctx, "user-2");
                var repo = new UserCircuitAccessRepository(ctx);
                await repo.SetUserCircuitAccessAsync("user-1", new() { (1, 1) });
                await repo.SetUserCircuitAccessAsync("user-2", new() { (1, 2) });

                await repo.SetUserCircuitAccessAsync("user-1", new() { (9, 9) });

                var user2 = await repo.GetByUserIdAsync("user-2");
                Assert.Single(user2.Data!);
                Assert.Equal((1, 2), (user2.Data![0].DeviceId, user2.Data[0].CircuitId));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task SetUserCircuitAccessAsync_WithAnEmptyList_ClearsAccess()
        {
            var (conn, ctx) = NewContext();
            try
            {
                await SeedUserAsync(ctx, "user-1");
                var repo = new UserCircuitAccessRepository(ctx);
                await repo.SetUserCircuitAccessAsync("user-1", new() { (1, 1) });

                await repo.SetUserCircuitAccessAsync("user-1", new());

                var result = await repo.GetByUserIdAsync("user-1");
                Assert.Empty(result.Data!);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetAllAsync_ReturnsRowsAcrossEveryUser()
        {
            var (conn, ctx) = NewContext();
            try
            {
                await SeedUserAsync(ctx, "user-1");
                await SeedUserAsync(ctx, "user-2");
                var repo = new UserCircuitAccessRepository(ctx);
                await repo.SetUserCircuitAccessAsync("user-1", new() { (1, 1) });
                await repo.SetUserCircuitAccessAsync("user-2", new() { (2, 2) });

                var result = await repo.GetAllAsync();

                Assert.True(result.Success);
                Assert.Equal(2, result.Data!.Count);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetByUserIdAsync_OnADisposedContext_ReturnsAFailureResponse_RatherThanThrowing()
        {
            // Every method here wraps its body in try/catch and returns CommonResponse.Fail -
            // pinning that the exception path is actually reachable and does not escape as an
            // unhandled exception, using a disposed context to force a real EF failure.
            var (conn, ctx) = NewContext();
            var repo = new UserCircuitAccessRepository(ctx);
            ctx.Dispose();
            conn.Dispose();

            var result = await repo.GetByUserIdAsync("user-1");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Message));
        }
    }

    // ========================================================================= AuditRepository

    public class AuditRepositoryTests
    {
        [Fact]
        public async Task LogEventAsync_FillsIpUserAgentAndUser_OnlyWhenNotAlreadySet()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new AuditRepository(ctx);
                var withValues = new AuditLog
                {
                    Module = ModuleName.SYSTEM,
                    Action = AuditActionType.CREATE,
                    User = "explicit-user",
                    IPAddress = "10.0.0.1",
                    UserAgent = "explicit-agent"
                };

                var result = await repo.LogEventAsync(withValues);

                Assert.True(result.Success);
                Assert.Equal("explicit-user", withValues.User);
                Assert.Equal("10.0.0.1", withValues.IPAddress);
                Assert.Equal("explicit-agent", withValues.UserAgent);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task LogEventAsync_WithNoAmbientHttpContext_DefaultsUserToEmpty_NotNull()
        {
            // CurrentUser reads from a static IHttpContextAccessor that is never configured in
            // this test process - documents that the fallback is an empty string, not a crash
            // and not "UNKNOWN_USER" (that string is only used when a request *is* authenticated
            // but has no name claim).
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new AuditRepository(ctx);
                var log = new AuditLog { Module = ModuleName.SYSTEM, Action = AuditActionType.LOGIN };

                var result = await repo.LogEventAsync(log);

                Assert.True(result.Success);
                Assert.Equal(string.Empty, log.User);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task LogEventAsync_PurgesLogsOlderThanThreeMonths_OnEveryCall()
        {
            // This is a retention side-effect hidden inside what looks like a pure "write one
            // log" method - every LogEventAsync call also runs a delete sweep. Worth pinning so
            // nobody "simplifies" LogEventAsync without noticing retention silently stops.
            var (conn, ctx) = NewContext();
            try
            {
                ctx.AuditLogs.Add(new AuditLog
                {
                    Module = ModuleName.SYSTEM,
                    Action = AuditActionType.VIEW,
                    Timestamp = DateTime.UtcNow.AddMonths(-4)
                });
                ctx.AuditLogs.Add(new AuditLog
                {
                    Module = ModuleName.SYSTEM,
                    Action = AuditActionType.VIEW,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                });
                await ctx.SaveChangesAsync();

                var repo = new AuditRepository(ctx);
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.SYSTEM, Action = AuditActionType.CREATE });

                var remaining = await ctx.AuditLogs.AsNoTracking().ToListAsync();
                Assert.Equal(2, remaining.Count);
                Assert.DoesNotContain(remaining, l => l.Timestamp < DateTime.UtcNow.AddMonths(-3));
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetAuditRecordsAsync_FiltersByModuleActionUserAndDateRange()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new AuditRepository(ctx);
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.CIRCUIT, Action = AuditActionType.START, User = "alice", Timestamp = DateTime.UtcNow.AddDays(-1) });
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.CIRCUIT, Action = AuditActionType.STOP, User = "alice", Timestamp = DateTime.UtcNow });
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.BATTERY, Action = AuditActionType.START, User = "bob", Timestamp = DateTime.UtcNow });

                var result = await repo.GetAuditRecordsAsync(new AuditLogQueryParameters
                {
                    Module = ModuleName.CIRCUIT,
                    Action = AuditActionType.START,
                    User = "ali"
                });

                Assert.True(result.Success);
                var log = Assert.Single(result.Data!);
                Assert.Equal(ModuleName.CIRCUIT, log.Module);
                Assert.Equal(AuditActionType.START, log.Action);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetAuditRecordsAsync_WithNoMatches_ReturnsAFailureResponse_NotAnEmptySuccess()
        {
            // Documents current behaviour rather than assuming it's correct: "no rows found" is
            // modeled as CommonResponse.Fail, not CommonResponse.Ok(empty list). A caller that
            // treats Success == false as an error condition will surface "no logs match your
            // filter" as if something had gone wrong.
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new AuditRepository(ctx);

                var result = await repo.GetAuditRecordsAsync(new AuditLogQueryParameters { Module = ModuleName.DBC });

                Assert.False(result.Success);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }

        [Fact]
        public async Task GetAuditRecordsAsync_WithNullParameters_ReturnsEverything_OrderedNewestFirst()
        {
            var (conn, ctx) = NewContext();
            try
            {
                var repo = new AuditRepository(ctx);
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.SYSTEM, Action = AuditActionType.VIEW, Timestamp = DateTime.UtcNow.AddHours(-1) });
                await repo.LogEventAsync(new AuditLog { Module = ModuleName.SYSTEM, Action = AuditActionType.VIEW, Timestamp = DateTime.UtcNow });

                var result = await repo.GetAuditRecordsAsync(null);

                Assert.True(result.Success);
                Assert.Equal(2, result.Data!.Count);
                Assert.True(result.Data[0].Timestamp > result.Data[1].Timestamp);
            }
            finally { ctx.Dispose(); conn.Dispose(); }
        }
    }
}
