using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace BatteryTestingSystem.Services.Alarms
{
    public static class AlarmRetention
    {
        public static DateTime CutoffUtc(DateTime nowUtc, int retentionDays)
            => nowUtc.AddDays(-retentionDays);

        /// <summary>Rows deleted per batch, to avoid a long write lock on the first run.</summary>
        public const int BatchSize = 500;

        /// <summary>Safety stop so one pass cannot run unbounded.</summary>
        public const int MaxBatchesPerPass = 200;
    }

    /// <summary>
    /// Prunes alarms older than Alarms:RetentionDays. Runs once shortly after startup, then
    /// daily. Never deletes a row that is still unacknowledged and uncleared.
    /// </summary>
    public class AlarmRetentionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AlarmOptions _options;
        private readonly Serilog.ILogger _log = Log.ForContext<AlarmRetentionService>();

        public AlarmRetentionService(IServiceScopeFactory scopeFactory, IOptions<AlarmOptions> options)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the app finish starting before touching the database.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PruneOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Alarm retention pass failed");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task PruneOnceAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();

            var cutoff = AlarmRetention.CutoffUtc(DateTime.UtcNow, _options.RetentionDays);
            var total = 0;

            for (var i = 0; i < AlarmRetention.MaxBatchesPerPass && !token.IsCancellationRequested; i++)
            {
                var deleted = await repo.PruneAsync(cutoff, AlarmRetention.BatchSize);
                total += deleted;
                if (deleted < AlarmRetention.BatchSize) break;
            }

            if (total > 0)
                _log.Information("Alarm retention pruned {Count} rows older than {Cutoff:u}", total, cutoff);
        }
    }
}
