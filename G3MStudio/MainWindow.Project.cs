using Avalonia.Platform.Storage;
using Avalonia.Threading;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Project mode in the window (ADR-0010): the sync state item, Enable project, autosave and autosync timers, settings and moving the data file.</summary>
public partial class MainWindow
{
    private string? _exportName;
    private int _exportDone, _exportTotal;
    private CancellationTokenSource? _exporting;
    private DispatcherTimer? _projectTimer;

    private SavePipeline Pipeline => new(Workspaces.UserData, Workspaces.Settings.Current.BackupCount);

    private void InitProject()
    {
        ProjectButton.Click += (_, _) => Registry.Execute(Workspaces.Current?.Project is null ? "project.enable" : Workspaces.Current.Project.DataFileChanged ? "project.resolve" : "project.sync");
        _projectTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => _ = RunProjectAsync(link => link.TickAsync(DateTimeOffset.Now, Pipeline)));
        _projectTimer.Start();
        Deactivated += (_, _) => _ = RunProjectAsync(link => link.FocusLostAsync(Pipeline));
        Closed += (_, _) => _projectTimer.Stop();
    }

    /// <summary>Called when a workspace is shown: the sync state follows the project, queued edits that fail are reported, and the one-time offer appears.</summary>
    private void ShowProject(Workspace workspace)
    {
        workspace.QueuedEditFailed += (_, e) => Notify(NotificationLevel.Error, Loc.Current.Format("project.queueFailed", ("reason", e.Message)));
        BindGit();
        if (workspace.Project is { } link)
        {
            link.Changed += UpdateTexts;
            if (link.DataFileChanged) Dispatcher.UIThread.Post(() => _ = ResolveExternalChangeAsync());
        }
        else if (Workspaces.Projects.ShouldOffer(workspace))
            Notifications.Post(NotificationLevel.Info, Loc.Current["project.offer"], Loc.Current["project.enable.action"], () => Registry.Execute("project.enable"));
    }

    private string ProjectText()
    {
        if (Workspaces.Current is not { } workspace) return "";
        if (workspace.Project is not { } link) return Loc.Current["project.state.enable"];
        return link.State switch
        {
            ProjectState.Syncing => Loc.Current["project.state.syncing"],
            ProjectState.Exporting => Loc.Current["project.state.exporting"],
            ProjectState.DataFileChanged => Loc.Current["project.state.changed"],
            ProjectState.NotSynced => Loc.Current.Format("project.state.notSynced", ("count", link.NotSynced)),
            _ => Loc.Current["project.state.synced"],
        };
    }

    private async Task RunProjectAsync(Func<ProjectLink, Task> run)
    {
        if (Workspaces.Current?.Project is not { } link) return;
        try
        {
            await run(link);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ProjectException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.writeFailed", ("reason", e.Message)));
        }
    }

    public void EnableProject() => _ = EnableProjectAsync();

    private async Task EnableProjectAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.Project is not null)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.already"]);
            return;
        }

        var projects = Workspaces.Projects;
        string? root = null;
        var remember = false;
        if (projects.Registry.NeedsLocationQuestion)
        {
            if (await ProjectDialogs.AskLocationAsync(this, projects.SuggestedRoot) is not var (answer, keep)) return;
            (root, remember) = (answer, keep);
        }

        (_exportName, _exportDone, _exportTotal) = (workspace.Name, 0, 0);
        using var source = _exporting = new CancellationTokenSource();
        ShowProgress();
        try
        {
            var link = await projects.EnableAsync(workspace, root, remember, new Progress<BatchProgress>(p => { (_exportDone, _exportTotal) = (p.Done, p.Total); ShowProgress(); }), source.Token);
            link.Changed += UpdateTexts;
            BindGit();
            Notify(NotificationLevel.Info, Loc.Current.Format("project.created", ("name", workspace.Name)));
            if (link.Warnings.Count > 0) Notify(NotificationLevel.Warning, Loc.Current.Format("project.warnings", ("count", link.Warnings.Count), ("first", link.Warnings[0])));
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.cancelled"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ProjectException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.createFailed", ("reason", e.Message)));
        }
        finally
        {
            _exportName = null;
            _exporting = null;
            Progress.IsVisible = false;
            UpdateTexts();
        }
    }

    public void SyncProject()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.Project is not { } link) Notify(NotificationLevel.Info, Loc.Current["project.none"]);
        else _ = SaveOrSyncAsync(workspace, async () => await link.SyncAsync(Pipeline));
    }

    public void ProjectOptions() => _ = ProjectOptionsAsync();

    private async Task ProjectOptionsAsync()
    {
        if (Workspaces.Current?.Project is not { } link)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.none"]);
            return;
        }

        if (await ProjectDialogs.AskSettingsAsync(this, link.Settings) is { } settings) link.UpdateSettings(settings);
    }

    public void MoveDataFile() => _ = MoveDataFileAsync();

    private async Task MoveDataFileAsync()
    {
        if (Workspaces.Current is not { Project: { } link } workspace)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.none"]);
            return;
        }

        var old = workspace.Path;
        await ProjectDialogs.AskMoveAsync(this, old, async (path, mode) =>
        {
            try
            {
                await link.RelocateAsync(path, mode, Pipeline);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ProjectException)
            {
                return Loc.Current.Format("project.move.failed", ("reason", e.Message));
            }

            Workspaces.Recent.Remove(old);
            Workspaces.Recent.Add(workspace.Path, workspace.Name, workspace.Version, DateTimeOffset.Now);
            _viewPath = workspace.Path;
            SaveView();
            Notify(NotificationLevel.Info, Loc.Current.Format("project.move.done", ("path", workspace.Path)));
            return null;
        });
    }

    public void DisableProject() => _ = DisableProjectAsync();

    private async Task DisableProjectAsync()
    {
        if (Workspaces.Current is not { Project: not null } workspace)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.none"]);
            return;
        }

        if (await ProjectDialogs.AskDisableAsync(this) is not { } delete) return;
        Workspaces.Projects.Disable(workspace, delete);
        BindGit();
        UpdateTexts();
    }

    private void BindGit() => _ = _git.BindAsync(Workspaces.Current?.Project?.Record.Folder);

    public void ResolveExternalChange() => _ = ResolveExternalChangeAsync();

    /// <summary>ADR-0010: a data file that changed outside. Merging and replacing write the data file and then open it again.</summary>
    private async Task ResolveExternalChangeAsync()
    {
        if (Workspaces.Current is not { Project: { DataFileChanged: true } link } workspace) return;
        if (await ProjectDialogs.AskExternalChangeAsync(this, workspace.Name) is not { } choice) return;
        if (choice == ExternalChange.KeepBoth)
        {
            Workspaces.Projects.Disable(workspace, deleteProject: false);
            UpdateTexts();
            return;
        }

        var path = workspace.Path;
        SaveResult? result = null;
        try
        {
            await WithProgressAsync(workspace.Name, async (progress, token) =>
            {
                if (choice == ExternalChange.Overwrite)
                {
                    result = await link.OverwriteDataFileAsync(Pipeline, token);
                    return;
                }

                var plan = await link.PlanMergeAsync(progress, token);
                _exportTotal = 0;
                var choices = new Dictionary<string, MergeSide>();
                if (plan.Conflicts.Count > 0)
                {
                    Progress.IsVisible = false;
                    if (await ProjectDialogs.AskConflictsAsync(this, plan.Conflicts) is not { } picked) return;
                    choices = picked;
                    ShowProgress();
                }

                result = await link.BringInAsync(plan, choices, Pipeline, token);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ProjectException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.mergeFailed", ("reason", e.Message)));
            return;
        }

        if (result is not { Saved: true }) return;
        Notify(NotificationLevel.Info, Loc.Current.Format("project.merged", ("name", Path.GetFileName(path))));
        await OpenAsync(path);
    }

    /// <summary>Shows the progress overlay with Cancel while <paramref name="run"/> works; a cancel is reported and ends quietly.</summary>
    private async Task WithProgressAsync(string name, Func<IProgress<BatchProgress>, CancellationToken, Task> run)
    {
        (_exportName, _exportDone, _exportTotal) = (name, 0, 0);
        using var source = _exporting = new CancellationTokenSource();
        ShowProgress();
        try
        {
            await run(new Progress<BatchProgress>(p => { (_exportDone, _exportTotal) = (p.Done, p.Total); ShowProgress(); }), source.Token);
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.cancelled"]);
        }
        finally
        {
            _exportName = null;
            _exporting = null;
            Progress.IsVisible = false;
            UpdateTexts();
        }
    }

    /// <summary>A data file did not open and its project is known: offer to write it again from the project (ADR-0010 scenarios).</summary>
    private async Task OfferRebuildAsync(string path)
    {
        if (Workspaces.Projects.Registry.FindByPath(path) is not { } record) return;
        if (!await ProjectDialogs.AskRebuildAsync(this, Path.GetFileName(path))) return;
        var baseFile = Workspaces.Projects.NewestBackup(path);
        if (baseFile is null && StorageProvider.CanOpen)
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Loc.Current["project.rebuild.base"], AllowMultiple = false });
            baseFile = picked is [{ } first, ..] ? first.TryGetLocalPath() : null;
        }

        try
        {
            await WithProgressAsync(Path.GetFileName(path), (_, token) => Workspaces.Projects.RebuildAsync(record, baseFile, token));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ProjectException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.rebuildFailed", ("reason", e.Message)));
            return;
        }

        await OpenAsync(path);
    }
}
