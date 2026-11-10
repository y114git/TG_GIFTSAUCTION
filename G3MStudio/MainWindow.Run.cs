using Avalonia.Threading;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Live run: applies the edits to a working copy, starts the game, shows its output and state, and restarts it quickly after edits.</summary>
public sealed partial class MainWindow
{
    private const string RunTempName = "g3mstudio_run.tmp";
    private const string RunProblemSource = "(run)";

    private readonly LiveRun _live = new();
    private string? _runCopyKey; // history version and room of the working copy on disk
    private string? _lastRoom;
    private string? _lastRunner;
    private bool _restarting;
    private bool _warnedTempRun;
    private readonly HashSet<string> _warnedUnknownRunner = [];
    private RunExit? _lastCrash;
    private Workspace? _runWorkspace;
    private DispatcherTimer? _autoRestart;

    private void InitRun()
    {
        _output.Bind(Notifications, _live.Log);
        _live.StateChanged += UpdateRunStatus;
        _live.Finished += OnRunFinished;
        _output.LocationRequested += OpenRunLocation;
        _output.RestartRequested += RestartGame;
        _output.StopRequested += StopGame;
    }

    /// <summary>Completes when no game runs; tests and shutdown use it.</summary>
    public Task WaitRunAsync() => _live.WaitIdleAsync();

    public RunState RunState => _live.State;

    public void RunGame() => _ = RunGameAsync();

    /// <summary>Plays the room of the room tab in front: the game is started with that room first in the room order, so it opens there instead of at the real first room.</summary>
    public void PlayRoom() => _ = PlayRoomAsync();

    /// <summary>Runs the game with a GameMaker runtime picked from the ones on this machine (UTMT-RUN-03), for this run only.</summary>
    public void RunWithRuntime() => _ = RunWithRuntimeAsync();

