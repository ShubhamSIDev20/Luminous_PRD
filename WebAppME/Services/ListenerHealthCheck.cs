using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BatteryTestingSystem.Services
{
    /// <summary>
    /// Reports the state of the three sockets the hardware depends on: TCP 9999
    /// (commands), UDP 10000 (view data) and UDP 10001 (store data).
    /// <para>
    /// <see cref="ChannelManager"/> keeps those ports bound under its own supervisor and
    /// never gives up, so the process stays alive even when a port cannot bind. This
    /// check exists so that state is visible: without it a customer site sees a healthy
    /// web app while the hardware ports are dead.
    /// </para>
    /// </summary>
    public sealed class ListenerHealthCheck : IHealthCheck
    {
        private readonly ChannelManager _channelManager;

        public ListenerHealthCheck(ChannelManager channelManager) => _channelManager = channelManager;

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var states = _channelManager.ListenerStates;

            var data = states.ToDictionary(
                s => s.Name,
                s => (object)new
                {
                    s.Port,
                    s.IsListening,
                    s.ConsecutiveFailures,
                    s.LastError,
                    s.LastErrorUtc
                });

            // No state at all means StartInternal has not run yet (startup window).
            if (states.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Hardware listeners have not started yet.", data: data));
            }

            var down = states.Where(s => !s.IsListening).Select(s => $"{s.Name}:{s.Port}").ToArray();

            if (down.Length > 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Hardware listener(s) down: {string.Join(", ", down)}.", data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "TCP 9999 and UDP 10000/10001 are listening.", data: data));
        }
    }
}
