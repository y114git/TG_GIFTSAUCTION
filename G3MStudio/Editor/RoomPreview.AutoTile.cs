using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>The auto-tile brush of the tile tools: a rule set chosen for the selected tile layer turns paint, erase, line, rectangle and fill into terrain edits.</summary>
public sealed partial class RoomPreview
{
    private readonly ComboBox _rules = new() { Name = "RoomAutoTile", MinWidth = 120 };
    private AutoTileStore? _autoTiles;

    private string? TileSetName => View.TileLayer?.TilesData?.Background is { } set ? ResourceTypes.NameOf(set) : null;

    /// <summary>The auto-tile rule sets of the data file; set once, right after the editor is made.</summary>
    public AutoTileStore? AutoTiles { get => _autoTiles; init => WireAutoTile(value); }

    private void WireAutoTile(AutoTileStore? store)
    {
        _autoTiles = store;
        ToolTip.SetTip(_rules, Loc.Current["roomEditor.autoTile"]);
        if (store is null) return;
        AttachedToVisualTree += (_, _) => store.Changed += KeepRules;
        DetachedFromVisualTree += (_, _) => store.Changed -= KeepRules;
    }

    /// <summary>The rule set the tile tools paint with, or null when the auto-tile brush is off.</summary>
    public AutoTileRuleSet? ActiveRules => _rules.SelectedIndex > 0 && TileSetName is { } set ? _autoTiles?.At(set, _rules.SelectedIndex - 1) : null;

    private void KeepRules() => RefreshRules(ActiveRules?.Name);

    /// <summary>Lists the rule sets of the layer's tile set and chooses <paramref name="choose"/> when it exists.</summary>
    private void RefreshRules(string? choose)
    {
        var sets = TileSetName is { } set ? _autoTiles?.For(set) ?? [] : [];
        _rules.ItemsSource = new[] { Loc.Current["roomEditor.autoOff"] }.Concat(sets.Select(r => r.Name)).ToList();
        _rules.SelectedIndex = choose is null ? 0 : sets.ToList().FindIndex(r => r.Name == choose) + 1;
    }

    /// <summary>Chooses a rule set of the layer's tile set by name; false when there is none of that name. Null turns the brush off.</summary>
    public bool ChooseRules(string? name)
    {
        var index = name is null || TileSetName is not { } set ? 0 : (_autoTiles?.For(set).ToList().FindIndex(r => r.Name == name) ?? -1) + 1;
        if (index == 0 && name is not null) return false;
        _rules.SelectedIndex = index;
        return true;
    }

    /// <summary>Switches the auto-tile brush off, or on with the first rule set of the tile set; false when it cannot be turned on.</summary>
    public bool ToggleAutoTile()
    {
        if (ActiveRules is not null) return ChooseRules(null);
        return TileSetName is { } set && _autoTiles?.At(set, 0) is { } first && ChooseRules(first.Name);
    }

    /// <summary>The cell changes of a tile stroke under the active rule set, or null when the stroke is not an auto-tile stroke.</summary>
    private List<(int X, int Y, uint Value)>? AutoChanges(GameMakerRoom.Layer layer, RoomTool tool, IReadOnlyList<(int X, int Y)> cells)
    {
        if (ActiveRules is not { } rules || _autoTiles is null || layer.TilesData is not { TileData: { } rows } || tool is not (RoomTool.Paint or RoomTool.Erase or RoomTool.Line or RoomTool.Rectangle or RoomTool.Fill)) return null;
        var outside = RoomOptions.AutoTile.OutsideIsTerrain;
        if (tool == RoomTool.Fill) return cells.Count == 0 ? [] : AutoTiler.Paint(rows, rules, _autoTiles.All, AutoTiler.FillArea(rows, rules, _autoTiles.All, cells[0].X, cells[0].Y), false, outside);
        return AutoTiler.Paint(rows, rules, _autoTiles.All, cells, tool == RoomTool.Erase, outside);
    }

    /// <summary>Retiles the terrain cells inside the tile selection with the active rule set (or the first one of the tile set); false without a selection or a rule set.</summary>
    public bool RetileSelection()
    {
        if (View.TileLayer is not { TilesData.TileData: { } rows } layer || View.TileSelection is not { } s || _autoTiles is null) return false;
        var rules = ActiveRules ?? (TileSetName is { } set ? _autoTiles.At(set, 0) : null);
        if (rules is null) return false;
        CommitTiles(layer, AutoTiler.Retile(rows, rules, _autoTiles.All, s.X, s.Y, s.Width, s.Height, RoomOptions.AutoTile.OutsideIsTerrain));
        return true;
    }
}
