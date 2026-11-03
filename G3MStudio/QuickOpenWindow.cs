using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>How a chosen resource opens: Enter previews it, Ctrl+Enter opens a tab, Alt+Enter opens a tab in a split to the side (design/shell.md 3.3).</summary>
public enum QuickOpenMode { Preview, Tab, Side }

/// <summary>Go to Resource (Ctrl+P): a box over the resource and code names of the open data file.</summary>
public sealed class QuickOpenWindow : Window
{
    private readonly Func<string, IReadOnlyList<QuickOpenItem>> _search;
    private readonly TextBox _input = new() { Name = "Input", PlaceholderText = Loc.Current["quick.placeholder"] };
    private readonly ListBox _results = new() { Name = "Results", MaxHeight = 320 };
    private readonly TextBlock _empty = new() { Name = "Empty", Classes = { "muted" }, IsVisible = false };

    public QuickOpenWindow(Func<string, IReadOnlyList<QuickOpenItem>> search)
    {
        _search = search;
        Title = Loc.Current["quick.title"];
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _results.ItemTemplate = new FuncDataTemplate<QuickOpenItem>((item, _) => item is null ? new TextBlock() : new DockPanel
        {
            Children =
            {
                new TextBlock { Text = Loc.Current["explorer.type." + item.Type], Classes = { "muted" }, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right },
                new TextBlock { Text = item.Name },
            },
        });
        Content = new StackPanel { Margin = new Avalonia.Thickness(8), Spacing = 6, Children = { _input, _results, _empty } };
        _input.TextChanged += (_, _) => Refresh();
        _input.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _results.DoubleTapped += (_, _) => Choose(QuickOpenMode.Tab);
        Opened += (_, _) => _input.Focus();
        Refresh();
    }

    public IReadOnlyList<QuickOpenItem> Items => [.. _results.Items.OfType<QuickOpenItem>()];

    public event Action<QuickOpenItem, QuickOpenMode>? Chosen;

    private void Refresh()
    {
        var text = _input.Text ?? "";
        var items = _search(text);
        _results.ItemsSource = items;
        _results.SelectedIndex = items.Count > 0 ? 0 : -1;
        _empty.IsVisible = items.Count == 0 && text.Trim().Length > 0;
        _empty.Text = Loc.Current.Format("palette.empty", ("text", text));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Move(1); break;
            case Key.Up: Move(-1); break;
            case Key.Enter: Choose(e.KeyModifiers.HasFlag(KeyModifiers.Alt) ? QuickOpenMode.Side : e.KeyModifiers.HasFlag(KeyModifiers.Control) ? QuickOpenMode.Tab : QuickOpenMode.Preview); break;
            case Key.Escape: Close(); break;
            default: return;
        }

        e.Handled = true;
    }

    private void Move(int step)
    {
        var count = _results.ItemCount;
        if (count > 0) _results.SelectedIndex = (_results.SelectedIndex + step + count) % count;
    }

    private void Choose(QuickOpenMode mode)
    {
        if (_results.SelectedItem is not QuickOpenItem item) return;
        Close();
        Chosen?.Invoke(item, mode);
    }
}
