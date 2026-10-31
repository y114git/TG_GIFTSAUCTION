using Avalonia.Platform.Storage;
using G3MLib.Modding.Diffing;
using G3MLib.Modding.Merging;
using G3MLib.Modding.Patching;
using G3MStudio.Core.Workspace;
using G3MStudio.Core.Notifications;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Creating, applying and validating patch files of the library, on files on disk.</summary>
public sealed partial class MainWindow
{
    private const string PatchExtension = ".g3mpatch";

    /// <summary>Replaces the file dialog (title, save or open; null when cancelled). Tests set it.</summary>
    public Func<string, bool, Task<string?>>? FilePicker { get; set; }

    private Task<string?> PickPatchFile(string title, bool save, string? extension = null) => FilePicker?.Invoke(title, save) ?? PickFileCoreAsync(title, save, extension ?? PatchExtension);

    private async Task<string?> PickFileCoreAsync(string title, bool save, string extension)
    {
        if (save)
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, DefaultExtension = extension.TrimStart('.') });
            return target?.TryGetLocalPath();
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files is [{ } file, ..] ? file.TryGetLocalPath() : null;
    }

    /// <summary>Replaces the dialog that picks several files (title; empty when cancelled). Tests set it.</summary>
    public Func<string, Task<IReadOnlyList<string>>>? FilesPicker { get; set; }

    private async Task<IReadOnlyList<string>> PickPatchFiles(string title)
    {
        if (FilesPicker is { } picker) return await picker(title);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = true });
        return [.. files.Select(f => f.TryGetLocalPath()).OfType<string>()];
    }

    public void ExportMod() => _ = ExportModAsync();

    /// <summary>Makes a G3M mod zip from the open (saved) data file: a patch against the chosen original plus a generated mod_config.json to edit later.</summary>
    public async Task ExportModAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.needsSave"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["mods.pickOriginal"], false) is not { } original
            || await PickPatchFile(Loc.Current["mods.pickModOut"], true) is not { } output) return;
        await RunPatchAsync("mods.packaging", async token =>
        {
            var patch = Path.Combine(Directory.CreateTempSubdirectory("g3mstudio-mod-").FullName, "mod.g3mpatch");
            try
            {
                var result = await PatchService.CreatePatchAsync(original, workspace.Path, patch, cancellationToken: token);
                if (!result.Success) return (false, result.Error);
                var game = workspace.Data.GeneralInfo?.Name?.Content ?? Path.GetFileNameWithoutExtension(workspace.Path);
                ModPackage.Write(output, patch, workspace.Name, game, Path.GetFileName(workspace.Path));
                return (true, Loc.Current.Format("mods.packaged", ("path", output)));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(patch)!, true);
            }
        });
    }

    public void CompareFiles() => _ = CompareFilesAsync();

    /// <summary>Writes a report of what differs between the original data file and another one (the open file, or one the user picks).</summary>
    public async Task CompareFilesAsync()
    {
        var original = await PickPatchFile(Loc.Current["mods.pickOriginal"], false);
        var other = original is null ? null : Workspaces.Current?.Path ?? await PickPatchFile(Loc.Current["mods.pickData"], false);
        if (original is null || other is null || await PickPatchFile(Loc.Current["mods.pickReport"], true) is not { } report) return;
        await RunPatchAsync("mods.comparing", async token =>
        {
            var result = await DiffService.CompareAsync(original, other, report, DiffReportMode.Standard, null, token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.compared", ("path", report), ("count", result.DifferenceCount)) : result.Error);
        });
    }

    public void MergePatches() => _ = MergePatchesAsync();

    /// <summary>Merges two or more patches made from the same original into one patch; conflicts that cannot be merged by code or property are counted in the message.</summary>
    public async Task MergePatchesAsync()
    {
        var original = Workspaces.Current?.Path ?? await PickPatchFile(Loc.Current["mods.pickOriginal"], false);
        if (original is null) return;
        var patches = await PickPatchFiles(Loc.Current["mods.pickPatches"]);
        if (patches.Count < 2)
        {
            if (patches.Count > 0) Notify(NotificationLevel.Warning, Loc.Current["mods.needTwo"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["mods.pickPatchOut"], true) is not { } output) return;
        await RunPatchAsync("mods.merging", async token =>
        {
            var result = await MergeService.MergePatchesAsync(original, [.. patches], new MergeOptions { OutputPath = output, UseCodeMerge = true, UsePropertyMerge = true }, token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.merged", ("path", output), ("merged", result.AutoMerged), ("conflicts", result.TotalConflicts)) : result.Error);
        });
    }

    public void CreatePatch() => _ = CreatePatchAsync();

    public void ApplyPatch() => _ = ApplyPatchAsync();

    public void ValidatePatch() => _ = ValidatePatchAsync();

    /// <summary>The open (saved) data file is the modified one; the user picks the original it differs from.</summary>
    public async Task CreatePatchAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.needsSave"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["mods.pickOriginal"], false) is not { } original
            || await PickPatchFile(Loc.Current["mods.pickPatchOut"], true) is not { } output) return;
        await RunPatchAsync("mods.creating", async token =>
        {
            var result = await PatchService.CreatePatchAsync(original, workspace.Path, output, cancellationToken: token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.created", ("path", output)) : result.Error);
        });
    }

    /// <summary>Patches the open data file, or one the user picks, into a new file; the original is never overwritten.</summary>
    public async Task ApplyPatchAsync()
    {
        var data = Workspaces.Current?.Path ?? await PickPatchFile(Loc.Current["mods.pickData"], false);
        if (data is null || await PickPatchFile(Loc.Current["mods.pickPatch"], false) is not { } patch
            || await PickPatchFile(Loc.Current["mods.pickDataOut"], true) is not { } output) return;
        if (Path.GetFullPath(output) == Path.GetFullPath(data))
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.sameFile"]);
            return;
        }

        await RunPatchAsync("mods.applying", async token =>
        {
            var result = await PatchService.ApplyPatchAsync(data, patch, output, cancellationToken: token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.applied", ("path", output)) : result.Error);
        });
    }

    public async Task ValidatePatchAsync()
    {
        if (await PickPatchFile(Loc.Current["mods.pickPatch"], false) is not { } patch) return;
        await RunPatchAsync("mods.validating", async token =>
        {
            var result = await PatchService.ValidatePatchAsync(patch, Workspaces.Current?.Path, cancellationToken: token);
            return (result.Success, result.Success ? Loc.Current.Format("mods.valid", ("name", Path.GetFileName(patch))) : result.Error);
        });
    }

    /// <summary>Runs one patch operation behind the progress overlay. The delegate returns whether it worked and the message or the error text.</summary>
    private async Task RunPatchAsync(string titleKey, Func<CancellationToken, Task<(bool Success, string? Text)>> run)
    {
        var what = Loc.Current[titleKey];
        using var source = _exporting = new CancellationTokenSource();
        (_exportName, _exportDone, _exportTotal) = (what, 0, 0);
        ShowProgress();
        try
        {
            var (ok, text) = await run(source.Token);
            Notify(ok ? NotificationLevel.Info : NotificationLevel.Error, ok ? text ?? string.Empty : Loc.Current.Format("mods.failed", ("what", what), ("reason", text ?? Loc.Current["mods.unknownError"])));
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["mods.cancelled"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("mods.failed", ("what", what), ("reason", e.Message)));
        }
        finally
        {
            _exporting = null;
            _exportName = null;
            Progress.IsVisible = false;
            UpdateTexts();
        }
    }
}
