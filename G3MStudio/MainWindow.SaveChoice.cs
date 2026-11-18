using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The Save command (menu, Ctrl+S, toolbar): the first-save question and the remembered answer. Close, Run and autosave call <see cref="SaveAsync"/> and never ask.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the Save question; the argument is whether the file has a project. Returns the answer, or null for cancel. Tests set it.</summary>
    public Func<bool, Task<(bool? Sync, bool Remember)?>>? SaveAsker { get; set; }

    private bool _askingSave;

    public void Save() => _ = SaveCommandAsync();

    private async Task SaveCommandAsync()
    {
        // Named before the question: the user may switch files while it is open.
        var name = Workspaces.Current is { } open ? Path.GetFileName(open.Path) : "";
        try { await AskAndSaveAsync(); }
        catch (Exception e)
        {
            // Nothing awaits the Save command: a failure of the question or of the save is reported here instead of going unobserved.
            Notify(NotificationLevel.Error, Loc.Current.Format("save.failed", ("name", name), ("reason", e.Message)));
        }
    }

    private async Task AskAndSaveAsync()
    {
        // A second Ctrl+S while the question is open must not stack another one.
        if (Workspaces.Current is not { } workspace || _askingSave) return;
        var project = workspace.Project is not null;
        var id = project ? Prompts.SaveProject : Prompts.SaveFirst;
        var remembered = Prompts.Get(Workspaces.Settings.Current, id);
        bool? sync = project && remembered != PromptChoice.Ask ? remembered == PromptChoice.Yes : null;
        if (remembered == PromptChoice.Ask)
        {
            _askingSave = true;
            (bool? Sync, bool Remember)? answer;
            try
            {
                answer = SaveAsker is { } test ? await test(project) : await ProjectDialogs.AskSaveAsync(this, project);
            }
            finally
            {
                _askingSave = false;
            }

            // The user may have switched files while the question was open: only the save is skipped, the remembered answer still counts.
            if (answer is not { } picked) return;
            sync = picked.Sync;
            if (picked.Remember) Workspaces.Settings.Update(s => Prompts.Set(s, id, project && sync != true ? PromptChoice.No : PromptChoice.Yes));
            if (!ReferenceEquals(Workspaces.Current, workspace)) return;
        }

        await SaveAsync(sync);
    }
}
