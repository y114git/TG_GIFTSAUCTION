using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Delete for an entry of the Functions and Variables lists of the Explorer.</summary>
public sealed partial class MainWindow
{
    private static string Places(Workspace workspace, IReadOnlyList<UsageHit> hits) =>
        string.Join(Environment.NewLine, hits.Take(8).Select(h => $"  {h.OwnerType} {workspace.Nicknames.OwnerLabel(h.OwnerType, h.OwnerName)}: {h.Where}")) + (hits.Count > 8 ? Environment.NewLine + $"  ... +{hits.Count - 8}" : "");

    /// <summary>
    /// A function with a script of its own goes with that script and its code entry; a function declared in a larger script is cut out of that script's source and the script is
    /// compiled again. The list entry goes too when no other code calls the function; else it stays, because the calls still name it.
    /// </summary>
    internal async Task DeleteFunctionAsync(Workspace workspace, string name)
    {
        FunctionDeletePlan plan;
        using (workspace.Lock.Read()) plan = FunctionDeletion.Plan(workspace.Data, name);
        if (plan.Kind == FunctionDeletePlan.Way.Refused)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format(plan.Reason == "Code" ? "explorer.deleteFunctionNoCode" : "explorer.deleteFunctionNoScript", ("name", name)));
            return;
        }

        string? text = null;
        if (plan.Kind == FunctionDeletePlan.Way.Inside)
        {
            var context = workspace.DecompileContext();
            try
            {
                using (workspace.Lock.Read()) text = FunctionSource.Remove(CodeText.Decompile(workspace.Data, plan.Entry, context), UsageSearch.CodeNameOf(name));
            }
            catch (Exception e) when (e is InvalidOperationException or G3MLib.Analyzer.Decompiler.DecompilerException)
            {
                Notify(NotificationLevel.Error, e.Message);
                return;
            }

            if (text is null)
            {
                Notify(NotificationLevel.Warning, Loc.Current.Format("explorer.deleteFunctionNoSource", ("name", name), ("entry", plan.Entry)));
                return;
            }
        }

        var all = await UsageSearch.FindAsync(workspace, "functions", name);
        // The function's own code goes away with it, and its declaration in the script that holds it is no use.
        var hits = all.Where(h => h.OwnerName != name && h.OwnerName != plan.Entry).ToList();
        // A call left in the main code of the script that held the declaration is a use too.
        if (text is not null && UsageSearch.FindUse(text, UsageSearch.CodeNameOf(name), 0) >= 0) hits.Add(new UsageHit("code", plan.Entry, "call"));
        var places = Places(workspace, hits);
        var title = Loc.Current.Format("explorer.deleteFunctionTitle", ("name", name));
        var what = plan.Kind == FunctionDeletePlan.Way.OwnScript
            ? Loc.Current.Format("explorer.deleteFunctionOwn", ("name", name), ("script", plan.Script), ("entry", plan.Entry))
            : Loc.Current.Format("explorer.deleteFunctionInside", ("name", name), ("entry", plan.Entry));
        var uses = hits.Count == 0 ? Loc.Current["explorer.deleteFunctionUnused"] : Loc.Current.Format("explorer.deleteFunctionUsed", ("count", hits.Count), ("places", places));
        if (!await Confirm(Prompts.DeleteResource, title, what + Environment.NewLine + Environment.NewLine + uses, "common.delete")) return;

        var drop = hits.Count == 0;
        if (!await EditOrForceAsync(workspace, keep => new GroupCommand(
        [
            .. plan.Kind == FunctionDeletePlan.Way.OwnScript
                ? new EditCommand[] { new ClearReferencesCommand("scripts", plan.Script, keep), new ResourceListCommand("scripts", ResourceListCommand.Delete, plan.Script) }
                : [new EditCodeCommand(plan.Entry, text!)],
            .. drop ? [new ResourceListCommand("functions", ResourceListCommand.Delete, name)] : Array.Empty<EditCommand>(),
        ]), name)) return;

        // The code entry, the script and their tabs are gone with the delete.
        string[] gone = [ResourceTypes.KeyOf("code", name), ResourceTypes.KeyOf("scripts", name), ResourceTypes.KeyOf("scripts", plan.Script), ResourceTypes.KeyOf("functions", name)];
        foreach (var tab in Docking.Tabs.Tabs.Where(t => t.Key is { } key && gone.Contains(key)).ToList()) Docking.CloseTab(tab.Id, true);
    }

    /// <summary>An unused variable entry can go: no code names it, so no instruction is left without its entry. A used one stays, and the message says where it is used.</summary>
    internal async Task DeleteVariableAsync(Workspace workspace, string name)
    {
        var hits = await UsageSearch.FindAsync(workspace, "variables", name);
        if (hits.Count > 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("explorer.deleteVariableUsed", ("name", name), ("count", hits.Count), ("places", Places(workspace, hits))));
            return;
        }

        if (!await Confirm(Prompts.DeleteResource, Loc.Current.Format("explorer.deleteVariableTitle", ("name", name)), Loc.Current.Format("explorer.deleteVariableBody", ("name", name)), "common.delete")) return;
        await EditOrForceAsync(workspace, _ => new ResourceListCommand("variables", ResourceListCommand.Delete, name), name);
    }
}
