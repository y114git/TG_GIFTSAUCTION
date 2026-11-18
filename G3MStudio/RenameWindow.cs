using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Input;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// What a rename dialog adds to the name field. Preview says what the typed name would change; it runs off the UI thread a moment after typing stops,
/// and a null result clears the text. Nickname adds the Nickname field under the name: the text it starts with, and what to do with the new one once the
/// name is settled (an empty text clears it). CheckNickname takes the settled name and the new nickname and returns why the nickname is refused, or null; it runs before the rename.
/// RenameStrings adds the "Also rename matching strings" box, checked as given; SetRenameStrings gets the box's state when the dialog is accepted.
/// </summary>
public sealed record RenameOptions(Func<string, string?>? Preview = null, string? Nickname = null, Action<string, string>? SetNickname = null, Func<string, string, string?>? CheckNickname = null, bool? RenameStrings = null, Action<bool>? SetRenameStrings = null);

/// <summary>
/// Rename dialog (F2 on a resource). the apply callback runs the rename and returns the reason it was refused, or null when done;
/// a refusal stays in the dialog next to the field so the name can be fixed.
/// </summary>
public sealed class RenameWindow : Window
{
    private bool? _strings;
    private readonly TextBox _name = new() { Name = "NewName" };
    private readonly TextBlock _error = new() { Name = "Error", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    private readonly TextBlock _preview = new TextBlock { Name = "Preview", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = (Avalonia.Media.IBrush?)Avalonia.Application.Current?.FindResource("Text.Muted") }.TextSize("FontSize.Small");
    private readonly TextBox _nickname = new() { Name = "Nickname", MaxLength = Nicknames.MaxLength * 2 };
    private int _previewVersion;

    public RenameWindow(string current, Func<string, string?> apply, string? title = null, string? label = null, string? okText = null, RenameOptions? options = null)
    {
        var preview = options?.Preview;
        var setNickname = options?.SetNickname;
        var hasNickname = setNickname is not null;
        var nickname = Nicknames.Clean(options?.Nickname);
        _nickname.Text = nickname;
        _nickname.PlaceholderText = Loc.Current["rename.nicknameHint"];
        Title = title ?? Loc.Current["rename.title"];
        Width = 460;
        MinWidth = 320;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _name.Text = current;
        _error.Foreground = (Avalonia.Media.IBrush?)Avalonia.Application.Current?.FindResource("Danger");
        var ok = DialogFrame.Action(this, okText ?? Loc.Current["rename.ok"], "Rename", Run, DialogRole.Primary);
        ok.IsEnabled = false;
        string Nick() => hasNickname ? Nicknames.Clean(_nickname.Text) : "";
        bool Changed(string name) => name.Length > 0 && (name != current || Nick() != nickname);
        void Run()
        {
            var name = _name.Text?.Trim() ?? "";
            if (!Changed(name)) return;
            if (hasNickname && Nick() != nickname && options?.CheckNickname?.Invoke(name, Nick()) is { } taken)
            {
                _error.Text = taken;
                G3MStudio.Theme.Motion.Shake(_nickname);
                return;
            }

            // The name is applied first: a refused name leaves the nickname as it was, and the nickname then belongs to the new name.
            if (_strings is { } keep) options?.SetRenameStrings?.Invoke(keep); // remembered only when the dialog is accepted, and before the rename reads it
            var refused = name == current ? null : apply(name);
            _error.Text = refused ?? "";
            if (refused is not null)
            {
                G3MStudio.Theme.Motion.Shake(_name);
                return;
            }

            if (hasNickname && Nick() != nickname) setNickname?.Invoke(name, Nick());
            Close(true);
        }

        _nickname.TextChanged += (_, _) => (ok.IsEnabled, _error.Text) = (Changed(_name.Text?.Trim() ?? ""), "");
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            var name = _name.Text?.Trim() ?? "";
            ok.IsEnabled = Changed(name);
            _error.Text = "";
            _preview.Text = "";
            var version = ++_previewVersion; // also for an unchanged name, so a preview still running for the last text cannot show up late
            if (preview is null || name.Length == 0 || name == current) return;
            _ = ShowPreviewAsync(preview, name, version);
        };
        void OnEnter(object? _, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            DialogFrame.Commit(this, Run);
            e.Handled = true;
        }

        _name.KeyDown += OnEnter;
        _nickname.KeyDown += OnEnter;
        Opened += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        var fields = new StackPanel
        {
            Spacing = Mods.ModsUi.Space(2),
            Children = { new TextBlock { Text = label ?? Loc.Current.Format("rename.label", ("name", current)), TextWrapping = Avalonia.Media.TextWrapping.Wrap }, _name },
        };
        // The nickname sits right under the name; the error and the change text come after both fields.
        if (hasNickname) fields.Children.AddRange([new TextBlock { Text = Loc.Current["rename.nickname"], TextWrapping = Avalonia.Media.TextWrapping.Wrap }, _nickname]);
        if (options?.RenameStrings is { } renameStrings) fields.Children.Add(StringsBox(renameStrings));
        fields.Children.Add(_error.HideWhenEmpty());
        fields.Children.Add(_preview.HideWhenEmpty());
        DialogFrame.Show(this, fields, [ok, DialogFrame.Cancel(this, () => Close(false))], scroll: DialogScroll.Vertical, dirty: () => (_name.Text?.Trim() ?? "") != current || Nick() != nickname);
    }


    private CheckBox StringsBox(bool isChecked)
    {
        var strings = new CheckBox { Name = "RenameStrings", Content = Loc.Current["rename.strings"], IsChecked = isChecked };
        _strings = isChecked;
        strings.IsCheckedChanged += (_, _) => _strings = strings.IsChecked == true;
        return strings;
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
