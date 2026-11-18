using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The code check over every code entry of the corpus games: code the game shipped with compiles and runs, so a warning on it is a false alarm.</summary>
public sealed class GmlLintCorpusTests(ITestOutputHelper output)
{
    // Hints that real code may well deserve (an unused local, unreachable code after the decompiler's own exit) are counted, not failed.
    private static readonly HashSet<string> Counted = [GmlLint.UnusedLocal, GmlLint.Unreachable, GmlLint.Deprecated, GmlLint.AssignInCondition, GmlLint.UnusedParameter, GmlLint.Shadowing, GmlLint.MissingReturn];

    [Fact]
    public async Task DecompiledCodeOfTheCorpusGamesGetsNoWarnings()
    {
        var bad = new List<string>();
        foreach (var file in Corpus.Files(Corpus.Folder))
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var context = GmlLintSource.For(workspace);
            var game = Path.GetFileName(Path.GetDirectoryName(file));
            var codes = new Dictionary<string, int>(StringComparer.Ordinal);
            int entries = 0, failed = 0, unparsed = 0;
            long chars = 0;
            var names = workspace.Data.Code.Where(c => c?.Name?.Content is { Length: > 0 } && c.ParentEntry is null).Select(c => c.Name.Content!).ToList();
            foreach (var entry in names)
            {
                string text;
                try { text = CodeText.Decompile(workspace.Data, entry, workspace.DecompileContext()); }
                catch (Exception) { failed++; continue; }
                entries++;
                chars += text.Length;
                // Only text the parser accepts is checked, as in the editor.
                if (LiveCheck.Parse(text).Any(d => d.Severity == DiagnosticSeverity.Error)) { unparsed++; continue; }
                foreach (var issue in GmlLint.Analyze(GmlDoc.Parse(text, workspace.Data.IsGameMaker2()), context, TestContext.Current.CancellationToken))
                {
                    var code = issue.Diagnostic.Code;
                    codes[code] = codes.GetValueOrDefault(code) + 1;
                    if (!Counted.Contains(code) && bad.Count < 40)
                        bad.Add($"{game} {entry} {code} line {issue.Diagnostic.Span.StartLine}: {issue.Diagnostic.Message} '{text[issue.Diagnostic.Span.StartOffset..issue.Diagnostic.Span.EndOffset]}'");
                }
            }

            output.WriteLine($"{game}: {entries} entries ({failed} not decompiled, {unparsed} not parsed, {chars:N0} characters): " + string.Join(", ", codes.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}")));
        }

        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }
}
