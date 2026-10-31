using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Runs the game on a temporary copy of the model, with its output in the Output panel.</summary>
public sealed partial class MainWindow
{
    private const string RunTempName = "g3mstudio_run.tmp";

    private GameRun? _game;

    public void RunGame() => _ = RunGameAsync();

    /// <summary>Plays the room of the room tab in front: the game is started with that room first in the room order, so it opens there instead of at the real first room.</summary>
    public void PlayRoom() => _ = PlayRoomAsync();

    public async Task PlayRoomAsync()
    {
        if (Workspaces.Current is null || Docking.ActiveTabId is not { } id || !_roomViews.ContainsKey(id)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, name))
        {
            Notify(NotificationLevel.Info, Loc.Current["rooms.noRoom"]);
            return;
        }

        await RunGameAsync(name);
    }

    /// <summary>Moves the named room to the front of the room order and returns the call that puts the order back. Done under the write lock, outside the undo history, for the length of the save only.</summary>
    private static Action? MoveRoomFirst(Workspace workspace, string room)
    {
        using (workspace.Lock.Write())
        {
            var order = workspace.Data.GeneralInfo.RoomOrder;
            var at = order.ToList().FindIndex(r => r.Resource?.Name?.Content == room);
            if (at <= 0) return null; // already first, or not in the room order (the game cannot start there)
            var reference = order[at];
            order.RemoveAt(at);
            order.Insert(0, reference);
            return () =>
            {
                using (workspace.Lock.Write())
                {
                    order.Remove(reference);
                    order.Insert(Math.Min(at, order.Count), reference);
                }
            };
        }
    }

    public void StopGame()
    {
        if (_game is { IsRunning: true } game)
        {
            game.Stop();
            Notify(NotificationLevel.Info, Loc.Current["run.stopped"]);
        }
        else
        {
            Notify(NotificationLevel.Info, Loc.Current["run.notRunning"]);
        }
    }

    /// <summary>Writes the model, saved or not, next to the data file as a temporary file and starts the game on it. The data file is not touched.</summary>
    public async Task RunGameAsync(string? startRoom = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (_game is { IsRunning: true })
        {
            Notify(NotificationLevel.Warning, Loc.Current["run.running"]);
            return;
        }

        var name = workspace.Name;
        var settings = Workspaces.Settings.Current;
        var runner = settings.RunRunner.Length > 0 ? settings.RunRunner : GameRun.FindRunner(workspace.Path, workspace.Data.GeneralInfo?.FileName?.Content);
        if (runner is null || !File.Exists(runner))
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("run.noRunner", ("file", Path.GetFileName(workspace.Path))));
            return;
        }

        if (!ApplyCodeEditors()) return;
        var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(workspace.Path))!, RunTempName);
        Notify(NotificationLevel.Info, Loc.Current.Format("run.starting", ("name", name)));
        try
        {
            if (startRoom is not null) Notify(NotificationLevel.Warning, Loc.Current.Format("run.roomWarning", ("room", startRoom)));
            var restore = startRoom is null ? null : MoveRoomFirst(workspace, startRoom);
            SaveResult written;
            try
            {
                written = await Pipeline.SaveAsync(workspace.Data, temp);
            }
            finally
            {
                restore?.Invoke();
            }

            if (!written.Saved)
            {
                Notify(NotificationLevel.Error, Loc.Current.Format("run.failed", ("reason", written.Message ?? written.Failure.ToString())));
                return;
            }

            var game = _game = GameRun.Start(runner, temp, (text, error) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(error ? NotificationLevel.Warning : NotificationLevel.Info, text)), settings.RunArguments, settings.RunDebugOutput);
            Notify(NotificationLevel.Info, Loc.Current.Format("run.started", ("name", name)));
            var code = await game.Exited;
            Notify(code == 0 ? NotificationLevel.Info : NotificationLevel.Warning, Loc.Current.Format("run.exited", ("name", name), ("code", code)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("run.failed", ("reason", e.Message)));
        }
        finally
        {
            _game?.Dispose();
            _game = null;
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
