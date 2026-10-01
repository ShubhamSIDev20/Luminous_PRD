namespace BatteryTestingSystem.Services
{
    public class EventBusService : IDisposable
    {
        private class Subscription
        {
            public string SubscriberId { get; set; } = default!;
            public Func<object?, Task> Handler { get; set; } = default!;
            public DateTime LastActive { get; set; } = DateTime.UtcNow;
        }

        private readonly Dictionary<string, List<Subscription>> _handlers = new();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        public EventBusService()
        {
            _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        // 
        public bool HasSubscribers(string topic) => _handlers.ContainsKey(topic) && _handlers[topic].Count > 0;

        // ── Subscribe — adds topic, returns subscriberId ────────
        public string Subscribe<T>(string topic, Func<T, Task> handler)
        {
            if (!_handlers.ContainsKey(topic))
                _handlers[topic] = new();

            var subscriberId = Guid.NewGuid().ToString();

            _handlers[topic].Add(new Subscription
            {
                SubscriberId = subscriberId,
                Handler = payload =>
                {
                    if (payload is not T t) return Task.CompletedTask;
                    return handler(t);
                }
            });

            return subscriberId;
        }

        // ── Unsubscribe — removes subscriber, cleans topic if empty ──
        public void Unsubscribe(string topic, string subscriberId)
        {
            if (!_handlers.TryGetValue(topic, out var subs)) return;

            subs.RemoveAll(s => s.SubscriberId == subscriberId);

            // Remove topic if no subscribers left
            if (subs.Count == 0)
                _handlers.Remove(topic);
        }


        // ── Publish — only sends if topic exists (has subscribers) ──
        public async Task PublishAsync<T>(string topic, T? payload)
        {
            // Topic doesn't exist = no subscribers = skip silently
            if (!_handlers.TryGetValue(topic, out var subs)) return;

            foreach (var sub in subs.ToList())
            {
                try
                {
                    sub.LastActive = DateTime.UtcNow;
                    await sub.Handler(payload);
                }
                catch (Exception ex) { }
            }
            
        }

        // ── Cleanup ─────────────────────────────────────────────
        private void Cleanup(object? state)
        {
            var cutoff = DateTime.UtcNow - _expiry;
            foreach (var topic in _handlers.Keys.ToList())
            {
                _handlers[topic].RemoveAll(s => s.LastActive < cutoff);
                if (_handlers[topic].Count == 0)
                    _handlers.Remove(topic);
            }
        }

        public void Dispose() => _cleanupTimer.Dispose();
    }
}