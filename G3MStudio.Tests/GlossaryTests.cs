using System.Text.Json;
using System.Text.RegularExpressions;
using G3MStudio.Core.Localization;

namespace G3MStudio.Tests;

/// <summary>ADR-0006: a UI string that uses a glossary concept must use the glossary term of its language.</summary>
public sealed partial class GlossaryTests
{
    private static readonly string[] Targets = ["ru", "es", "ja", "ko", "zh_cn", "zh_tw"];
    private static readonly string[] Cjk = ["ja", "ko", "zh_cn", "zh_tw"];

    private sealed record Term(string En, Dictionary<string, string> Forms, Dictionary<string, string[]> Avoid);

    private static List<Term> LoadGlossary()
    {
        var path = Path.Combine(RepoRoot(), "G3MStudio.Core", "Glossary", "glossary.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return [.. doc.RootElement.GetProperty("terms").EnumerateArray().Select(t => new Term(
            t.GetProperty("en").GetString()!,
            Targets.ToDictionary(l => l, l => t.GetProperty(l).GetString()!),
            t.TryGetProperty("avoid", out var avoid) ? avoid.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray()) : []))];
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("G3MStudio.slnx not found above the test output folder.");
    }

    private static Dictionary<string, Dictionary<string, string>> LoadLanguages() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "G3MStudio.Core", "Languages"), "lang_*.json").ToDictionary(
            f => Path.GetFileNameWithoutExtension(f)["lang_".Length..],
            f => { using var s = File.OpenRead(f); return Localizer.Flatten(s); });

    // shortcut: a word counts when its stem (the word minus up to 3 ending letters; CJK whole) is in the text, so ru and es inflection passes; upgrade when real morphology matters.
    private static bool Contains(string text, string term, string language)
    {
        foreach (var word in term.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var cut = StemCut(word, language);
            if (!text.Contains(word[..^cut], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    // Japanese verbs end in く and Korean verb nouns in 기: the stem (開, 열) is what every form of the verb shares. Spanish infinitives lose ar, er or ir.
    private static int StemCut(string word, string language)
    {
        if (language == "ja") return word.EndsWith('く') ? 1 : 0;
        if (language == "ko") return word.EndsWith('기') ? 1 : 0;
        if (Cjk.Contains(language)) return 0;
        if (language == "es" && word.Length >= 5 && (word.EndsWith("ar", StringComparison.Ordinal) || word.EndsWith("er", StringComparison.Ordinal) || word.EndsWith("ir", StringComparison.Ordinal))) return 2;
        return Math.Clamp(word.Length - 4, 0, 3);
    }

    // Irregular forms the stem rule cannot reach: Spanish abierto (the participle of abrir).
    private static readonly HashSet<string> Allowed = ["es:cmd.tabs.keepOpen", "es:editor.empty", "es:settings.projectsNote", "es:transfer.confirmImport", "es:transfer.cancelledImport"];

    [GeneratedRegex(@"\{\w+\}")]
    private static partial Regex Placeholder();

    private static List<string> Violations(string language, Dictionary<string, string> english, Dictionary<string, string> strings, List<Term> glossary)
    {
        var problems = new List<string>();
        foreach (var (key, text) in strings.Where(p => !p.Key.StartsWith("metadata.", StringComparison.Ordinal)))
        {
            // Placeholders like {file} are not words, and a shorter term inside a longer one that matched (save in save as) is left to the longer term.
            var source = Placeholder().Replace(english[key], "");
            var matched = glossary.Where(t => Regex.IsMatch(source, $@"\b{Regex.Escape(t.En)}\b", RegexOptions.IgnoreCase)).ToList();
            foreach (var term in glossary)
            {
                var covered = matched.Any(o => o.En.Length > term.En.Length && o.En.Contains(term.En, StringComparison.OrdinalIgnoreCase));
                if (matched.Contains(term) && !covered && !Allowed.Contains($"{language}:{key}") && !Contains(text, term.Forms[language], language))
                    problems.Add($"{language}:{key} uses '{term.En}' but not '{term.Forms[language]}'");
                foreach (var bad in term.Avoid.GetValueOrDefault(language, []))
                    if (text.Contains(bad, StringComparison.OrdinalIgnoreCase)) problems.Add($"{language}:{key} uses rejected '{bad}' for '{term.En}'");
            }
        }
        return problems;
    }

    [Fact]
    public void GlossaryHasEveryTermInTheSixLanguages()
    {
        var glossary = LoadGlossary();
        Assert.NotEmpty(glossary);
        Assert.Equal(glossary.Count, glossary.Select(t => t.En).Distinct().Count());
        Assert.All(glossary, t => Assert.All(t.Forms.Values, f => Assert.False(string.IsNullOrWhiteSpace(f))));
    }

    [Fact]
    public void UiStringsUseTheGlossaryTerms()
    {
        var all = LoadLanguages();
        var glossary = LoadGlossary();
        var problems = all.Where(p => p.Key != "en").SelectMany(p => Violations(p.Key, all["en"], p.Value, glossary)).ToList();
        Assert.Empty(problems);
    }

    [Fact]
    public void ACheckerThatFindsNothingWouldBeCaught()
    {
        var glossary = LoadGlossary();
        var english = new Dictionary<string, string> { ["a"] = "Open settings", ["b"] = "Redo" };
        Assert.Empty(Violations("ru", english, new() { ["a"] = "Откройте настройки", ["b"] = "Повторить" }, glossary));
        var bad = Violations("ru", english, new() { ["a"] = "Откройте параметры", ["b"] = "Вернуть" }, glossary);
        Assert.Equal(4, bad.Count);
    }
}
