using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Input;
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

    private readonly TextBlock _preview = new TextBlock { Name = "Preview", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = (Avalonia.Media.IBrush?)Avalonia.Application.Current?.FindResource("Text.Muted") }.TextSize("FontSize.Small");
    private int _previewVersion;

    // preview (optional) says what the typed name would change; it runs off the UI thread a moment after typing stops, and a null result clears the text.
    public RenameWindow(string current, Func<string, string?> apply, string? title = null, string? label = null, string? okText = null, Func<string, string?>? preview = null)
    {
        Title = title ?? Loc.Current["rename.title"];
        Width = 400;
        MinWidth = 320;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _name.Text = current;
        _error.Foreground = (Avalonia.Media.IBrush?)Avalonia.Application.Current?.FindResource("Danger");
        var ok = DialogFrame.Action(this, okText ?? Loc.Current["rename.ok"], "Rename", Run, DialogRole.Primary);
        ok.IsEnabled = false;
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
            _preview.Text = "";
            var version = ++_previewVersion; // also for an unchanged name, so a preview still running for the last text cannot show up late
            if (preview is null || name.Length == 0 || name == current) return;
            _ = ShowPreviewAsync(preview, name, version);
        };
        _name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            DialogFrame.Commit(this, Run);
            e.Handled = true;
        };
        Opened += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        DialogFrame.Show(this, new StackPanel
        {
            Spacing = Mods.ModsUi.Space(2),
            Children = { new TextBlock { Text = label ?? Loc.Current.Format("rename.label", ("name", current)), TextWrapping = Avalonia.Media.TextWrapping.Wrap }, _name, _error.HideWhenEmpty(), _preview.HideWhenEmpty() },
        }, [ok, DialogFrame.Cancel(this, () => Close(false))], scroll: DialogScroll.Vertical, dirty: () => (_name.Text?.Trim() ?? "") != current);
    }


    private async Task ShowPreviewAsync(Func<string, string?> preview, string name, int version)
    {
        await Task.Delay(250);
        if (version != _previewVersion) return;
        var text = await Task.Run(() => preview(name));
        if (version == _previewVersion) _preview.Text = text ?? "";
    }

    /// <summary>True when the rename was applied.</summary>
    public async Task<bool> AskAsync(Window owner) => await ShowDialog<bool?>(owner) ?? false;
}
