using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.ModPack;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Mods;

/// <summary>Asks for what the manifest of a new pack says about it.</summary>
public sealed class PackInfoWindow : Window
{
    private readonly TextBox _name = new() { Name = "PackName" };
    private readonly TextBox _version = new() { Name = "PackVersion", Text = "1.0.0" };
    private readonly TextBox _authors = new() { Name = "PackAuthors" };

    public PackInfoWindow(int changes, string suggestedName)
    {
        Title = Loc.Current["mods.pack.title"];
        Width = 480;
        MinWidth = 340;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _name.Text = suggestedName;
        var ok = DialogFrame.Action(this, Loc.Current["mods.pack.ok"], "PackOk", () => Close(Result()), DialogRole.Primary);
        DialogFrame.Show(this, new StackPanel
        {
            Spacing = ModsUi.Space(2),
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("mods.pack.info", ("count", changes)), TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = Loc.Current["mods.pack.name"], TextWrapping = TextWrapping.Wrap }, _name,
                new TextBlock { Text = Loc.Current["mods.pack.version"], TextWrapping = TextWrapping.Wrap }, _version,
                new TextBlock { Text = Loc.Current["mods.pack.authors"], TextWrapping = TextWrapping.Wrap }, _authors,
            },
        }, [ok, DialogFrame.Cancel(this, () => Close(null), "PackCancel")], scroll: DialogScroll.Vertical, dirty: DialogFrame.Snapshot(_name, _version, _authors));
    }

    private PackInfo? Result() => string.IsNullOrWhiteSpace(_name.Text) ? null : new PackInfo(_name.Text.Trim(), string.IsNullOrWhiteSpace(_version.Text) ? "1.0.0" : _version.Text.Trim(),
        [.. (_authors.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);

    /// <summary>The answer, or null when cancelled.</summary>
    public async Task<PackInfo?> AskAsync(Window owner) => await ShowDialog<PackInfo?>(owner);
}
