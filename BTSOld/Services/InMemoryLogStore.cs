using Microsoft.Identity.Client;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BatteryTestingSystem.Services
{

    public class UiLogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogEventLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; } = new();
        public string? Exception { get; set; }
    }
    public class InMemoryLogStore
    {
        public static ConcurrentQueue<UiLogEntry> Logs { get; } = new();

        public static LoggingLevelSwitch LevelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
    }

    public class InMemorySink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var entry = new UiLogEntry
            {
                Timestamp = logEvent.Timestamp.LocalDateTime,
                Level = logEvent.Level,
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString()
            };

            foreach (var prop in logEvent.Properties)
            {
                entry.Properties[prop.Key] = prop.Value.ToString();
            }

            InMemoryLogStore.Logs.Enqueue(entry);

            // keep last 500 logs
            while (InMemoryLogStore.Logs.Count > 500)
                InMemoryLogStore.Logs.TryDequeue(out _);
        }

    }

}
