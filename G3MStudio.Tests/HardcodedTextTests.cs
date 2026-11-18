using System.Text.RegularExpressions;

namespace G3MStudio.Tests;

/// <summary>Texts a person reads come from the language files. Source that assigns a word or sentence to a visible property fails here.</summary>
public sealed partial class HardcodedTextTests
{
    // Content = "Word", Text = "Two words", ToolTip.SetTip(x, "..."), Labeled("..."), and the same properties in axaml.
    [GeneratedRegex("""\b(Content|Text|Header|Title|Watermark|PlaceholderText)\s*=\s*\$?"(?<t>[^"{}]*\p{L}{2,}[^"{}]*)"|ToolTip\.SetTip\([^,]+,\s*\$?"(?<t>[^"{}]*\p{L}{2,}[^"{}]*)"|\b(Content|Text|Header|Title|Watermark|PlaceholderText|ToolTip\.Tip)="(?<t>[^"{}&][^"{}]*\p{L}{2,}[^"{}]*)"|AutomationProperties\.SetName\([^,]+,\s*\$?"(?<t>[^"{}]*\p{L}{2,}[^"{}]*)"|AutomationProperties\.Name="(?<t>[^"{}]*\p{L}{2,}[^"{}]*)"|\bToolTip="(?<t>[^"{}]*\p{L}{2,}[^"{}]*)"|<ToolTip>(?<t>[^<{]*\p{L}{2,}[^<]*)</ToolTip>""")]
    private static partial Regex Visible();

    // Units, a product name and file formats are the same in every language.
    private static readonly string[] Allowed = ["MB", "GB", "G3MStudio"];

    // Escaped symbols (a dot, an ellipsis) and addresses carry no words.
    private static bool Symbol(string text) => text.Contains(@"\u", StringComparison.Ordinal) || text.Contains("&#", StringComparison.Ordinal) || text.StartsWith("http", StringComparison.Ordinal);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("The solution folder was not found.");
    }

    [Fact]
    public void NoVisibleTextIsWrittenInTheSource()
    {
        var folder = Path.Combine(Root(), "G3MStudio");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal) || lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                foreach (Match m in Visible().Matches(lines[i]))
                    if (!Allowed.Contains(m.Groups["t"].Value) && !Symbol(m.Groups["t"].Value)) found.Add($"{Path.GetRelativePath(folder, file)}:{i + 1}: {m.Value}");
            }
        }

        Assert.True(found.Count == 0, "visible text outside the language files:\n" + string.Join('\n', found));
    }
}
