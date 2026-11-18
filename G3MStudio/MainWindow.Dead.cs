using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

public sealed partial class MainWindow
{
    public void ShowDeadResources()
    {
        if (Workspaces.Current is not { } workspace) return;
        ShowTool(PerFile + "deadResources", () => new DeadResourcesWindow(workspace.Nicknames, () => ScanDeadAsync(workspace), chosen => DeleteDeadAsync(workspace, chosen)));
    }

    private Task<IReadOnlyList<DeadResource>> ScanDeadAsync(Workspace workspace)
    {
        ApplyCodeEditorsForSearch();
        var quick = Explorer.Model.Quick;
        return Task.Run(() => DeadResources.Find(workspace, quick));
    }

    /// <summary>
    /// Deletes the chosen dead resources as one undo step after a question that lists the counts. The scan runs again first: a resource that
    /// is used or doubtful by now, or whose type cannot be deleted, is skipped and named. Returns what to tell the user, or null when asked off.
    /// </summary>
    private async Task<string?> DeleteDeadAsync(Workspace workspace, IReadOnlyList<DeadResource> chosen)
    {
        var safe = (await ScanDeadAsync(workspace)).Where(d => d.IsSafe).Select(d => (d.Type, d.Name)).ToHashSet();
        var skipped = new List<string>();
        var go = new List<DeadResource>();
        foreach (var item in chosen)
        {
            var reason = !ResourceListCommand.Deletable.Contains(item.Type) ? "dead.skip.cannot" : safe.Contains((item.Type, item.Name)) ? null : "dead.skip.changed";
            if (reason is null) go.Add(item);
            else skipped.Add($"{workspace.Nicknames.Both(item.Type, item.Name)}: {Loc.Current[reason]}");
        }

        var tail = skipped.Count == 0 ? "" : Environment.NewLine + Loc.Current.Format("dead.skipped", ("count", skipped.Count), ("list", string.Join(Environment.NewLine, skipped.Take(8).Select(s => "  " + s)) + (skipped.Count > 8 ? Environment.NewLine + $"  ... +{skipped.Count - 8}" : "")));
        if (go.Count == 0) return Loc.Current["dead.nothingSafe"] + tail;
        var counts = string.Join(Environment.NewLine, go.GroupBy(d => d.Type).Select(g => $"  {Loc.Current["explorer.type." + g.Key]}: {g.Count()}"));
        if (!await Confirm(Loc.Current.Format("dead.confirmTitle", ("count", go.Count)), Loc.Current.Format("dead.confirm", ("count", go.Count), ("counts", counts)) + tail)) return null;
        if (!await EditOrForceAsync(workspace, keep => BulkResources.Delete([.. go.Select(d => (d.Type, d.Name, false))], keep), string.Join(", ", go.Take(3).Select(d => d.Name)))) return null;
        var names = go.Select(d => (d.Type, d.Name)).ToHashSet();
        foreach (var tab in Docking.Tabs.Tabs.Where(t => ResourceTypes.SplitKey(t.Key ?? "") is var (type, name) && names.Contains((type, name))).ToList()) Docking.CloseTab(tab.Id, true);
        return Loc.Current.Format("dead.done", ("count", go.Count)) + tail;
    }
}
