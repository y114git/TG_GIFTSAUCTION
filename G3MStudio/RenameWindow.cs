using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Rename dialog (F2 on a resource). the apply callback runs the rename and returns the reason it was refused, or null when done;
/// a refusal stays in the dialog next to the field so the name can be fixed.
/// </summary>
public sealed class RenameWindow : Window
{
    private readonly TextBox _name = new() { Name = "NewName" };
    private readonly TextBlock _error = new() { Name = "Error", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public RenameWindow(string current, Func<string, string?> apply)
    {
        Title = Loc.Current["rename.title"];
        Width = 400;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _name.Text = current;
        _error.Foreground = (Avalonia.Media.IBrush?)Avalonia.Application.Current?.FindResource("Danger");
        var ok = new Button { Name = "Rename", Content = Loc.Current["rename.ok"], IsEnabled = false };
        var cancel = new Button { Name = "Cancel", Content = Loc.Current["explorer.cancel"] };
        void Run()
        {
            var name = _name.Text?.Trim() ?? "";
            if (name == current || name.Length == 0) return;
            var refused = apply(name);
            _error.Text = refused ?? "";
            if (refused is null) Close(true);
        }

        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            var name = _name.Text?.Trim() ?? "";
            ok.IsEnabled = name.Length > 0 && name != current;
            _error.Text = "";
        };
        ok.Click += (_, _) => Run();
        cancel.Click += (_, _) => Close(false);
        _name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Run();
            e.Handled = true;
        };
        Opened += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("rename.label", ("name", current)) },
                _name,
                _error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
            },
        };
    }

    /// <summary>True when the rename was applied.</summary>
    public async Task<bool> AskAsync(Window owner) => await ShowDialog<bool?>(owner) ?? false;
}
