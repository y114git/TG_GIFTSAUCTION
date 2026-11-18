using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>Where one entry changes in a rename across all code.</summary>
public sealed record EntryChange(string Entry, string NewText, int Uses, int Strings);

/// <summary>A rename of a name in every code entry. Warnings are shown first, and the rename can still go on ("do it anyway").</summary>
/// <param name="Changes">The entries that change.</param>
/// <param name="Warnings">Language keys of the problems found.</param>
/// <param name="Review">Uses after a dot (other.name, struct keys, enum members) that are not renamed because they cannot be resolved safely; the user checks them by hand.</param>
public sealed record CodeRenamePlan(IReadOnlyList<EntryChange> Changes, IReadOnlyList<string> Warnings, IReadOnlyList<NameHit> Review)
{
    public int Uses => Changes.Sum(c => c.Uses);

    public int Strings => Changes.Sum(c => c.Strings);
}

public static class CodeRename
{
    /// <summary>Plans the rename: decompiles every entry that mentions the name and rewrites its tokens (and, when asked, string literals that equal it). Nothing is changed in the file.</summary>
    public static Task<CodeRenamePlan> PlanAsync(Workspace workspace, string oldName, string newName, bool includeStrings, CancellationToken cancellationToken = default) =>
        Task.Run(() => Plan(workspace, oldName, newName, includeStrings, cancellationToken), cancellationToken);

    /// <summary>The same on the calling thread. Never call it on the UI thread: it decompiles every entry.</summary>
    public static CodeRenamePlan Plan(Workspace workspace, string oldName, string newName, bool includeStrings, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        if (!GmlSymbols.IsValidName(newName)) warnings.Add("code.rename.invalid");
        if (newName == oldName) warnings.Add("code.rename.same");
        if (newName != oldName && (GmlNames.For(workspace)(newName) is not null || CodeScan.FindName(workspace, newName, cancellationToken).Count > 0)) warnings.Add("code.rename.collision");
        G3MLib.DataFile.Decompiler.GlobalDecompileContext context;
        using (workspace.Lock.Read()) context = workspace.DecompileContext();
        var changes = new List<EntryChange>();
        var review = new List<NameHit>();
        foreach (var (entry, text) in CodeScan.TextsContaining(workspace, oldName, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = GmlDoc.Parse(text);
            var (rename, unsure) = GmlSymbols.AcrossReferences(doc, oldName);
            var lines = text.Split('\n');
            foreach (var (start, _) in unsure)
            {
                var line = text.AsSpan(0, start).Count('\n');
                review.Add(new NameHit(entry, line + 1, lines[line].Trim(), false));
            }

            var edits = rename.Select(r => new TextEdit(r.Start, r.End - r.Start, newName)).ToList();
            var strings = includeStrings ? GmlSymbols.StringReferences(doc, oldName).Select(r => new TextEdit(r.Start + 1, oldName.Length, newName)).ToList() : [];
            if (edits.Count + strings.Count > 0) changes.Add(new EntryChange(entry, GmlSymbols.Apply(text, [.. edits, .. strings]), edits.Count, strings.Count));
        }

        return new CodeRenamePlan(changes, warnings, review);
    }

    /// <summary>The edit that applies the plan: one undo step for every entry. A compile error in one entry reverts the others and throws.</summary>
    public static EditCommand ToCommand(CodeRenamePlan plan) => new GroupCommand([.. plan.Changes.Select(c => new EditCodeCommand(c.Entry, c.NewText))]);
}
