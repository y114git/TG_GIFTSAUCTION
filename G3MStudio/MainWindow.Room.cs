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

    public void ShowCreationCode()
    {
        if (FrontRoom() is not var (_, room)) return;
        if (room.CreationCodeId?.Name.Content is { } code) OpenCodeAt(code, 1, 1);
        else Notify(NotificationLevel.Info, Loc.Current["rooms.noCode"]);
    }

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
            await using var stream = File.Create(path);
            room.View.SavePng(stream);
            Notify(NotificationLevel.Info, Loc.Current.Format("rooms.pngSaved", ("room", name), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("rooms.pngFailed", ("reason", e.Message)));
        }
    }
}
