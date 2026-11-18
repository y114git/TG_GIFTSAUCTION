using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Notifications;

/// <summary>Bottom right stack of the active notifications, 360 px wide (design/shell.md section 8). Info and warning toasts expire.</summary>
public sealed class ToastHost : StackPanel
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);

    private NotificationCenter? _center;
    private bool _hovered;

    public ToastHost()
    {
        Spacing = 6;
        G3MStudio.Theme.Motion.Follow(this);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick(DateTimeOffset.Now);
        AttachedToVisualTree += (_, _) => timer.Start();
        DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    /// <summary>Expires old toasts, except while the pointer rests on one so it can be read.</summary>
    internal void Tick(DateTimeOffset now)
    {
        if (!_hovered) _center?.Expire(now, Lifetime);
    }

    public void Bind(NotificationCenter center)
    {
        _center = center;
        center.Changed += Show;
        Show();
    }

    private readonly Dictionary<long, Border> _toasts = [];

    // Toasts stay in place between calls: a new one fades in, a dismissed one fades out and the rest slide up.
    private void Show()
    {
        if (_center is null) return;
        var active = _center.Active.ToList();
        var live = active.Select(i => i.Id).ToHashSet();
        foreach (var (id, gone) in _toasts.Where(t => !live.Contains(t.Key)).ToList())
        {
            _toasts.Remove(id);
            _hovered = false;
            G3MStudio.Theme.Motion.Exit(gone, () => Children.Remove(gone));
        }

        foreach (var item in active.Where(i => !_toasts.ContainsKey(i.Id)))
        {
            var toast = Toast(item);
            _toasts[item.Id] = toast;
            Children.Add(toast);
            toast.AttachedToVisualTree += (_, _) => G3MStudio.Theme.Motion.Enter(toast, scale: true, fromX: 48);
        }
    }

    private Border Toast(Notification item)
    {
        var accent = (IBrush?)(Application.Current?.FindResource(item.Level switch { NotificationLevel.Error => "Danger", NotificationLevel.Warning => "Warning", _ => "Info" })) ?? Brushes.Gray;
        var close = new Button { Content = G3MStudio.Theme.GlyphIcons.Close(), Padding = new Thickness(6, 0), MinWidth = 24, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Tag = "close" }.Labeled("notify.close");
        close.Click += (_, _) => _center?.Dismiss(item.Id);
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        if (item.ActionLabel is { } label)
        {
            var action = new Button { Content = label, Tag = "action" };
            action.Click += (_, _) =>
            {
                item.Action?.Invoke();
                _center?.Dismiss(item.Id);
            };
            row.Children.Add(action);
        }

        // The border colour alone does not tell the level, so the word comes first.
        var level = Loc.Current[item.Level switch { NotificationLevel.Error => "notify.error", NotificationLevel.Warning => "notify.warning", _ => "notify.info" }];
        var body = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = level, Classes = { "muted" } }, new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap }, row } };
        var grid = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Avalonia.Controls.Dock.Right);
        grid.Children.Add(close);
        grid.Children.Add(body);
        var toast = new Border
        {
            Width = 360,
            Padding = new Thickness(10, 8),
            Background = (IBrush?)Application.Current?.FindResource("Bg.Raised"),
            BorderBrush = accent,
            BorderThickness = new Thickness(2),
            Child = grid,
            Tag = item.Level,
        };
        toast.PointerEntered += (_, _) => _hovered = true;
        toast.PointerExited += (_, _) => _hovered = false;
        Avalonia.Automation.AutomationProperties.SetLiveSetting(toast, Avalonia.Automation.AutomationLiveSetting.Polite);
        Avalonia.Automation.AutomationProperties.SetName(toast, level + ": " + item.Text);
        return toast;
    }
}
