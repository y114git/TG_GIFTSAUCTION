using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

public partial class MainWindow
{
    /// <summary>
    /// Applies an edit that may be refused with a <see cref="RiskyRefusalException"/>. The owner is then warned and may go on (unless risky actions are set to refuse),
    /// and the edit is built again with its force flag set. Returns true when an edit was made.
    /// </summary>
    internal async Task<bool> EditOrForceAsync(Workspace workspace, Func<bool, EditCommand> build, string name)
    {
        try
        {
            workspace.Edit(build(false));
            return true;
        }
        catch (RiskyRefusalException e)
        {
            if (!await AskRiskyAsync(e, name)) return false;
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Error, e.Message);
            return false;
        }

        try
        {
            workspace.Edit(build(true));
            return true;
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Error, e.Message);
            return false;
        }
    }

    internal RiskChoice RiskNow() => RiskPolicy.Of(Workspaces.Settings.Current);

    /// <summary>True when the owner agrees to go on after the refusal, or has set Studio to go on without asking.</summary>
    internal async Task<bool> AskRiskyAsync(RiskyRefusalException refusal, string name)
    {
        switch (RiskPolicy.Of(Workspaces.Settings.Current))
        {
            case RiskChoice.Allow:
                return true;
            case RiskChoice.Ask:
                return await Confirm(Prompts.RiskyAction, Loc.Current.Format(refusal.TitleKey, ("name", name)), Loc.Current.Format(refusal.BodyKey, ("reason", refusal.Message), ("name", name)));
            default:
                Notify(NotificationLevel.Error, refusal.Message);
                return false;
        }
    }

    /// <summary>
    /// True when the file has no unsaved edits, or the owner agrees to go on with them. <paramref name="bodyKey"/> says what happens to them:
    /// a change that cannot be undone drops the undo stack, an export leaves them out.
    /// </summary>
    internal async Task<bool> UnsavedOkAsync(Workspace workspace, string bodyKey, string refuseKey = "transfer.needsSave")
    {
        if (!workspace.History.IsDirty) return true;
        switch (RiskPolicy.Of(Workspaces.Settings.Current))
        {
            case RiskChoice.Allow:
                return true;
            case RiskChoice.Ask:
                return await Confirm(Prompts.UnsavedChanges, Loc.Current["risky.unsavedTitle"], Loc.Current[bodyKey]);
            default:
                Notify(NotificationLevel.Warning, Loc.Current[refuseKey]);
                return false;
        }
    }
}