    public async Task RunWithRuntimeAsync()
    {
        var folders = RuntimeFinder.DefaultFolders().Concat(Workspaces.Settings.Current.RuntimeFolders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var rows = RuntimeFinder.Find(folders).ToList();
        if (RuntimeFinder.Gms1() is { } gms1) rows.Add(gms1);
        var settings = Workspaces.Settings.Current;
        // The first row is the game's own executable, the one a plain Run uses.
        if (Workspaces.Current is { } workspace && (settings.RunRunner.Length > 0 ? settings.RunRunner : RunLocator.Find(workspace.Path, workspace.Data.GeneralInfo?.FileName?.Content)) is { } own)
            rows.Insert(0, new RuntimeInfo(Loc.Current["run.gameExe"], own));
        if (rows.Count == 0)
        {
            Notify(NotificationLevel.Info, Loc.Current["run.noRuntime"]);
            return;
        }

        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["cmd.run.withRuntime"].TrimEnd('.'), Loc.Current["run.pickRuntime"], [.. rows.Select(r => r.Version + "\t" + r.Runner)], [Loc.Current["run.colVersion"], Loc.Current["run.colPath"]]) is { } at)
            await RunGameAsync(runner: rows[at].Runner);
    }

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

    public void StopGame() => _ = StopGameAsync();

    /// <summary>Asks the game to close and kills it after the grace time of the settings. Only the process Studio started is touched.</summary>
    public async Task StopGameAsync()
    {
        if (_live.State != RunState.Running)
        {
            Notify(NotificationLevel.Info, Loc.Current["run.notRunning"]);
            return;
        }

        _live.Grace = TimeSpan.FromSeconds(Workspaces.Settings.Current.RunStopSeconds);
        await _live.StopAsync();
    }

    /// <summary>Stops the running game and starts it again on the latest edits; starts it when none runs.</summary>
    public void RestartGame() => _ = RestartGameAsync();

    public async Task RestartGameAsync()
    {
        if (Workspaces.Current is null || _live.State is RunState.Preparing or RunState.Stopping) return;
        _restarting = true;
        try
        {
            if (_live.State == RunState.Running)
            {
                Notify(NotificationLevel.Info, Loc.Current["live.restarting"]);
                _live.Grace = TimeSpan.FromSeconds(Workspaces.Settings.Current.RunStopSeconds);
                await _live.StopAsync();
            }

            await RunGameAsync(_lastRoom, _lastRunner);
        }
        finally
        {
            _restarting = false;
        }
    }

    /// <summary>
    /// Asks about a run options file that is not approved yet and remembers its hash on yes. True when the owner approved it now.
    /// The prompt is not in the Prompts list: a run file can name any program, so it is asked every time it is new or changed.
    /// </summary>
    private async Task<bool> AskRunFileTrustAsync(string dataFile)
    {
        if (RunPlan.Unapproved(Workspaces.Settings.Current, dataFile, Workspaces.UserData) is not var (project, hash)) return false;
        var details = Loc.Current.Format("live.trustBody", ("runner", project.Runner), ("arguments", project.Arguments), ("folder", project.WorkingDir), ("environment", project.Environment));
        if (!await Confirm(Loc.Current["live.trustTitle"], details)) return false;
        Workspaces.Settings.Update(x => x with { TrustedRunFiles = [.. x.TrustedRunFiles, hash] });
        return true;
    }

    /// <summary>Applies the edits to a working copy next to the data file (or saves the data file itself when the run option says so) and starts the game on it. Returns once the game runs; <see cref="LiveRun.WaitIdleAsync"/> waits for its end.</summary>
    public async Task RunGameAsync(string? startRoom = null, string? runner = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (_live.State != RunState.Idle)
        {
            Notify(NotificationLevel.Warning, Loc.Current["run.running"]);
            return;
        }

        (_lastRoom, _lastRunner) = (startRoom, runner);
        var name = workspace.Name;
        var settings = Workspaces.Settings.Current;
        var inPlace = settings.RunSaveInPlace && startRoom is null; // a room start changes the room order, which never goes into the real file
        var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(workspace.Path)) ?? ".", RunTempName);
        var home = Workspaces.UserData;
        if (await AskRunFileTrustAsync(workspace.Path) && settings.RunOptionsLocation == RunOptionsLocation.StudioFolder) await OfferRunOptionsMigrationAsync(workspace.Path);
        settings = Workspaces.Settings.Current;

        var (spec, check, found) = RunPlan.Resolve(settings, workspace.Path, workspace.Data.GeneralInfo?.FileName?.Content, inPlace ? workspace.Path : temp, runner, home);
        if (spec is null)
        {
            Notify(NotificationLevel.Error, found is null
                ? Loc.Current.Format("run.noRunner", ("file", Path.GetFileName(workspace.Path)))
                : Loc.Current.Format("live.missingExe", ("path", found)));
            return;
        }

        if (!ApplyCodeEditors()) return;
        _lastCrash = null;
        workspace.Problems.SetRaw("run", []);
        if (check == RunnerCheck.Unknown && _warnedUnknownRunner.Add(Path.GetFullPath(workspace.Path) + "|" + Path.GetFullPath(spec.Runner))) Notify(NotificationLevel.Warning, Loc.Current.Format("live.unknownExe", ("path", spec.Runner)));
        Notify(NotificationLevel.Info, Loc.Current.Format("run.starting", ("name", name)));
        if (inPlace) Notify(NotificationLevel.Warning, Loc.Current["live.inPlaceWarning"]);
        else if (settings.WarnTempRun && !_warnedTempRun)
        {
            _warnedTempRun = true;
            Notify(NotificationLevel.Warning, Loc.Current["run.tempWarning"]);
        }

        if (startRoom is not null) Notify(NotificationLevel.Warning, Loc.Current.Format("run.roomWarning", ("room", startRoom)));
        try
        {
            if (!await _live.StartAsync(() => PrepareRunAsync(workspace, spec, startRoom, inPlace, temp))) return;
            _runWorkspace?.History.Changed -= OnEditDuringRun;
            _runWorkspace = workspace;
            workspace.History.Changed += OnEditDuringRun;
            Notify(NotificationLevel.Info, Loc.Current.Format("run.started", ("name", name)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("run.failed", ("reason", e.Message)));
            DeleteWorkingCopy(temp);
        }
    }

    /// <summary>Writes the working copy, or skips the write when nothing changed since the copy on disk was made (the fast path of a quick restart).</summary>
    private async Task<RunSpec?> PrepareRunAsync(Workspace workspace, RunSpec spec, string? room, bool inPlace, string temp)
    {
        if (inPlace) return workspace.IsDirty && !await SaveAsync() ? null : spec;
        var key = $"{workspace.History.Version}|{room}";
        if (key == _runCopyKey && File.Exists(temp)) return spec;
        _runCopyKey = null;
        var restore = room is null ? null : MoveRoomFirst(workspace, room);
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
            return null;
        }

        _runCopyKey = key;
        return spec;
    }

    private void DeleteWorkingCopy(string path)
    {
        _runCopyKey = null;
        try
        {
            if (Path.GetFileName(path) == RunTempName && File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the game may still hold it; the next run overwrites it
        }
    }

    private void OnRunFinished(RunExit exit)
    {
        if (_runWorkspace is { } workspace)
        {
            workspace.History.Changed -= OnEditDuringRun;
            _runWorkspace = null;
        }

        // A restart keeps the copy so the next start can skip the write when nothing changed.
        if (!_restarting) DeleteWorkingCopy(exit.Spec.DataFile);
        var name = Workspaces.Current?.Name ?? "";
        if (exit.Stopped)
        {
            if (!_restarting) Notify(NotificationLevel.Info, Loc.Current["run.stopped"]);
        }
        else if (exit.Crashed)
        {
            ReportCrash(exit, name);
        }
        else
        {
            Notify(NotificationLevel.Info, Loc.Current.Format("run.exited", ("name", name), ("code", exit.Code)));
        }

        UpdateRunStatus();
    }

    /// <summary>A game that ends with a non-zero code: the code and the last lines in the Output, and a Problems entry that opens the failing code line when the output names one.</summary>
    private void ReportCrash(RunExit exit, string name)
    {
        _lastCrash = exit;
        var last = exit.Tail.Count > 0 ? exit.Tail[^1] : "";
        Notify(NotificationLevel.Error, Loc.Current.Format("live.crashed", ("name", name), ("code", exit.Code), ("lines", string.Join(" | ", exit.Tail.TakeLast(5)))));
        var where = exit.Tail.Select(GmlLocation.Find).LastOrDefault(l => l is not null);
        var problem = new Problem(where?.Entry ?? RunProblemSource, "Error", "RUN", Loc.Current.Format("live.crashProblem", ("code", exit.Code), ("line", last)), where?.Line ?? 1, 1);
        Workspaces.Current?.Problems.SetRaw("run", [problem]);
        Docking.ShowPanel("Problems");
    }

    /// <summary>Opens the code entry a game error names, at its line.</summary>
    private void OpenRunLocation(GmlLocation location)
    {
        if (Workspaces.Current is not { } workspace || !workspace.Data.Code.Any(c => c.Name?.Content == location.Entry))
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("live.noEntry", ("entry", location.Entry)));
            return;
        }

        OpenCodeAt(location.Entry, location.Line, 1);
    }

    /// <summary>Auto restart: an edit while the game runs restarts it once the edits pause for a moment.</summary>
    private void OnEditDuringRun()
    {
        if (!Workspaces.Settings.Current.RunAutoRestart || _live.State != RunState.Running) return;
        _autoRestart ??= new DispatcherTimer(TimeSpan.FromMilliseconds(1500), DispatcherPriority.Normal, (_, _) =>
        {
            _autoRestart?.Stop();
            if (_live.State == RunState.Running) RestartGame();
        });
        _autoRestart.Stop();
        _autoRestart.Start();
    }

    /// <summary>Stops the game Studio started, without waiting, when the window goes away.</summary>
    private void KillGame() => _live.Kill();

    /// <summary>The run state in the status bar: running, stopping, or the exit code of the last crash.</summary>
    private void UpdateRunStatus()
    {
        var text = _live.State switch
        {
            RunState.Preparing => Loc.Current["live.statusPreparing"],
            RunState.Running => Loc.Current.Format("live.statusRunning", ("name", Workspaces.Current?.Name ?? "")),
            RunState.Stopping => Loc.Current["live.statusStopping"],
            _ => _lastCrash is { } crash ? Loc.Current.Format("live.statusCrashed", ("code", crash.Code)) : "",
        };
        _output.SetRunState(_live.State);
        RunText.Text = text;
        RunText.IsVisible = text.Length > 0;
    }
}
