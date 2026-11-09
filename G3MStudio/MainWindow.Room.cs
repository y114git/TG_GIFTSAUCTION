using G3MStudio.Editor;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Commands of the room editor that work on the room tab in front.</summary>
public sealed partial class MainWindow
{
    private (Workspace Workspace, GameMakerRoom Room)? FrontRoom()
    {
        if (Workspaces.Current is { } workspace && Docking.ActiveTabId is { } id && _roomViews.ContainsKey(id)
            && Docking.Tabs.Get(id)?.Key is { } key && ResourceTypes.SplitKey(key) is var (_, name)
            && workspace.Data.Rooms.FirstOrDefault(r => r.Name.Content == name) is { } room)
            return (workspace, room);
        Notify(NotificationLevel.Info, Loc.Current["rooms.noRoom"]);
        return null;
    }

    private readonly Dictionary<string, AutoTileStore> _autoTiles = [];

    /// <summary>The auto-tile rule sets of a data file, one shared store per file so the room editor and the tile set editor see each other's changes.</summary>
    private AutoTileStore AutoTilesOf(Workspace workspace)
    {
        if (_autoTiles.TryGetValue(workspace.Path, out var store)) return store;
        _autoTiles[workspace.Path] = store = new AutoTileStore(Workspaces.UserData, workspace.Path);
        store.Failed += e => Notify(NotificationLevel.Warning, e.Message);
        return store;
    }

    private RoomPreview? FrontRoomPreview() => Docking.ActiveTabId is { } id && _roomViews.TryGetValue(id, out var room) ? room : null;

    public void ToggleAutoTile()
    {
        if (FrontRoomPreview() is not { } room) return;
        if (!room.ToggleAutoTile() && room.ActiveRules is null) Notify(NotificationLevel.Info, Loc.Current[room.View.TileLayer is null ? "rooms.noTileLayer" : "roomEditor.autoNone"]);
    }

    public void RetileSelection()
    {
        if (FrontRoomPreview() is not { } room) return;
        if (!room.RetileSelection()) Notify(NotificationLevel.Info, Loc.Current["roomEditor.retileNone"]);
    }

    public void ShowCreationCode() => _ = EditCreationCodeAsync(0);

    /// <summary>Opens the creation code of the room in front (instance 0) or of one of its instances. A missing entry is created after a warning the owner may overrule.</summary>
    private async Task EditCreationCodeAsync(uint instanceId)
    {
        if (FrontRoom() is not var (workspace, room)) return;
        if (RoomCreationCodeCommand.CodeOf(room, instanceId) is null)
        {
            var refusal = new RiskyRefusalException("roomEditor.codeWarnTitle", "roomEditor.codeWarn", Loc.Current["roomEditor.noCodeReason"]);
            if (!await AskRiskyAsync(refusal, instanceId == 0 ? room.Name.Content ?? "" : $"inst_{instanceId:X8}")) return;
            try
            {
                workspace.Edit(new RoomCreationCodeCommand(room.Name.Content ?? "", instanceId));
            }
            catch (InvalidOperationException e)
            {
                Notify(NotificationLevel.Error, e.Message);
                return;
            }
        }

        if (RoomCreationCodeCommand.CodeOf(room, instanceId)?.Name.Content is { } code) OpenCodeAt(code, 1, 1);
    }

    private void ShowInstanceCreationCode(GameMakerRoom.GameObject instance) => _ = EditCreationCodeAsync(instance.InstanceID);

    public void ShowCreationOrder()
    {
        if (FrontRoom() is not var (workspace, room) || room.GameObjects is null) return;
        new CreationOrderWindow(room, workspace).Show(this);
    }

    /// <summary>The room name, workspace and selected tile layer of the room tab in front, or null with a message.</summary>
    private (Workspace Workspace, string Room, GameMakerRoom.Layer Layer)? FrontTileLayer()
    {
        if (Workspaces.Current is not { } workspace || Docking.ActiveTabId is not { } id || !_roomViews.TryGetValue(id, out var room)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, name))
        {
            Notify(NotificationLevel.Info, Loc.Current["rooms.noRoom"]);
            return null;
        }

        if (room.View.TileLayer is not { } layer)
        {
            Notify(NotificationLevel.Info, Loc.Current["rooms.noTileLayer"]);
            return null;
        }

