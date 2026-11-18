using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Rename a name in all code (and the strings that equal it) with a preview. Warnings are shown and the rename can still go on.</summary>
public static class CodeRenameDialog
{
    public static Task AskAsync(Avalonia.Controls.Window owner, Workspace workspace, string name)
    {
        string? planned = null;
        CodeRenamePlan? plan = null;
        CodeRenamePlan Plan(string newName)
        {
            if (planned != newName || plan is null)
            {
                plan = CodeRename.Plan(workspace, name, newName, includeStrings: true);
                planned = newName;
            }

            return plan;
        }

        // The preview runs off the UI thread; Apply uses the plan the preview made, or makes one.
        string Preview(string newName) => Describe(Plan(newName), workspace.Nicknames.CodeLabel);
        string? Apply(string newName)
        {
            try
            {
                workspace.Edit(CodeRename.ToCommand(Plan(newName)));
                return null;
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
        }

        return new RenameWindow(name, Apply, Loc.Current["code.rename.title"], options: new RenameOptions(Preview)).AskAsync(owner);
    }

    /// <summary>The preview text: each warning, then how many uses, strings and code entries change.</summary>
    public static string Describe(CodeRenamePlan plan, Func<string, string>? entryLabel = null)
    {
        var lines = plan.Warnings.Select(w => Loc.Current[w]).ToList();
        lines.Add(plan.Changes.Count == 0 ? Loc.Current["code.rename.none"] : Loc.Current.Format("code.rename.across", ("uses", plan.Uses), ("strings", plan.Strings), ("entries", plan.Changes.Count)));
        lines.AddRange(plan.Changes.Take(6).Select(c => $"{entryLabel?.Invoke(c.Entry) ?? c.Entry}  {c.Uses}"));
        if (plan.Review.Count > 0)
        {
            lines.Add(Loc.Current.Format("code.rename.review", ("count", plan.Review.Count)));
            lines.AddRange(plan.Review.Take(6).Select(h => $"{entryLabel?.Invoke(h.Entry) ?? h.Entry}:{h.Line}  {h.Text}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
