using G3MStudio.Core.ModPack;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Prompts with "Don't ask again" (one list: <see cref="Prompts"/>), the timeline moment copy question, run options migration and the mod base cache commands.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the moment copy question; the answer is copy the code (or share it) and whether to remember it, null for cancel. Tests set it.</summary>
    public Func<Task<(bool CopyCode, bool Remember)?>>? MomentCopyAsker { get; set; }

    /// <summary>The last background copy into the mod base cache, so a test can wait for it.</summary>
    internal Task ModBaseTask { get; private set; } = Task.CompletedTask;

    // Data files that were opened while the remembered original was another file; asked once, at the first mod command.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ModBaseEntry> _modBaseChanged = new();

    /// <summary>
    /// A yes or no question that can be silenced. A remembered answer is used without asking; otherwise the dialog has a "Don't ask again" box
    /// that writes the answer into the settings under <paramref name="promptId"/>. Only ids of <see cref="Prompts.All"/> are remembered.
    /// </summary>
    internal async Task<bool> Confirm(string promptId, string title, string message)
    {
        var choice = Prompts.Get(Workspaces.Settings.Current, promptId);
        if (choice != PromptChoice.Ask) return choice == PromptChoice.Yes;
        if (Confirmer is { } test) return await test(title, message);
        var (answer, remember) = await ProjectDialogs.AskConfirmRememberAsync(this, title, message);
        if (remember) Workspaces.Settings.Update(s => Prompts.Set(s, promptId, answer ? PromptChoice.Yes : PromptChoice.No));
        return answer;
    }

    private async Task<CloseChoice> AskCloseAsync(ClosePrompt prompt)
    {
        var window = new ClosePromptWindow(prompt);
        var choice = await window.AskAsync(this);
        if (window.DontAskAgain && choice == CloseChoice.DoItLater) Workspaces.Settings.Update(s => Prompts.Set(s, Prompts.CloseUnsaved, PromptChoice.Yes));
        return choice;
    }

    public void ResetPrompts()
    {
        Workspaces.Settings.Update(Prompts.ResetAll);
        Notify(NotificationLevel.Info, Loc.Current["prompts.resetDone"]);
    }

    /// <summary>
    /// Copies a moment of a timeline. A copy asks whether to copy the code too (independent entries) or share the code entries; the answer can be remembered per option.
    /// A move (cut) always keeps the code entries, so it asks nothing.
    /// </summary>
    internal async Task CopyTimelineMomentAsync(Workspace workspace, string from, int index, string to, bool cut)
    {
        var copyCode = false;
        if (!cut)
        {
            var settings = Workspaces.Settings.Current;
            if (Prompts.Get(settings, Prompts.TimelineCopyCode) == PromptChoice.Yes) copyCode = true;
            else if (Prompts.Get(settings, Prompts.TimelineCopyShare) != PromptChoice.Yes)
            {
                var answer = MomentCopyAsker is { } ask ? await ask() : await ProjectDialogs.AskMomentCopyAsync(this);
                if (answer is not { } picked) return;
                copyCode = picked.CopyCode;
                if (picked.Remember) Workspaces.Settings.Update(s => Prompts.Set(s, copyCode ? Prompts.TimelineCopyCode : Prompts.TimelineCopyShare, PromptChoice.Yes));
            }
        }

        try
        {
            workspace.Edit(new TimelineMomentCopyCommand(from, index, to, cut, copyCode));
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    /// <summary>After the owner approved a run file next to the data file: offers to move it into Studio's folder, where it needs no approval.</summary>
    private async Task OfferRunOptionsMigrationAsync(string dataFile)
    {
        if (await Confirm(Prompts.MigrateRunOptions, Loc.Current["live.migrateTitle"], Loc.Current["live.migrateAsk"])) await MigrateRunOptionsForAsync(dataFile);
    }

    private async Task MigrateRunOptionsForAsync(string dataFile)
    {
        try
        {
            await AskRunFileTrustAsync(dataFile); // a file beside the data file is copied only after its hash is approved
            var done = RunPlan.Migrate(Workspaces.Settings.Current, Workspaces.UserData, dataFile);
            Notify(done ? NotificationLevel.Info : NotificationLevel.Warning, Loc.Current[done ? "live.migrated" : "live.nothingToMigrate"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    public void MigrateRunOptions()
    {
        if (Workspaces.Current is { } workspace) _ = MigrateRunOptionsForAsync(workspace.Path);
    }

    /// <summary>True for the remembered original, false for remembering the file as it is now.</summary>
    private Task<bool> AskUseRememberedBaseAsync() =>
        Confirmer?.Invoke(Loc.Current["modBase.changedTitle"], Loc.Current["modBase.changedAsk"]) ?? ProjectDialogs.AskUseRememberedAsync(this, Loc.Current["modBase.changedTitle"], Loc.Current["modBase.changedAsk"]);

    private ModBaseCache ModBase => new(Workspaces.UserData);

    private long ModBaseBytes => Workspaces.Settings.Current.ModBaseMaxMb * 1024L * 1024L;

    /// <summary>Keeps a copy of a data file that was just opened as its mod base, in the background, unless it has one already or the setting is off.</summary>
    private void RememberModBaseOnOpen(string path)
    {
        if (!Workspaces.Settings.Current.ModBaseEnabled) return;
        var (cache, cap) = (ModBase, ModBaseBytes);
        var key = Path.GetFullPath(path);
        _modBaseChanged.TryRemove(key, out _);
        ModBaseTask = Task.Run(() =>
        {
            try
            {
                if (cache.Remember(path, path, cap) is { } entry && ModBaseCache.Differs(entry, path)) _modBaseChanged[key] = entry;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the cache is a convenience: Export Mod Pack asks for the original when it has no copy
            }
        });
    }

    /// <summary>Command "Remember this file as the mod base": the data file on disk replaces the copy of the original.</summary>
    public void RememberModBase() => _ = RememberModBaseAsync();

    public async Task RememberModBaseAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        var (cache, cap) = (ModBase, ModBaseBytes);
        try
        {
            var entry = await Task.Run(() => cache.Remember(workspace.Path, workspace.Path, cap, true));
            Notify(entry is null ? NotificationLevel.Warning : NotificationLevel.Info, Loc.Current.Format(entry is null ? "modBase.tooBig" : "modBase.remembered", ("name", workspace.Name)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    public void ClearModBase()
    {
        var done = ModBase.Clear();
        Notify(done ? NotificationLevel.Info : NotificationLevel.Error, Loc.Current[done ? "modBase.cleared" : "modBase.clearFailed"]);
    }

    /// <summary>
    /// The original data file for Export Mod Pack, opened for reading. The folder is a temporary one to delete after the file is closed.
    /// </summary>
    private async Task<(Workspace Base, string? TempFolder)?> OpenModBaseAsync(Workspace current)
    {
        if (await ResolveModBaseAsync(current) is not var (path, temp)) return null;
        if (await OpenDiffPathAsync(path) is { } workspace) return (workspace, temp);
        if (temp is not null) CopyPackage.TryDelete(temp);
        return null;
    }

    /// <summary>
    /// The path of the original data file for a mod command. Uses the remembered copy when it is intact; otherwise asks the user for the original,
    /// checks it against the remembered hash when there is one, and remembers it again. A temporary folder (the unpacked copy) is deleted by the caller.
    /// </summary>
    private async Task<(string Path, string? TempFolder)?> ResolveModBaseAsync(Workspace current)
    {
        var settings = Workspaces.Settings.Current;
        var cache = ModBase;
        await ModBaseTask; // the check against the file on disk runs there
        var known = settings.ModBaseEnabled ? cache.Find(current.Path) : null;
        if (known is not null && _modBaseChanged.TryRemove(Path.GetFullPath(current.Path), out var stale) && stale == known)
        {
            if (!await AskUseRememberedBaseAsync())
            {
                var cap = ModBaseBytes;
                try
                {
                    known = await Task.Run(() => cache.Remember(current.Path, current.Path, cap, true)) ?? known;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Notify(NotificationLevel.Error, e.Message);
                    return null;
                }
            }
        }

        if (known is not null)
        {
            var folder = Directory.CreateTempSubdirectory("g3mstudio-base-").FullName;
            if (await Task.Run(() => cache.TryExtract(known, folder)) is { } copy) return (copy, folder);
            CopyPackage.TryDelete(folder);
            Notify(NotificationLevel.Warning, Loc.Current["modBase.lost"]);
        }

        if (await PickPatchFile(Loc.Current["mods.pack.pickBase"], false) is not { } picked) return null;
        if (known is not null && await Task.Run(() => ModPackBuilder.HashFile(picked)) != known.Hash)
        {
            Notify(NotificationLevel.Error, Loc.Current["modBase.mismatch"]);
            return null;
        }

        if (settings.ModBaseEnabled)
        {
            var cap = ModBaseBytes;
            try
            {
                await Task.Run(() => cache.Remember(current.Path, picked, cap, true));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // not saved again; the command still works from the picked file
            }
        }

        return (picked, null);
    }
}