        return (workspace, name, layer);
    }

    public void ExportTiles() => _ = ExportTilesAsync();

    /// <summary>Writes the tiles of the selected tile layer as CSV.</summary>
    public async Task ExportTilesAsync()
    {
        if (FrontTileLayer() is not var (_, room, layer) || layer.TilesData is not { } tiles) return;
        if (await PickPatchFile(Loc.Current["rooms.pickCsvSave"], true, ".csv") is not { } path) return;
        try
        {
            await File.WriteAllTextAsync(path, TileCsv.Write(tiles.TileData));
            Notify(NotificationLevel.Info, Loc.Current.Format("rooms.tilesExported", ("layer", layer.LayerName.Content ?? ""), ("room", room), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("rooms.tilesBad", ("reason", e.Message)));
        }
    }

    public void ImportTiles() => _ = ImportTilesAsync();

    /// <summary>Reads a CSV of the same size as the selected tile layer and applies the cells it changes as one undo step.</summary>
    public async Task ImportTilesAsync()
    {
        if (FrontTileLayer() is not var (workspace, room, layer) || layer.TilesData is not { } tiles) return;
        if (await PickPatchFile(Loc.Current["rooms.pickCsv"], false) is not { } path) return;
        try
        {
            var changes = TileCsv.Changes(tiles.TileData, await File.ReadAllTextAsync(path));
            if (changes.Count > 0) workspace.Edit(new RoomTilesCommand(room, layer.LayerId, changes));
            Notify(NotificationLevel.Info, Loc.Current.Format("rooms.tilesImported", ("count", changes.Count), ("layer", layer.LayerName.Content ?? "")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("rooms.tilesBad", ("reason", e.Message)));
        }
    }

    /// <summary>Raised with each image to tiles window, so a test can drive it.</summary>
    public event Action<ImageToTilesWindow>? ImageToTilesOpened;

    public void ConvertImageToTiles() => _ = ImageToTilesAsync();

    /// <summary>Makes a tile layer in the room in front from a picture, matching its cells with the tiles of a tile set; one undo step.</summary>
    public async Task ImageToTilesAsync()
    {
        if (FrontRoom() is not var (workspace, room)) return;
        List<GameMakerBackground> sets;
        using (workspace.Lock.Read()) sets = [.. workspace.Data.Backgrounds.Where(b => b.GMS2TileWidth > 0 && b.GMS2TileHeight > 0 && b.Texture is not null)];
        if (sets.Count == 0)
        {
            Notify(NotificationLevel.Info, Loc.Current["rooms.imgNoSets"]);
            return;
        }

        var window = new ImageToTilesWindow(room, sets, () => PickPatchFile(Loc.Current["rooms.pickImage"], false), () => workspace.Lock.Read());
        ImageToTilesOpened?.Invoke(window);
        if (!await window.AskAsync(this) || window.Match is not { } match) return;
        try
        {
            workspace.Edit(ImageToTiles.Build(workspace.Data, room.Name.Content ?? "", window.TileSet, window.LayerName, match));
            Notify(match.Unmatched.Count > 0 ? NotificationLevel.Warning : NotificationLevel.Info,
                Loc.Current.Format("rooms.imgDone", ("layer", window.LayerName), ("matched", match.Matched), ("unmatched", match.Unmatched.Count), ("first", ImageToTilesWindow.First(match))));
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ImageMagick.MagickException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    public void SaveRoomPng() => _ = SaveRoomPngAsync();

    /// <summary>Writes the room in front as a PNG at 100%.</summary>
    public async Task SaveRoomPngAsync()
    {
        if (Docking.ActiveTabId is not { } id || !_roomViews.TryGetValue(id, out var room) || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, name))
        {
            Notify(NotificationLevel.Info, Loc.Current["rooms.noRoom"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["rooms.pickPng"], true, ".png") is not { } path) return;
        try
        {
            await room.Ready;
            var reduce = false;
            if (room.View.TooLargeForPng is { } refusal)
            {
                if (!await AskRiskyAsync(refusal, name)) return;
                reduce = true;
            }

            await using var stream = File.Create(path);
            room.View.SavePng(stream, reduce);
            Notify(NotificationLevel.Info, Loc.Current.Format("rooms.pngSaved", ("room", name), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("rooms.pngFailed", ("reason", e.Message)));
        }
    }
}
