using System.Text.Json;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Mods;

namespace G3MStudio;

/// <summary>The mod pack builder and diff viewer: compare two data files, export changes as a pack, apply a pack to another version of the game.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the choice of the file to compare with (gets the left file; returns a workspace the comparison owns and disposes, or null). Tests set it.</summary>
    public Func<Workspace, Task<Workspace?>>? DiffRightChooser { get; set; }

    /// <summary>Replaces the dialog that asks the name, version and authors of a pack (change count, suggested name). Tests set it.</summary>
    public Func<int, string, Task<PackInfo?>>? PackInfoAsker { get; set; }

    /// <summary>Replaces the dry-run dialog of a pack (the plan; returns the choice for each conflict, or null when cancelled). Tests set it.</summary>
    public Func<ReapplyPlan, Task<IReadOnlyDictionary<string, Resolution>?>>? PackReviewer { get; set; }

    public void CompareDataFiles() => _ = CompareDataFilesAsync();

    public void ExportModPack() => _ = ExportModPackAsync();

    public void ApplyModPack() => _ = ApplyModPackAsync();

    /// <summary>Compares the open data file (or one picked) with another and shows the result in a window that lives until it is closed; the files it opened are released then.</summary>
    public async Task CompareDataFilesAsync()
    {
        var owned = new List<Workspace>();
        var left = Workspaces.Current;
        if (left is null)
        {
            left = await OpenDiffSideAsync("mods.diff.pickLeft");
            if (left is not null) owned.Add(left);
        }

        var right = left is null ? null : DiffRightChooser is { } choose ? await choose(left) : await OpenDiffSideAsync("mods.diff.pickRight");
        if (left is null || right is null || await ComputeDiffAsync(left, right) is not { } diff)
        {
            foreach (var workspace in owned.Append(right).OfType<Workspace>()) workspace.Dispose();
            return;
        }

        owned.Add(right);
        var window = new DiffWindow(this, left, right, diff, Workspaces.Settings.Current.Diff);
        window.Closed += (_, _) => owned.ForEach(w => w.Dispose());
        window.Show(this);
    }

    /// <summary>Writes the text report of what differs between the files of the two sides (the files on disk, not unsaved edits).</summary>
    public async Task SaveDiffReportAsync(Workspace left, Workspace right)
    {
        if (await PickPatchFile(Loc.Current["mods.pickReport"], true) is not { } report) return;
        await RunPatchAsync("mods.comparing", async token =>
        {
            var result = await G3MLib.Modding.Diffing.DiffService.CompareAsync(left.Path, right.Path, report, G3MLib.Modding.Diffing.DiffReportMode.Standard, null, token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.compared", ("path", report), ("count", result.DifferenceCount)) : result.Error);
        });
    }

    private async Task<Workspace?> OpenDiffSideAsync(string titleKey) =>
        await PickPatchFile(Loc.Current[titleKey], false) is { } path ? await OpenDiffPathAsync(path) : null;

    private async Task<Workspace?> OpenDiffPathAsync(string path)
    {
        try
        {
            return await Workspaces.OpenDetachedAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or G3MLib.DataFile.GameMakerSerializationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
            return null;
        }
    }

    internal async Task<DiffResult?> ComputeDiffAsync(Workspace left, Workspace right)
    {
        DiffResult? result = null;
        await RunPatchAsync("mods.diff.comparing", async token =>
        {
            result = await Task.Run(() =>
            {
                using (left.Lock.Read())
                using (right.Lock.Read())
                    return DataDiff.Compare(left.Data, right.Data, token);
            }, token);
            return (true, Loc.Current.Format("mods.diff.compared", ("count", result.Items.Count)));
        });
        return result;
    }

    /// <summary>
    /// Runs an edit that may be refused with a <see cref="RiskyRefusalException"/>: the owner is warned and may go on (unless risky actions are set to refuse),
    /// and the edit is built again with its force flag set. Returns true when an edit was made.
    /// </summary>
    internal async Task<bool> EditWithRiskAsync(Workspace target, Func<bool, Task<EditCommand?>> build, string name)
    {
        foreach (var keep in new[] { false, true })
        {
            try
            {
                if (await build(keep) is not { } command)
                {
                    Notify(NotificationLevel.Info, Loc.Current["mods.diff.nothing"]);
                    return false;
                }

                await target.EditInBackground(command);
                return true;
            }
            catch (RiskyRefusalException e) when (!keep)
            {
                if (!await AskRiskyAsync(e, name)) return false;
            }
            catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException)
            {
                Notify(NotificationLevel.Error, Loc.Current.Format("mods.failed", ("what", name), ("reason", e.Message)));
                return false;
            }
        }

        return false;
    }

    /// <summary>Makes one item (or one property of it) of the open file like the other file; one undo step.</summary>
    internal async Task<bool> ApplyDiffItemAsync(Workspace mine, Workspace other, DiffItem item, PropertyChange? property)
    {
        if (!ReferenceEquals(Workspaces.Current, mine))
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.diff.readOnly"]);
            return false;
        }

        if (item.Type == "code" && item.Kind != ChangeKind.Changed && property is null)
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.diff.codeOwner"]);
            return false;
        }

        var done = await EditWithRiskAsync(mine, keep => Task.Run(() =>
        {
            using (mine.Lock.Read())
            using (other.Lock.Read())
                return ModPackApplier.ForItem(new Side(mine.Data, mine.Path), new Side(other.Data, other.Path), item, property, keep, out _);
        }), item.Name);
        if (done) Notify(NotificationLevel.Info, Loc.Current.Format("mods.diff.applied", ("name", item.Name)));
        return done;
    }

    /// <summary>Copies an item of the other file into the open one through the Copy to Other Data File preview (dependencies, rename, skip or replace).</summary>
    internal async Task CopyDiffItemAsync(Workspace mine, Workspace other, DiffItem item)
    {
        if (!ReferenceEquals(Workspaces.Current, mine) || item.Kind == ChangeKind.Removed)
        {
            Notify(NotificationLevel.Warning, Loc.Current[item.Kind == ChangeKind.Removed ? "mods.diff.nothing" : "mods.diff.readOnly"]);
            return;
        }

        IReadOnlyList<(string Type, string Name)> items = [(item.Type, item.Name)];
        var plan = CopyReviewer is { } review ? await review(other, mine, items) : await new CopyToWindow(other, mine, items).AskAsync(this);
        if (plan is not null) await RunCopyAsync(other, mine, plan);
    }

    /// <summary>Asks for the original data file (before the changes), compares the open file with it and writes the differences as a pack.</summary>
    public async Task ExportModPackAsync()
    {
        if (Workspaces.Current is not { } right || await OpenModBaseAsync(right) is not var (left, temp)) return;
        try
        {
            if (await ComputeDiffAsync(left, right) is { } diff) await ExportPackAsync(left, right, diff.Items);
        }
        finally
        {
            left.Dispose();
            if (temp is not null) CopyPackage.TryDelete(temp);
        }
    }

    /// <summary>Writes a pack with the changes that turn <paramref name="left"/> into <paramref name="right"/> for the given items. Unsaved edits of the right file are included.</summary>
    internal async Task ExportPackAsync(Workspace left, Workspace right, IReadOnlyList<DiffItem> items)
    {
        if (items.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.pack.empty"]);
            return;
        }

        var info = PackInfoAsker is { } ask ? await ask(items.Count, right.Name) : await new PackInfoWindow(items.Count, right.Name).AskAsync(this);
        if (info is null || await PickPatchFile(Loc.Current["mods.pack.pickOut"], true, ModPackFile.Extension) is not { } output) return;
        await RunPatchAsync("mods.pack.building", async token =>
        {
            var build = await Task.Run(() =>
            {
                using (left.Lock.Read())
                using (right.Lock.Read())
                    return ModPackBuilder.Build(new Side(left.Data, left.Path), new Side(right.Data, right.Path), items, info, token);
            }, token);
            await Task.Run(() => ModPackFile.Write(output, build.Pack), token);
            var text = Loc.Current.Format("mods.pack.written", ("path", output), ("count", build.Pack.Entries.Count));
            return (true, build.Warnings.Count == 0 ? text : text + " " + Loc.Current.Format("mods.pack.skipped", ("count", build.Warnings.Count), ("first", build.Warnings[0])));
        });
    }

    /// <summary>The G3M mod package (a zip with a patch) for the changes between two saved data files.</summary>
    internal async Task CreateG3MModAsync(Workspace left, Workspace right)
    {
        if (!await UnsavedOkAsync(right, "risky.unsavedExport", "mods.needsSave")) return;
        if (await PickPatchFile(Loc.Current["mods.pickModOut"], true, ".zip") is { } output) await PackageG3MModAsync(left.Path, right, output);
    }

    /// <summary>Reads a pack, shows what it would do to the open file (nothing changes yet), and applies it as one undo step after the owner settled the conflicts.</summary>
    public async Task ApplyModPackAsync(string? packPath = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        packPath ??= await PickPatchFile(Loc.Current["mods.pack.pickPack"], false);
        if (packPath is null) return;
        ReapplyPlan plan;
        try
        {
            plan = await Task.Run(() =>
            {
                var pack = ModPackFile.Read(packPath);
                using (workspace.Lock.Read()) return ModPackApplier.Plan(pack, workspace.Data);
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("mods.failed", ("what", Path.GetFileName(packPath)), ("reason", e.Message)));
            return;
        }

        bool otherFile;
        try
        {
            otherFile = await Task.Run(() => ModPackBuilder.HashFile(workspace.Path) != plan.Pack.Manifest.TargetHash);
        }
        catch (IOException)
        {
            otherFile = true; // the file on disk is gone or locked: it is not the file the pack was made for
        }

        var choices = PackReviewer is { } review ? await review(plan) : await new PackReviewWindow(plan, otherFile).AskAsync(this);
        if (choices is null) return;
        var name = plan.Pack.Manifest.Name;
        var done = await EditWithRiskAsync(workspace, keep => Task.Run(() =>
        {
            using (workspace.Lock.Read()) return ModPackApplier.Build(plan, workspace.Data, choices, keep);
        }), name);
        if (done) Notify(NotificationLevel.Info, Loc.Current.Format("mods.pack.done", ("name", name)));
    }
}
