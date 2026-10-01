using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;

namespace BatteryTestingSystem.Services;

public class SessionItem
{
    public object State { get; set; } = default!;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public bool Permanent { get; set; } = false;

}

public class ServerSessionStorageService : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SessionItem>> _sessionStorage  = new();

    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _expiry = TimeSpan.FromHours(1);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);
    private readonly Task _cleanupTask;

    public ServerSessionStorageService()
    {
        // 🔥 Start background cleanup thread
        _cleanupTask = Task.Run(CleanupLoop);
        LoadDbStorage();

    }

    public void LoadDbStorage()
    {
        using var scope = ServiceLocator.GetScoped<IConfigStorageService>();

        if (scope?.Service == null)
            return;

        var configs = scope.Service
            .GetConfigurationsAsync()
            .ConfigureAwait(false)   // important
            .GetAwaiter()
            .GetResult();

        // use configs here
        if (configs.Success)
        {
            foreach (var config in configs.Data ?? new())
            {
                try
                {
                    object? state = null;

                    if (!string.IsNullOrWhiteSpace(config.Value))
                    {
                        state = JsonConvert.DeserializeObject<object>(config.Value);
                    }

                    if (state != null)
                    {
                        SetDbComponentState(
                            config.Key,
                            state
                        );
                    }
                }
                catch
                {
                    // log error?
                    Log.Debug($"ServerSessionStorageService Failed to deserialize config for key: {config.Key}");
                }
            }

        }
    }

    public void SetDbComponentState(string storeId, object state)
    {

        string userId = CurrentUser.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            // log warning or throw exception
            Log.Warning("Attempted to set component state without a valid user ID.");
        }

        var userStorage = _sessionStorage.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, SessionItem>()
        );

        userStorage[storeId] = new SessionItem
        {
            State = state,
            LastUpdated = DateTime.Now,
            Permanent = true
        };

    }

    public void SetComponentState(string storeId, object state, bool Permanent = false)
    {
        string userId = CurrentUser.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            // log warning or throw exception
            Log.Warning("Attempted to set component state without a valid user ID.");
            return;
        }

        var userStorage = _sessionStorage.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, SessionItem>()
        );

        userStorage[storeId] = new SessionItem
        {
            State = state,
            LastUpdated = DateTime.Now,
            Permanent = Permanent
        };

        if (Permanent)
        {
            // If permanent, we might want to do something special here
            // For now, just a placeholder
            using var getDbStorage = ServiceLocator.GetScoped<IConfigStorageService>();

            if (getDbStorage != null && getDbStorage.Service != null)
            {
                getDbStorage.Service.SaveConfigurationAsync(
                    new Models.Entities.ConfigurationEntity() 
                    {
                        Key = storeId,
                        Value = JsonConvert.SerializeObject(state),
                        CreatedAt = DateTime.Now,
                        CreatedBy = CurrentUser.UserName,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = CurrentUser.UserName
                    });
            }

        }
    }

    public T? GetComponentState<T>(string storeId) where T : class
    {
        string userId = CurrentUser.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            // log warning or throw exception
            Log.Warning("Attempted to set component state without a valid user ID.");
            return null;
        }

        if (_sessionStorage.TryGetValue(userId, out var userStorage) &&
            userStorage.TryGetValue(storeId, out var item))
        {
            // 🔄 touch on read
            item.LastUpdated = DateTime.Now;
           
          return GetState<T>(item.State);
        
        }

        return null;
    }

    public static T? GetState<T>(object? stateObj)
    {
        if (stateObj == null)
            return default;

        if (stateObj is T t)
            return t;

        try
        {
            // 1️⃣ Convert object to JSON string
            string json = JsonConvert.SerializeObject(stateObj);

            // 2️⃣ Deserialize JSON into the target type
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            // Return null if serialization/deserialization fails
            return default;
        }
    }

    public void RemoveComponentState(string storeId)
    {
        string userId = CurrentUser.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            // log warning or throw exception
            Log.Warning("Attempted to set component state without a valid user ID.");
        }

        if (_sessionStorage.TryGetValue(userId, out var userStorage))
        {
            userStorage.TryRemove(storeId, out _);

            // optional: remove user bucket if empty
            if (userStorage.IsEmpty)
                _sessionStorage.TryRemove(CurrentUser.UserId, out _);
        }
    }

    // ================= CLEANUP THREAD =================

    private async Task CleanupLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                CleanupExpiredSessions();
                await Task.Delay(_cleanupInterval, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                // TODO: log error
            }
        }
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.Now;

        foreach (var user in _sessionStorage)
        {
            foreach (var store in user.Value)
            {
                if (now - store.Value.LastUpdated > _expiry && !store.Value.Permanent)
                {
                    user.Value.TryRemove(store.Key, out _);
                }
            }

            // remove empty user bucket
            if (user.Value.IsEmpty)
                _sessionStorage.TryRemove(user.Key, out _);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cleanupTask.Wait();
        _cts.Dispose();
    }
}