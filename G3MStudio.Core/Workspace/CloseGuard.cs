namespace G3MStudio.Core.Workspace;

public enum CloseChoice { Save, DoItLater, Cancel }

/// <summary>What the close prompt is about. Save means Sync changes when <see cref="ProjectMode"/> is set.</summary>
public sealed record ClosePrompt(string Name, int Changes, bool ProjectMode);

/// <summary>ADR-0010 item 6: before the window closes or the app restarts, unsaved changes get Save, Do it later or Cancel.</summary>
public static class CloseGuard
{
    /// <summary>
    /// True when closing may go on. Save runs <paramref name="save"/> and goes on only if it worked, otherwise the app stays open.
    /// Do it later leaves the edits in the journal. Cancel changes nothing.
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
        if (!workspace.IsDirty || !ask) return true;
        var choice = await askUser(new ClosePrompt(workspace.Name, workspace.History.UnsavedCount, workspace.Project is not null));
        return choice switch
        {
            CloseChoice.Save => await save(workspace),
            CloseChoice.DoItLater => true,
            _ => false,
        };
    }
}
