using System;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;
using BatteryTestingSystem.Services.Interfaces;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmServiceTests
{
    private static readonly DateTime T0 = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    private static (AlarmService Service, FakeAlarmRepository Repo) Make(DateTime now)
    {
        var repo = new FakeAlarmRepository();
        var options = new AlarmOptions
        {
            EscalationCooldownSec = 60,
            StormThreshold = 100,
            StormWindowMinutes = 5,
            FlushIntervalSeconds = 5,
            BellActiveTake = 50
        };
        var service = new AlarmService(() => repo, options, new AlarmPolicy(options))
        {
            UtcNow = () => now
        };
        return (service, repo);
    }

    private static AlarmRequest Request(string key = "1/0/3/channel-error") => new()
    {
        AlarmKey = key,
        Severity = SeverityLevel.ERROR,
        Source = AlarmSource.ChannelError,
        DeviceId = "1",
        BoardNumber = 0,
        ChannelNumber = 3,
        Title = "Channel error",
        Message = "Over voltage"
    };

    [Fact]
    public async Task FirstRaise_InsertsOneRow()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());

        Assert.Single(repo.Rows);
        Assert.Equal(1, repo.Rows[0].OccurrenceCount);
        Assert.Equal(1, repo.InsertCount);
    }

    [Fact]
    public async Task RepeatRaise_CollapsesIntoOneRowWithCount()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());
        await service.RaiseAsync(Request());
        await service.RaiseAsync(Request());
        await service.FlushPendingAsync();

        Assert.Single(repo.Rows);
        Assert.Equal(3, repo.Rows[0].OccurrenceCount);
    }

    [Fact]
    public async Task RepeatRaise_IsDebounced_AndFlushedOnDemand()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());   // insert, always written
        await service.RaiseAsync(Request());   // bump inside flush interval -> queued
        await service.RaiseAsync(Request());   // bump inside flush interval -> queued

        Assert.Equal(0, repo.SaveCount);

        await service.FlushPendingAsync();

        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(3, repo.Rows[0].OccurrenceCount);
    }

    [Fact]
    public async Task RaiseAfterAcknowledge_OpensANewRow()
    {
        var (service, repo) = Make(T0);
        await service.RaiseAsync(Request());
        await service.AcknowledgeAsync(repo.Rows[0].Id, "operator1");

        await service.RaiseAsync(Request());

        Assert.Equal(2, repo.Rows.Count);
    }

    [Fact]
    public async Task Acknowledge_RecordsUserAndTimestamp()
    {
        var (service, repo) = Make(T0);
        await service.RaiseAsync(Request());

        await service.AcknowledgeAsync(repo.Rows[0].Id, "operator1");

        Assert.Equal("operator1", repo.Rows[0].AcknowledgedBy);
        Assert.Equal(T0, repo.Rows[0].AcknowledgedAtUtc);
    }

    [Fact]
    public async Task Clear_RemovesAlarmFromActiveList()
    {
        var (service, _) = Make(T0);
        await service.RaiseAsync(Request("1/comms-loss"));

        await service.ClearAsync("1/comms-loss");
        var active = await service.GetActiveAsync(50);

        Assert.Empty(active);
    }

    [Fact]
    public async Task RaiseWithNoSubscriber_StillPersists()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());

        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Raise_NotifiesSubscribersWithEscalationFlag()
    {
        var (service, _) = Make(T0);
        AlarmChanged? received = null;
        service.OnChanged += c => received = c;

        await service.RaiseAsync(Request());

        Assert.NotNull(received);
        Assert.Equal(AlarmChangeKind.Raised, received!.Kind);
        Assert.True(received.ShouldEscalate);
    }

    [Fact]
    public async Task StormGuard_EmitsOneSummaryAlarm()
    {
        var repo = new FakeAlarmRepository();
        var options = new AlarmOptions
        {
            EscalationCooldownSec = 0,
            StormThreshold = 3,
            StormWindowMinutes = 5,
            StormCooloffMinutes = 15,
            FlushIntervalSeconds = 0
        };
        var service = new AlarmService(() => repo, options, new AlarmPolicy(options))
        {
            UtcNow = () => T0
        };

        for (var i = 0; i < 5; i++)
            await service.RaiseAsync(Request("1/0/3/flapping"));

        var summaries = repo.Rows.Count(r => r.AlarmKey.EndsWith("/storm"));
        Assert.Equal(1, summaries);
    }
}
