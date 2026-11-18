using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Notifications;
using G3MStudio.Localization;

namespace G3MStudio.Notifications;

/// <summary>The list behind the Notifications button of the status bar: every notification with its level, time and message, newest first, each with Dismiss, and Dismiss all.</summary>
public sealed class NotificationList : Border
{
    private readonly NotificationCenter _center;
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public NotificationList(NotificationCenter center)
    {
        _center = center;
        Focusable = true;
        Width = 420;
        MaxHeight = 400;
        Padding = new Thickness(8);
        Child = new ScrollViewer { Content = _rows };
        Refresh();
        // Subscribed only while shown: a list that is built and never attached must not stay rooted by the long-lived center.
        AttachedToVisualTree += (_, _) =>
        {
            center.Changed += Refresh;
            Refresh();
            G3MStudio.Theme.Motion.Enter(this, scale: true);
        };
        DetachedFromVisualTree += (_, _) => center.Changed -= Refresh;
    }

    private void Refresh()
    {
        _rows.Children.Clear();
        var loc = Loc.Current;
        if (_center.History.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = loc["notify.empty"], Classes = { "muted" } });
            return;
        }

        var all = new Button { Content = loc["notify.dismissAll"], HorizontalAlignment = HorizontalAlignment.Right, Tag = "dismissAll" };
        all.Click += (_, _) => _center.Clear();
        _rows.Children.Add(all);
        var at = 0;
        foreach (var item in _center.History.Reverse()) _rows.Children.Add(Row(item, loc, at++));
    }

    private DockPanel Row(Notification item, Localizer loc, int index)
    {
        var level = loc[item.Level switch { NotificationLevel.Error => "notify.error", NotificationLevel.Warning => "notify.warning", _ => "notify.info" }];
        var dismiss = new Button { Content = loc["notify.dismiss"], VerticalAlignment = VerticalAlignment.Top, Tag = "dismiss" };
        dismiss.Click += (_, _) =>
        {
            _center.Remove(item.Id);
            // The rows were rebuilt, so the focused button is gone: move focus to the next Dismiss, or to the list when none is left.
            var next = _rows.Children.OfType<DockPanel>().Skip(index).Select(r => r.Children.OfType<Button>().FirstOrDefault()).FirstOrDefault()
                ?? _rows.Children.OfType<DockPanel>().Select(r => r.Children.OfType<Button>().FirstOrDefault()).LastOrDefault();
            (next as Control ?? this).Focus();
        };
        Avalonia.Automation.AutomationProperties.SetName(dismiss, loc["notify.close"]);
        var head = new TextBlock { Text = level + "  " + item.Time.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture), Classes = { "muted" } };
        var body = new StackPanel { Spacing = 2, Children = { head, new TextBlock { Text = item.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap } } };
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(dismiss, Avalonia.Controls.Dock.Right);
        row.Children.Add(dismiss);
        row.Children.Add(body);
        return row;
    }
}
