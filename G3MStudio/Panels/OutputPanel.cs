using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Notifications;

namespace G3MStudio.Panels;

/// <summary>The Output panel: everything the app told the user, newest last, kept after the toast is gone.</summary>
public sealed class OutputPanel : UserControl
{
    private readonly ListBox _list = new() { Name = "OutputList" };
    private NotificationCenter? _center;
    private readonly TextBlock _empty = new() { Name = "OutputEmpty", Text = Localization.Loc.Current["empty.output"], Classes = { "muted" }, Margin = new Avalonia.Thickness(12), IsHitTestVisible = false };

    public OutputPanel()
    {
        _list.ItemTemplate = new FuncDataTemplate<Notification>((n, _) => new TextBlock { Text = n is null ? "" : $"{n.Time:HH:mm:ss} {n.Level}: {n.Text}", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Content = new Grid { Children = { _list, _empty } };
    }

    public IReadOnlyList<Notification> Rows => [.. _list.Items.OfType<Notification>()];

    public void Bind(NotificationCenter center)
    {
        _center = center;
        center.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        _list.ItemsSource = _center?.History.ToList() ?? [];
        _empty.IsVisible = _list.ItemCount == 0;
        _empty.Text = Localization.Loc.Current["empty.output"];
        if (_list.ItemCount > 0) _list.ScrollIntoView(_list.ItemCount - 1);
    }
}
