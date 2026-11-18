namespace G3MStudio.Core.Workspace;

public enum CloseChoice { Save, DoItLater, Cancel, Discard }

/// <summary>What the close prompt is about. Save means Sync changes when <see cref="ProjectMode"/> is set. Journaled is false when the edits are not in the journal, so only Save, Discard and Cancel are offered.</summary>
public sealed record ClosePrompt(string Name, int Changes, bool ProjectMode, bool Journaled = true);

/// <summary>ADR-0011 item 6: before the window closes or the app restarts, unsaved changes get Save, Do it later or Cancel.</summary>
public static class CloseGuard
{
    /// <summary>
    /// True when closing may go on. Save runs <paramref name="save"/> and goes on only if it worked, otherwise the app stays open.
    /// Do it later leaves the edits in the journal. Cancel changes nothing. Edits that are not journaled (after a script, patch or import, or a failed journal write)
    /// are never promised to it: the prompt then offers Save, Discard and Cancel, and is asked even when <paramref name="ask"/> is off.
    /// </summary>
    public static async Task<bool> ConfirmAsync(Workspace? workspace, Func<ClosePrompt, Task<CloseChoice>> askUser, Func<Workspace, Task<bool>> save, bool ask = true)
    {
        if (workspace is null) return true;
        if (workspace.Project is { } project)
        {
            // Project changes are saved on close without asking; if that fails the journal still holds them.
            try
            {
                await project.SaveToProjectAsync();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
            }
        }

        // Settings: closing without the prompt keeps the changes in the journal (files.closeUnsaved = keep).
        var journaled = !workspace.JournalLost;
        if (!workspace.IsDirty || (!ask && journaled)) return true;
        var choice = await askUser(new ClosePrompt(workspace.Name, workspace.History.UnsavedCount, workspace.Project is not null, journaled));
        return choice switch
        {
            CloseChoice.Save => await save(workspace),
            CloseChoice.DoItLater => journaled,
            CloseChoice.Discard => true,
            _ => false,
        };
    }
}
