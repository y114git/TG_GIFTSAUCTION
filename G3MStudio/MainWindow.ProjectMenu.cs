using Avalonia.Controls;
using G3MLib.DataFile.Project;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The Project menu for people who know UTMT: Open Project, New Project, Close Project, recent projects, and a tip on every item that is off.</summary>
public sealed partial class MainWindow
{
    private static readonly string[] ProjectTipIds = ["project.new", "project.enable", "project.close", "project.sync", "project.options", "project.resolve", "project.moveDataFile", "project.disable"];

    private readonly MenuItem _recentProjectsMenu = new() { Name = "RecentProjects" };

    public void OpenProject() => _ = OpenProjectAsync();

    /// <summary>
    /// Opens a project from its <c>.g3mproj</c> file, its folder, or a UTMT project (folder or project.json, which is converted). A project the program
    /// knows opens its data file; otherwise the data file is asked for and must be the one the project was last synced with.
    /// </summary>
    public async Task OpenProjectAsync(string? given = null)
    {
        if ((given ?? await PickPatchFile(Loc.Current["project.open.pick"], false, G3MProjectService.FileExtension)) is not { } path) return;
        if (UtmtProjectImporter.Detect(path) is not null)
        {
            await OpenUtmtProjectAsync(path);
            return;
        }

        var file = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*" + G3MProjectService.FileExtension).FirstOrDefault() : path;
        if (file is null || !File.Exists(file) || !string.Equals(Path.GetExtension(file), G3MProjectService.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.open.notProject", ("path", path)));
            return;
        }

        var projects = Workspaces.Projects;
        var folder = Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".";
        var name = Path.GetFileNameWithoutExtension(file);
        string? data = null;
        if (projects.Registry.Projects.FirstOrDefault(p => PathRules.Same(p.Folder, folder)) is { } known && File.Exists(known.Synced.Path)) data = known.Synced.Path;
        // A known data file was saved by Studio and differs from the project's base on purpose; only a file the user picks must be the one the project was made from.
        var picked = data is null;
        data ??= await PickPatchFile(Loc.Current["project.open.pickData"], false, ".win");
        if (data is null) return;
        try
        {
            var project = await G3MProjectService.OpenAsync(file);
            var identity = await DataFileIdentity.ComputeAsync(data);
            if (picked && project.BaseFile is { } recorded && (recorded.Size != identity.Size || !string.Equals(recorded.Md5, identity.Md5, StringComparison.OrdinalIgnoreCase)))
            {
                Notify(NotificationLevel.Error, Loc.Current.Format("project.open.mismatch", ("name", Path.GetFileName(data))));
                return;
            }

            projects.Link(folder, name, identity);
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("project.open.failed", ("reason", e.Message)));
            return;
        }

        // The file that is already open only needs the project attached; any other file opens and finds it.
        if (Workspaces.Current is { Project: null } current && PathRules.Same(current.Path, data) && projects.Attach(current) is { } link)
        {
            link.Changed += UpdateTexts;
            BindGit();
            UpdateTexts();
        }
        else await OpenAsync(data);
    }

    public void NewProject() => _ = EnableProjectAsync(ask: true);

    /// <summary>Ends Project mode for the open file and keeps the project folder; Open Project (or the offer when the file opens) brings it back.</summary>
    public void CloseProject()
    {
        if (Workspaces.Current is not { Project: not null } workspace)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.none"]);
            return;
        }

        Workspaces.Projects.Disable(workspace, deleteProject: false);
        BindGit();
        UpdateTexts();
    }

    /// <summary>A closed or unregistered project beside the file that was just opened is offered, so nobody has to find it by hand; without one, the one-time offer to enable a project.</summary>
    internal async Task OfferProjectAsync(Workspace workspace)
    {
        var file = await Workspaces.Projects.FindClosedAsync(workspace);
        if (Workspaces.Current != workspace || workspace.Project is not null) return; // closed or changed while the folders were read
        if (file is not null)
            Notifications.Post(NotificationLevel.Info, Loc.Current.Format("project.found", ("name", Path.GetFileNameWithoutExtension(file))), Loc.Current["project.foundAction"], () => _ = OpenProjectAsync(file));
        else if (Workspaces.Projects.ShouldOffer(workspace))
            Notifications.Post(NotificationLevel.Info, Loc.Current["project.offer"], Loc.Current["project.enable.action"], RunEnableProject); // a method, not a lambda: a lambda here would share its closure with the workspace of this method
    }

    internal void RefreshRecentProjects()
    {
        _recentProjectsMenu.Items.Clear();
        foreach (var record in Workspaces.Projects.Registry.Projects.Where(p => Directory.Exists(p.Folder)).Take(10))
        {
            var item = new MenuItem { Header = record.Name, Tag = record.Folder };
            ToolTip.SetTip(item, record.Folder);
            item.Click += (_, _) => _ = OpenProjectAsync(Path.Combine(record.Folder, record.Name + G3MProjectService.FileExtension));
            _recentProjectsMenu.Items.Add(item);
        }

        if (_recentProjectsMenu.Items.Count > 0) return;
        var none = new MenuItem { IsEnabled = false };
        none.Bind(MenuItem.HeaderProperty, Tr("project.recent.none"));
        _recentProjectsMenu.Items.Add(none);
    }

    /// <summary>A Project item that is off says why: no data file is open, the file already has a project, or it has none yet.</summary>
    private void RefreshProjectTips()
    {
        foreach (var id in ProjectTipIds)
        {
            if (!_menuItems.TryGetValue(id, out var item) || Registry.Find(id) is not { } command) continue;
            if (Registry.IsEnabled(command))
            {
                RefreshMenuTip(id);
                continue;
            }

            var why = !Registry.HasContext(AppCommands.WorkspaceOpen) ? "project.why.noFile" : command.When == AppCommands.ProjectOff ? "project.why.hasProject" : "project.why.noProject";
            ToolTip.SetTip(item, Loc.Current[why]);
            ToolTip.SetShowOnDisabled(item, true);
        }
    }
}
