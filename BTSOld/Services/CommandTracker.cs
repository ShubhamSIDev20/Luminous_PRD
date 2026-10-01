using BatteryTestingSystem.Services.Implementations;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BatteryTestingSystem.Services
{
    public static class CommandTracker
    {
        private static ConcurrentDictionary<ushort, CommandInfo> _commands;

        private static ServerSessionStorageService? _sessionService;
        private static Timer? _timer;

        private static ushort _currentId = 0;
        private static readonly object _idLock = new();

        private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

        static CommandTracker()
        {
            try
            {
                var service = ServiceLocator.GetScoped<ServerSessionStorageService>();

                if (service.Scope != null || service.Service != null)
                    _sessionService = service.Service;

                _commands = _sessionService?
                    .GetComponentState<ConcurrentDictionary<ushort, CommandInfo>>("commands")
                    ?? new ConcurrentDictionary<ushort, CommandInfo>();

                _timer = new Timer(RemoveExpired, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
            }
            catch
            {
                _commands = new ConcurrentDictionary<ushort, CommandInfo>();
            }
        }

        // ✅ Generate 2-byte unique ID (ushort = 0–65535)
        private static ushort GenerateId()
        {
            try
            {
                lock (_idLock)
                {
                    const int maxAttempts = ushort.MaxValue;
                    int attempts = 0;

                    do
                    {
                        _currentId++;

                        if (_currentId > ushort.MaxValue)
                            _currentId = 1;

                        attempts++;

                        if (!_commands.ContainsKey(_currentId))
                            return _currentId;

                    } while (attempts < maxAttempts);

                    return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        // ✅ Add a command safely
        public static ushort AddCommand(string remark)
        {
            try
            {
                var id = GenerateId();

                if (id == 0) return 0;

                var cmd = new CommandInfo(
                    id,
                    remark,
                    DateTime.UtcNow
                );

                _commands[id] = cmd;

                try
                {
                    _sessionService?.SetComponentState("commands", _commands);
                }
                catch { }

                return id;
            }
            catch
            {
                return 0;
            }
        }

        // ✅ Get command safely
        public static CommandInfo? GetCommand(ushort id, bool remove = false)
        {
            if (id == 0)
                return null;

            try
            {
                if (_commands.TryGetValue(id, out var cmd))
                {
                    if (remove)
                    {
                        try
                        {
                            _commands.TryRemove(id, out _);
                        }
                        catch { }
                    }

                    try
                    {
                        _sessionService?.SetComponentState("commands", _commands);
                    }
                    catch { }

                    return cmd;
                }
            }
            catch { }

            return null;
        }

        // ✅ Remove expired commands safely
        private static void RemoveExpired(object? state)
        {
            try
            {
                if (_commands.IsEmpty) return;

                var now = DateTime.UtcNow;

                foreach (var item in _commands)
                {
                    try
                    {
                        if (now - item.Value.CreatedAt >= Expiry)
                        {
                            _commands.TryRemove(item.Key, out _);
                        }
                    }
                    catch { }
                }

                try
                {
                    _sessionService?.SetComponentState("commands", _commands);
                }
                catch { }
            }
            catch { }
        }
    }

    public record CommandInfo(
        ushort Id,
        string Remark,
        DateTime CreatedAt
    );
}