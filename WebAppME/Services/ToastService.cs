namespace BatteryTestingSystem.Services;

/// <summary>
/// Toast notification service for Blazor Server - Pure C#, no JavaScript required
/// </summary>
public class ToastService
{
    private readonly List<ToastMessage> _toasts = new();
    private readonly object _lock = new();

    public IReadOnlyList<ToastMessage> Toasts => _toasts.AsReadOnly();

    public event Action? OnChange;

    public void Show(string title,string? description = null, ToastVariant variant = ToastVariant.Default,int durationMs = 5000)
    {
        var toast = new ToastMessage
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            Variant = variant,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            _toasts.Add(toast);
        }

        OnChange?.Invoke();

        if (durationMs > 0)
        {
            _ = AutoRemoveAsync(toast.Id, durationMs);
        }
    }

    public void Success(string title, string? description = null, int durationMs = 5000)
        => Show(title, description, ToastVariant.Success, durationMs);

    public void Warning(string title, string? description = null, int durationMs = 5000)
        => Show(title, description, ToastVariant.Warning, durationMs);

    public void Error(string title, string? description = null, int durationMs = 5000)
        => Show(title, description, ToastVariant.Destructive, durationMs);

    public void Info(string title, string? description = null, int durationMs = 5000)
        => Show(title, description, ToastVariant.Default, durationMs);

    public void Remove(string id)
    {
        lock (_lock)
        {
            var toast = _toasts.FirstOrDefault(t => t.Id == id);
            if (toast == null) return;

            _toasts.Remove(toast);
        }

        OnChange?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _toasts.Clear();
        }

        OnChange?.Invoke();
    }

    private async Task AutoRemoveAsync(string id, int durationMs)
    {
        await Task.Delay(durationMs);
        Remove(id);
    }
}

/// <summary>
/// Toast message model
/// </summary>
public class ToastMessage
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ToastVariant Variant { get; set; } = ToastVariant.Default;
    public DateTime CreatedAt { get; set; }
}

public enum ToastVariant
{
    Default,
    Success,
    Warning,
    Destructive
}
