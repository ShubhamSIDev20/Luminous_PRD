using System;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Alarms;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmRetentionTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cutoff_IsRetentionDaysBeforeNow()
    {
        var cutoff = AlarmRetention.CutoffUtc(Now, 30);

        Assert.Equal(new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc), cutoff);
    }

    [Fact]
    public async Task Prune_DeletesOldAcknowledgedRows()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "old-acked",
            LastSeenUtc = Now.AddDays(-40),
            AcknowledgedAtUtc = Now.AddDays(-40)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(1, deleted);
        Assert.Empty(repo.Rows);
    }

    [Fact]
    public async Task Prune_KeepsOldRowThatIsStillUnacknowledgedAndUncleared()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "old-unresolved",
            LastSeenUtc = Now.AddDays(-99)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(0, deleted);
        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Prune_KeepsRecentRows()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "recent",
            LastSeenUtc = Now.AddDays(-2),
            AcknowledgedAtUtc = Now.AddDays(-2)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(0, deleted);
        Assert.Single(repo.Rows);
    }
}
