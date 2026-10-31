namespace G3MStudio.Core.Notifications;

public enum NotificationLevel { Info, Warning, Error }

/// <summary>One message to the user (design/shell.md section 8). An action, when given, runs from the toast button.</summary>
public sealed record Notification(long Id, NotificationLevel Level, string Text, DateTimeOffset Time, string? ActionLabel = null, Action? Action = null)
{
    /// <summary>Errors stay until dismissed; the rest expire.</summary>
    public bool Sticky => Level == NotificationLevel.Error;
}

/// <summary>The toasts that are showing and a short history of everything posted.</summary>
public sealed class NotificationCenter
{
    public const int MaxHistory = 100;

    private readonly List<Notification> _active = [];
    private readonly List<Notification> _history = [];
    private long _next;

    public IReadOnlyList<Notification> Active => _active;

    public IReadOnlyList<Notification> History => _history;

    /// <summary>Posted since the last <see cref="MarkRead"/>.</summary>
    public int Unread { get; private set; }

    public event Action? Changed;

    public Notification Post(NotificationLevel level, string text, string? actionLabel = null, Action? action = null, DateTimeOffset? now = null)
    {
        var item = new Notification(++_next, level, text, now ?? DateTimeOffset.Now, actionLabel, action);
        _active.Add(item);
        _history.Add(item);
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        Unread++;
        Changed?.Invoke();
        return item;
    }

    public void Dismiss(long id)
    {
        if (_active.RemoveAll(n => n.Id == id) > 0) Changed?.Invoke();
    }

    public void DismissAll()
    {
        if (_active.Count == 0 && Unread == 0) return;
        _active.Clear();
        Unread = 0;
        Changed?.Invoke();
    }

    /// <summary>Removes the toasts that are not sticky and older than <paramref name="lifetime"/>.</summary>
    public void Expire(DateTimeOffset now, TimeSpan lifetime)
    {
        if (_active.RemoveAll(n => !n.Sticky && now - n.Time >= lifetime) > 0) Changed?.Invoke();
    }

    public void MarkRead()
    {
        Unread = 0;
        Changed?.Invoke();
    }
}
