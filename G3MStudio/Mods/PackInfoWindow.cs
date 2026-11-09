using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.ModPack;
using G3MStudio.Localization;

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
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _name.Text = suggestedName;
        var ok = new Button { Name = "PackOk", IsDefault = true, Content = Loc.Current["mods.pack.ok"] };
        var cancel = new Button { Name = "PackCancel", IsCancel = true, Content = Loc.Current["explorer.cancel"] };
        ok.Click += (_, _) => Close(Result());
        cancel.Click += (_, _) => Close(null);
        Content = new StackPanel
        {
            Margin = ModsUi.Pad(4),
            Spacing = ModsUi.Space(2),
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("mods.pack.info", ("count", changes)), TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = Loc.Current["mods.pack.name"] }, _name,
                new TextBlock { Text = Loc.Current["mods.pack.version"] }, _version,
                new TextBlock { Text = Loc.Current["mods.pack.authors"] }, _authors,
                ModsUi.Buttons(ok, cancel),
            },
        };
    }

    private PackInfo? Result() => string.IsNullOrWhiteSpace(_name.Text) ? null : new PackInfo(_name.Text.Trim(), string.IsNullOrWhiteSpace(_version.Text) ? "1.0.0" : _version.Text.Trim(),
        [.. (_authors.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);

    /// <summary>The answer, or null when cancelled.</summary>
    public async Task<PackInfo?> AskAsync(Window owner) => await ShowDialog<PackInfo?>(owner);
}
