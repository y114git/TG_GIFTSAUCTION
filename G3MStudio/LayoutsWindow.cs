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
    private readonly Button _rename = new() { Name = "LayoutRename", Content = Loc.Current["layouts.rename"], IsEnabled = false };
    private readonly Button _delete = new() { Name = "LayoutDelete", Content = Loc.Current["layouts.delete"], IsEnabled = false };
    private readonly TextBlock _empty = new() { Name = "LayoutsEmpty", Text = Loc.Current["layouts.empty"], Classes = { "muted" } };

    /// <param name="names">The saved names.</param>
    /// <param name="save">Saves under a name; returns the reason it was refused, or null.</param>
    /// <param name="rename">Gives a saved layout another name; returns the reason it was refused, or null.</param>
    /// <param name="load">Puts a saved layout in place.</param>
    /// <param name="delete">Removes a saved layout.</param>
    public LayoutsWindow(Func<IReadOnlyList<string>> names, Func<string, string?> save, Action<string> load, Action<string> delete, Func<string, string, string?> rename)
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
            _load.IsEnabled = _delete.IsEnabled = _rename.IsEnabled = Picked() is not null;
        }

        void Run()
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) return;
            if (save(_name.Text.Trim()) is not null) return;
            _name.Text = "";
            Refresh();
        }

        _save.Click += (_, _) => Run();
        _load.Click += (_, _) =>
        {
            if (Picked() is { } picked) load(picked);
        };
        _rename.Click += async (_, _) =>
        {
            if (Picked() is not { } picked) return;
            await new RenameWindow(picked, to => rename(picked, to), Loc.Current["layouts.rename"], Loc.Current["layouts.name"]).AskAsync(this);
            Refresh();
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
            Spacing = Mods.ModsUi.Space(3),
            Children =
            {
                _empty,
                _list,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _load, _rename, _delete } },
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
