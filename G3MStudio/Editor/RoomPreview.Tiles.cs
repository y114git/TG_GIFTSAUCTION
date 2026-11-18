using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

public sealed partial class RoomPreview
{
    /// <summary>Shows the tile set of the chosen layer as the palette and makes it the layer that painting changes; other layers leave painting off.</summary>
    /// <summary>The mirror (1), flip (2) and quarter turn (4) flags put on painted tiles; 0 without a tile layer.</summary>
    public int TileFlags => _palette?.Flags ?? 0;

    public void ChooseTileLayer(GameMakerRoom.Layer? layer)
    {
        View.TileLayer = layer?.TilesData?.Background is not null ? layer : null;
        _palette = layer?.TilesData?.Background is { } set ? new TilePalette(set, _textures) : null;
        _paletteHost.Child = _palette is null ? null : new StackPanel
        {
            Spacing = 4,
            Children = { FlagBox(_palette, 1, "rooms.flagMirror"), FlagBox(_palette, 2, "rooms.flagFlip"), FlagBox(_palette, 4, "rooms.flagRotate"), _palette },
        };
        _paletteHost.IsVisible = _palette is not null;
        _tileBar.IsVisible = View.TileLayer is not null;
        RefreshRules(RoomOptions.AutoTile.StartOn && TileSetName is { } name ? _autoTiles?.At(name, 0)?.Name : null);
    }

    private static CheckBox FlagBox(TilePalette palette, int bit, string key)
    {
        var box = new CheckBox { Content = Loc.Current[key] };
        box.IsCheckedChanged += (_, _) => palette.Flags = box.IsChecked == true ? palette.Flags | bit : palette.Flags & ~bit;
        palette.FlagsChanged += () => box.IsChecked = (palette.Flags & bit) != 0;
        return box;
    }

    /// <summary>Keys of the tile editor: B brush, E erase, G fill, R rectangle, P pick; X, Y and Z turn the mirror, flip and quarter turn flags of the tiles painted on or off.</summary>
    private bool TileKey(Avalonia.Input.Key key)
    {
        if (_palette is null) return false;
        RoomTool? tool = key switch { Avalonia.Input.Key.B => RoomTool.Paint, Avalonia.Input.Key.E => RoomTool.Erase, Avalonia.Input.Key.G => RoomTool.Fill, Avalonia.Input.Key.R => RoomTool.Rectangle, Avalonia.Input.Key.P => RoomTool.Pick, Avalonia.Input.Key.L => RoomTool.Line, Avalonia.Input.Key.S => RoomTool.Select, _ => null };
        if (tool is { } chosen)
        {
            _tool.SelectedIndex = (int)chosen;
            return true;
        }

        var bit = key switch { Avalonia.Input.Key.X => 1, Avalonia.Input.Key.Y => 2, Avalonia.Input.Key.Z => 4, _ => 0 };
        if (bit == 0) return false;
        _palette.Flags ^= bit;
        return true;
    }
}
