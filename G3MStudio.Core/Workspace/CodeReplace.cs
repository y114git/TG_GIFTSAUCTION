using G3MLib.DataFile.Decompiler;

namespace G3MStudio.Core.Workspace;

/// <param name="Lines">Lines that were changed.</param>
/// <param name="Entries">Code entries that were changed.</param>
/// <param name="Failed">Entries that were left as they were: they did not decompile, or the new text did not compile.</param>
public sealed record CodeReplaceResult(int Lines, int Entries, IReadOnlyList<string> Failed);

/// <summary>Find and replace across all code (SF-03): writes the checked lines of a preview as one undoable edit.</summary>
public static class CodeReplace
{
    /// <summary>
    /// Replaces the matches on the chosen lines of the current decompiled text. An entry that fails to decompile or compile is left unchanged and listed;
    /// the others are written together, so one Undo takes the whole replacement back.
    /// </summary>
    public static async Task<CodeReplaceResult> ApplyAsync(Workspace workspace, CodeSearchOptions options, IReadOnlyCollection<CodeHit> chosen, CancellationToken cancellationToken = default)
    {
        if (options.Replacement is null) throw new ArgumentException("There is no replacement text.", nameof(options));
        var regex = CodeSearch.CreateRegex(options);
        var failed = new List<string>();
        var edits = new List<(EditCodeCommand Command, string Entry, int Lines)>();
        await Task.Run(() =>
        {
            GlobalDecompileContext context;
            using (workspace.Lock.Read()) context = workspace.DecompileContext();
            foreach (var group in chosen.GroupBy(h => h.Entry, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string text;
                try
                {
                    using (workspace.Lock.Read()) text = CodeText.Decompile(workspace.Data, group.Key, context);
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
                {
                    failed.Add(group.Key);
                    continue;
                }

                var lines = text.Split('\n');
                var changed = 0;
                foreach (var line in group.Select(h => h.Line).Distinct())
                {
                    if (line < 1 || line > lines.Length) continue;
                    var replaced = CodeSearch.ReplaceLine(regex, options, lines[line - 1]);
                    if (replaced is null || replaced == lines[line - 1]) continue;
                    lines[line - 1] = replaced;
                    changed++;
                }

                if (changed > 0) edits.Add((new EditCodeCommand(group.Key, string.Join('\n', lines)), group.Key, changed));
            }
        }, cancellationToken);

        // The group undoes itself when one entry does not compile; that entry is then left out and the rest tried again.
        while (edits.Count > 0)
        {
            try
            {
                await workspace.EditInBackground(new GroupCommand([.. edits.Select(e => e.Command)]));
                break;
            }
            catch (CodeCompileException e)
            {
                failed.Add(e.Entry);
                if (edits.RemoveAll(x => x.Entry == e.Entry) == 0) throw;
            }
        }

        return new CodeReplaceResult(edits.Sum(e => e.Lines), edits.Count, failed);
    }
}
