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
            var folder = Directory.CreateTempSubdirectory("g3mstudio-mod-").FullName;
            var patch = Path.Combine(folder, "mod.g3mpatch");
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
                Directory.Delete(folder, true);
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

    public void ApplyMod() => _ = ApplyModAsync();

    public void ApplyPatchOpen() => _ = ApplyPatchOpenAsync();

    public void ApplyModOpen() => _ = ApplyModOpenAsync();

    /// <summary>
    /// Applies a patch to the open data file in memory. Like an import it is a bulk change: it needs a file without unsaved edits, cannot be undone,
    /// and Revert to Saved is the way back. A patch that fails after it started may leave the file partly changed, so the failure says so.
    /// </summary>
    public async Task ApplyPatchOpenAsync(string? givenPatch = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        if ((givenPatch ?? await PickPatchFile(Loc.Current["mods.pickPatch"], false)) is not { } patch) return;
        var name = Path.GetFileName(patch);
        if (!await Confirm(Loc.Current["mods.openTitle"], Loc.Current.Format("mods.openAsk", ("name", name)))) return;
        await RunPatchAsync("mods.applyingOpen", async token =>
        {
            PatchApplyResult? result = null;
            // ApplyBulkAsync runs the action on a pool thread, where blocking cannot deadlock.
            await workspace.ApplyBulkAsync(() => result = PatchService.ApplyPatchAsync(workspace.Data, patch, new PatchApplyOptions { DataFilePath = workspace.Path }, null, token).GetAwaiter().GetResult());
            return result is { Success: true } ? (true, Loc.Current.Format("mods.appliedOpen", ("name", name))) : (false, (result?.Error ?? Loc.Current["mods.unknownError"]) + " " + Loc.Current["mods.openPartly"]);
        });
    }

    /// <summary>Applies the patches of a mod ZIP that target the open data file to it in memory, in order, as one bulk change (see <see cref="ApplyPatchOpenAsync"/>).</summary>
    public async Task ApplyModOpenAsync(string? zipPath = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        zipPath ??= await PickPatchFile(Loc.Current["mods.pickMod"], false, ".zip");
        if (zipPath is null) return;
        ModInfo mod;
        try
        {
            mod = ModImport.Read(zipPath);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("mods.modInvalid", ("reason", e.Message)));
            return;
        }

        var file = Path.GetFileName(workspace.Path);
        var usable = ModImport.ForDataFile(mod, workspace.Path).Where(p => p.Supported).ToList();
        if (usable.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("mods.modNoMatch", ("name", mod.Name), ("file", file)));
            return;
        }

        if (!await Confirm(Loc.Current["mods.modTitle"], Loc.Current.Format("mods.openModAsk", ("name", mod.Name), ("version", mod.Version), ("count", usable.Count), ("file", file)))) return;
        await RunPatchAsync("mods.applyingOpen", async token =>
        {
            var work = Directory.CreateTempSubdirectory("g3mstudio-mod-").FullName;
            try
            {
                string? error = null;
                await workspace.ApplyBulkAsync(() =>
                {
                    foreach (var (patch, index) in usable.Select((p, i) => (p, i)))
                    {
                        var payload = ModImport.Extract(zipPath, patch, Directory.CreateDirectory(Path.Combine(work, index.ToString(System.Globalization.CultureInfo.InvariantCulture))).FullName);
                        // Runs on the pool thread of ApplyBulkAsync, where blocking cannot deadlock.
                        var result = PatchService.ApplyPatchAsync(workspace.Data, payload, new PatchApplyOptions { DataFilePath = workspace.Path }, null, token).GetAwaiter().GetResult();
                        if (result.Success) continue;
                        error = result.Error;
                        return;
                    }
                });
                return error is null ? (true, Loc.Current.Format("mods.appliedOpen", ("name", mod.Name))) : (false, error + " " + Loc.Current["mods.openPartly"]);
            }
            finally
            {
                try
                {
                    Directory.Delete(work, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // The folder is in the temp folder; a payload that is still open is cleaned up with it later.
                }
            }
        });
    }

    /// <summary>Draws the glyphs of a font again from a TrueType or OpenType file at the font's size (one undo step). The size is the font's EmSize, set in its property list first.</summary>
    public async Task ReplaceFontAsync(string name, string? file = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        file ??= await PickPatchFile(Loc.Current["fonts.pickFile"], false, ".ttf");
        if (file is null) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(file);
            if (!FontReplaceCommand.LooksLikeFont(bytes) || bytes.Length > FontReplaceCommand.MaxFontBytes)
            {
                Notify(NotificationLevel.Error, Loc.Current["fonts.notAFont"]);
                return;
            }

            var size = workspace.Data.Fonts.FirstOrDefault(f => f.Name.Content == name)?.EmSize ?? 0;
            if (!await Confirm(Loc.Current["fonts.replace"], Loc.Current.Format("fonts.replaceAsk", ("name", name), ("file", Path.GetFileName(file)), ("size", size.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture))))) return;
            var command = new FontReplaceCommand(name, bytes);
            await MemoryLimit.RetryOnOutOfMemoryAsync(() => Task.Run(() => { using (workspace.Lock.Read()) command.Prepare(workspace.Data); return true; }), Memory.Relieve);
            workspace.Edit(command);
            Notify(NotificationLevel.Info, Loc.Current.Format("fonts.replaced", ("name", name)));
        }
        catch (OutOfMemoryException)
        {
            NotifyOutOfMemory("memory.opFont");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("fonts.replaceFailed", ("reason", e.Message)));
        }
    }

    /// <summary>
    /// Applies a G3M mod ZIP: reads its mod_config.json, takes the patch operations whose target is the data file in use (the open one, or one picked), asks, and applies
    /// them in order into a new file. The data file is never overwritten; the result can be opened from the notice. Payloads other than .g3mpatch are reported, not run.
    /// </summary>
    public async Task ApplyModAsync(string? zipPath = null)
    {
        zipPath ??= await PickPatchFile(Loc.Current["mods.pickMod"], false, ".zip");
        if (zipPath is null) return;
        ModInfo mod;
        try
        {
            mod = ModImport.Read(zipPath);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("mods.modInvalid", ("reason", e.Message)));
            return;
        }

        var data = Workspaces.Current?.Path ?? await PickPatchFile(Loc.Current["mods.pickData"], false);
        if (data is null) return;
        var file = Path.GetFileName(data);
        var matching = ModImport.ForDataFile(mod, data);
        if (matching.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("mods.modNoMatch", ("name", mod.Name), ("file", file)));
            return;
        }

        var usable = matching.Where(p => p.Supported).ToList();
        if (usable.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("mods.modUnsupported", ("name", mod.Name), ("file", file)));
            return;
        }

        var skipped = matching.Count - usable.Count;
        if (!await Confirm(Loc.Current["mods.modTitle"], Loc.Current.Format("mods.modAsk", ("name", mod.Name), ("version", mod.Version), ("count", usable.Count), ("file", file)) + (skipped > 0 ? " " + Loc.Current.Format("mods.modSkipped", ("count", skipped)) : ""))) return;
        if (await PickPatchFile(Loc.Current["mods.pickDataOut"], true) is not { } output) return;
        if (Path.GetFullPath(output) == Path.GetFullPath(data))
        {
            Notify(NotificationLevel.Warning, Loc.Current["mods.sameFile"]);
            return;
        }

        await RunPatchAsync("mods.applyingMod", async token =>
        {
            var work = Directory.CreateTempSubdirectory("g3mstudio-mod-").FullName;
            try
            {
                var current = data;
                for (var i = 0; i < usable.Count; i++)
                {
                    var payload = ModImport.Extract(zipPath, usable[i], work);
                    var next = i == usable.Count - 1 ? output : Path.Combine(work, "step" + i + ".win");
                    var result = await PatchService.ApplyPatchAsync(current, payload, next, cancellationToken: token);
                    if (!result.Success) return (false, result.Error);
                    current = next;
                }

                return (true, Loc.Current.Format("mods.modApplied", ("name", mod.Name), ("path", output)));
            }
            finally
            {
                Directory.Delete(work, true);
            }
        }, output);
    }

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
    public async Task ApplyPatchAsync(string? givenPatch = null)
    {
        var data = Workspaces.Current?.Path ?? await PickPatchFile(Loc.Current["mods.pickData"], false);
        if (data is null || (givenPatch ?? await PickPatchFile(Loc.Current["mods.pickPatch"], false)) is not { } patch
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
    private async Task RunPatchAsync(string titleKey, Func<CancellationToken, Task<(bool Success, string? Text)>> run, string? openResult = null)
    {
        var what = Loc.Current[titleKey];
        using var source = _exporting = new CancellationTokenSource();
        (_exportName, _exportDone, _exportTotal) = (what, 0, 0);
        ShowProgress();
        try
        {
            var (ok, text) = await run(source.Token);
            if (ok && openResult is not null) Notifications.Post(NotificationLevel.Info, text ?? string.Empty, Loc.Current["mods.openResult"], () => _ = OpenAsync(openResult));
            else Notify(ok ? NotificationLevel.Info : NotificationLevel.Error, ok ? text ?? string.Empty : Loc.Current.Format("mods.failed", ("what", what), ("reason", text ?? Loc.Current["mods.unknownError"])));
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["mods.cancelled"]);
        }
        catch (OutOfMemoryException)
        {
            Memory.Relieve();
            Notify(NotificationLevel.Error, Loc.Current.Format("memory.failed", ("name", what)));
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
