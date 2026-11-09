using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Asks for a picture, a tile set and a layer name, and shows how many cells of the picture match a tile of the set before anything is made.
/// Cells with pixels that match no tile are listed (the first few) and stay empty.
/// </summary>
public sealed class ImageToTilesWindow : Window
{
    private readonly GameMakerRoom _room;
    private readonly IReadOnlyList<GameMakerBackground> _sets;
    private readonly Func<Task<string?>> _pick;
    private readonly Func<IDisposable>? _read;
    private readonly TextBlock _path = new() { Name = "TilesPath", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, MaxWidth = 320, VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _set = new() { Name = "TilesSet", MinWidth = 220 };
    private readonly TextBox _name = new() { Name = "TilesName", MinWidth = 220 };
    private readonly TextBlock _summary = new() { Name = "TilesSummary", TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440 };
    private readonly Button _ok = new() { Name = "TilesOk", IsEnabled = false, Classes = { "primary" } };
    private PixelBuffer? _picture;

    public ImageToTilesWindow(GameMakerRoom room, IReadOnlyList<GameMakerBackground> sets, Func<Task<string?>> pick, Func<IDisposable>? read = null)
    {
        (_room, _sets, _pick, _read) = (room, sets, pick, read);
        Title = Loc.Current["rooms.imgTitle"];
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _set.ItemsSource = sets.Select(s => s.Name.Content ?? "").ToList();
        _set.SelectedIndex = 0;
        _name.Text = ImageToTiles.FreeLayerName(room, "Tiles");
        _path.Text = Loc.Current["rooms.imgNone"];
        _ok.Content = Loc.Current["image.ok"];
        var choose = new Button { Name = "TilesChoose", Content = Loc.Current["rooms.imgChoose"] };
        var cancel = new Button { Name = "TilesCancel", Content = Loc.Current["explorer.cancel"] };
        choose.Click += async (_, _) =>
        {
            if (await _pick() is { } file) Load(file);
        };
        _set.SelectionChanged += (_, _) => Refresh();
        _name.TextChanged += (_, _) => UpdateOk();
        _ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);
        Content = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(12),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { choose, _path } },
                Row("rooms.imgTileSet", _set),
                Row("rooms.imgLayer", _name),
                _summary,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, _ok } },
            },
        };
    }

    /// <summary>The match for the current choice, or null before a picture is loaded.</summary>
    public TileMatch? Match { get; private set; }

    public string TileSet => _set.SelectedItem as string ?? "";

    public string LayerName => (_name.Text ?? "").Trim();

    private static StackPanel Row(string label, Control control) =>
        new() { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Loc.Current[label], Width = 110, VerticalAlignment = VerticalAlignment.Center }, control } };

    /// <summary>Reads the picture file and shows the match; a file that cannot be read shows the reason and keeps OK off.</summary>
    public void Load(string file)
    {
        try
        {
            _picture = SheetSplit.Read(File.ReadAllBytes(file))[0];
            _path.Text = Path.GetFileName(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ImageMagick.MagickException)
        {
            _picture = null;
            Match = null;
            _summary.Text = Loc.Current.Format("sprites.badPicture", ("name", Path.GetFileName(file)), ("reason", e.Message));
            _ok.IsEnabled = false;
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        Match = null;
        if (_picture is null || _set.SelectedIndex < 0) return;
        try
        {
            using (_read?.Invoke()) Match = ImageToTiles.Match(_room, _sets[_set.SelectedIndex], _picture);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ImageMagick.MagickException)
        {
            _summary.Text = e.Message;
            _ok.IsEnabled = false;
            return;
        }

        _summary.Text = Summary(Match);
        UpdateOk();
    }

    // The layer name changes on every keystroke; the match only when the picture or the tile set changes.
    private void UpdateOk() => _ok.IsEnabled = Match is not null && LayerName.Length > 0 && !(_room.Layers ?? []).Any(l => l.LayerName.Content == LayerName);

    /// <summary>The counts and the first unmatched cells, as the dialog and the notification show them.</summary>
    public static string Summary(TileMatch match) =>
        Loc.Current.Format("rooms.imgSummary", ("matched", match.Matched), ("unmatched", match.Unmatched.Count), ("first", First(match))) + (match.Cropped ? " " + Loc.Current["rooms.imgCropped"] : "");

    /// <summary>"(3,4) (5,6)" for the first cells that matched nothing, or nothing.</summary>
    public static string First(TileMatch match) => match.Unmatched.Count == 0 ? "" : " " + string.Join(' ', match.Unmatched.Take(8).Select(c => $"({c.X},{c.Y})")) + (match.Unmatched.Count > 8 ? " ..." : "");

    public async Task<bool> AskAsync(Window owner) => await ShowDialog<bool?>(owner) ?? false;
}
