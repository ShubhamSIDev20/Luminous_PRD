using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// Reproduces, without a browser, the failure that 64-channel registration produced: a Blazor
/// circuit's scoped services all share one AppDbContext, and hardware-thread events used to
/// start overlapping queries on it.
/// </summary>
public class SharedDbContextConcurrencyTests
{
    private static (SqliteConnection Conn, AppDbContext Ctx) NewSharedContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        ctx.Database.EnsureCreated();

        return (conn, ctx);
    }

    /// <summary>
    /// The bug itself: unguarded overlapping queries on one shared context. Retried a few
    /// rounds so the assertion cannot flake on a machine that happens to serialise one burst -
    /// the point is that the collision is reachable, not that it happens on every round.
    /// </summary>
    [Fact]
    public async Task UnguardedConcurrentQueries_OnOneSharedContext_Collide()
    {
        var (conn, ctx) = NewSharedContext();
        try
        {
            var failures = new ConcurrentQueue<Exception>();

            for (int round = 0; round < 5 && failures.IsEmpty; round++)
            {
                var bursts = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
                {
                    try { await ctx.Channels.AsNoTracking().ToListAsync(); }
                    catch (Exception ex) { failures.Enqueue(ex); }
                }));

                await Task.WhenAll(bursts);
            }

            Assert.Contains(failures, e => e.Message.Contains("second operation"));
        }
        finally { ctx.Dispose(); conn.Dispose(); }
    }

    /// <summary>
    /// The same burst routed through <see cref="CoalescingRunner"/> - which is how
    /// DashboardView and MainLayout now consume hardware events - must never collide.
    /// </summary>
    [Fact]
    public async Task CoalescingRunner_ProtectsSharedDbContext_FromConcurrentQueries()
    {
        var (conn, ctx) = NewSharedContext();
        try
        {
            var runner = new CoalescingRunner();
            var failures = new ConcurrentQueue<Exception>();

            Task Query() => ctx.Channels.AsNoTracking().ToListAsync();

            var bursts = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
            {
                try { await runner.RunAsync(Query); }
                catch (Exception ex) { failures.Enqueue(ex); }
            }));

            await Task.WhenAll(bursts);

            Assert.Empty(failures);
        }
        finally { ctx.Dispose(); conn.Dispose(); }
    }
}
