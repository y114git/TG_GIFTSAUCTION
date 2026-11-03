using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Running .csx scripts on the open data file.</summary>
public sealed partial class MainWindow
{
    public void RunScript() => _ = RunScriptAsync();

    /// <summary>Asks for a few lines of C# and runs them as a script with the same globals as a .csx file (UTMT-APP-05).</summary>
    public void RunCommand() => _ = RunCommandAsync();

    public async Task RunCommandAsync()
    {
        if (await ProjectDialogs.AskTextAsync(this, Loc.Current["scripts.commandTitle"], Loc.Current["scripts.commandLabel"], "", true) is not { Length: > 0 } code) return;
        var path = Path.Combine(Path.GetTempPath(), "command-" + Guid.NewGuid().ToString("N") + ".csx");
        await File.WriteAllTextAsync(path, code);
        try { await RunScriptAsync(path); }
        finally { File.Delete(path); }
    }

    /// <summary>Lists the .csx files in the Scripts folder next to the program, subfolders included, and runs the chosen one (UTMT-SCRIPT-01).</summary>
    public void RunScriptFromFolder() => _ = RunScriptFromFolderAsync();

    public async Task RunScriptFromFolderAsync()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Scripts");
        var files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.csx", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToList() : [];
        if (files.Count == 0)
        {
            Notify(NotificationLevel.Info, Loc.Current.Format("scripts.none", ("folder", folder)));
            return;
        }

        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["scripts.title"], Loc.Current["scripts.pick"], [.. files.Select(f => Path.GetRelativePath(folder, f))]) is { } at)
            await RunScriptAsync(files[at]);
    }

    /// <summary>
    /// Asks for a .csx file, warns what a script can do, and runs it as a bulk change (it cannot be undone; Revert to Saved is the way back).
    /// It needs a file without unsaved edits, like an import.
    /// </summary>
    public async Task RunScriptAsync(string? chosen = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        if ((chosen ?? await PickPatchFile(Loc.Current["scripts.pick"], false)) is not { } path) return;
        var name = Path.GetFileName(path);
        if (!await Confirm(Loc.Current["scripts.title"], Loc.Current.Format("scripts.confirm", ("name", name)))) return;
        Notify(NotificationLevel.Info, Loc.Current.Format("scripts.running", ("name", name)));
        var host = NewScriptHost(name);
        // The script holds the data lock and waits for the UI thread when it asks something, so nothing on the window may take that lock meanwhile.
        IsEnabled = false;
        try
        {
            var error = await ScriptRunner.RunAsync(workspace, path,
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Info, text)),
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, text)),
                host);
            if (error is not null) Notify(NotificationLevel.Error, Loc.Current.Format("scripts.failed", ("name", name), ("reason", error)));
            else if (host.FinishedMessage) Notify(NotificationLevel.Info, Loc.Current.Format("scripts.done", ("name", name)));
            if (host.Selections.Count > 0 && UsageSearch.TypeKeyOf(host.Selections[^1]) is { } type) OpenEntry(ResourceTypes.KeyOf(type, ResourceTypes.NameOf(host.Selections[^1])));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or OperationCanceledException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("scripts.failed", ("name", name), ("reason", e.Message)));
        }
        finally
        {
            IsEnabled = true;
            _exportName = null;
            Progress.IsVisible = false;
        }
    }

    // The script runs on a pool thread and holds the write lock, so a prompt is shown on the UI thread and the script waits for the answer. The answer never touches the data.
    private ScriptHost NewScriptHost(string name)
    {
        static T OnUi<T>(Func<Task<T>> work) => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(work).GetAwaiter().GetResult();
        return new ScriptHost
        {
            Question = text => OnUi(() => Confirm(Loc.Current["scripts.title"], text)),
            Input = (title, label, current, multiline) => OnUi(() => ProjectDialogs.AskTextAsync(this, title, label, current, multiline)),
            ChooseDirectory = () => OnUi(() => PickFolderAsync(Loc.Current["scripts.pickFolder"])),
            LoadFile = (extension, _) => OnUi(() => PickPatchFile(Loc.Current["scripts.pickFile"], false, extension.Length > 0 ? extension : null)),
            SaveFile = (extension, _) => OnUi(() => PickPatchFile(Loc.Current["scripts.pickSave"], true, extension.Length > 0 ? extension : null)),
            Progress = (label, status, value, maximum) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _exportName = label ?? _exportName ?? name;
                (_exportDone, _exportTotal) = (value, (int)Math.Max(maximum, 0));
                ShowProgress();
            }),
        };
    }
}
