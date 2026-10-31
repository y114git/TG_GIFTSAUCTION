using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Saves the current layout under a name, and loads or deletes a saved one. Loading a layout moves the panels and keeps the open editors.</summary>
public sealed class LayoutsWindow : Window
{
    private readonly Func<IReadOnlyList<string>> _names;
    private readonly ListBox _list = new() { Name = "SavedLayouts", MinHeight = 140 };
    private readonly TextBox _name = new() { Name = "LayoutName", PlaceholderText = Loc.Current["layouts.name"] };
    private readonly Button _save = new() { Name = "LayoutSave", Content = Loc.Current["layouts.save"], IsEnabled = false };
    private readonly Button _load = new() { Name = "LayoutLoad", Content = Loc.Current["layouts.load"], IsEnabled = false };
    private readonly Button _delete = new() { Name = "LayoutDelete", Content = Loc.Current["layouts.delete"], IsEnabled = false };
    private readonly TextBlock _empty = new() { Name = "LayoutsEmpty", Text = Loc.Current["layouts.empty"], Classes = { "muted" } };

    public LayoutsWindow(Func<IReadOnlyList<string>> names, Action<string> save, Action<string> load, Action<string> delete)
    {
        _names = names;
        Title = Loc.Current["layouts.title"];
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        string? Picked() => _list.SelectedItem as string;
        void Update()
        {
            _save.IsEnabled = !string.IsNullOrWhiteSpace(_name.Text);
            _load.IsEnabled = _delete.IsEnabled = Picked() is not null;
        }

        void Run()
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) return;
            save(_name.Text.Trim());
            _name.Text = "";
            Refresh();
        }

        _save.Click += (_, _) => Run();
        _load.Click += (_, _) =>
        {
            if (Picked() is { } picked) load(picked);
        };
        _delete.Click += (_, _) =>
        {
            if (Picked() is not { } picked) return;
            delete(picked);
            Refresh();
        };
        _list.SelectionChanged += (_, _) => Update();
        _list.DoubleTapped += (_, _) =>
        {
            if (Picked() is { } picked) load(picked);
        };
        _name.TextChanged += (_, _) => Update();
        _name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Run();
            e.Handled = true;
        };
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 10,
            Children =
            {
                _empty,
                _list,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _load, _delete } },
                _name,
                _save,
            },
        };
        Refresh();
        Update();
    }

    private void Refresh()
    {
        var names = _names();
        _list.ItemsSource = names;
        _empty.IsVisible = names.Count == 0;
        _list.IsVisible = names.Count > 0;
    }
}
