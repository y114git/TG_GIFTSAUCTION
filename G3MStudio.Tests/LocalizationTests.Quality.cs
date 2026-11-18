using System.Text;
using System.Text.RegularExpressions;
using G3MStudio.Core.Localization;

namespace G3MStudio.Tests;

/// <summary>Rules that keep the seven languages complete and the code free of hard-coded UI text.</summary>
public sealed partial class LocalizationTests
{
    [GeneratedRegex(@"\{(\w+):([^{}]*)\}")]
    private static partial Regex PluralToken();

    [GeneratedRegex(@"\{\w+(:[^{}]*)?\}")]
    private static partial Regex AnyBrace();

    private static Localizer Build(params (string Code, string Json)[] languages) =>
        new(languages.Select(l => (l.Code, (Stream)new MemoryStream(Encoding.UTF8.GetBytes(l.Json)))));

    [Theory]
    [InlineData("en", 1, "1 file")]
    [InlineData("en", 0, "0 files")]
    [InlineData("en", 2, "2 files")]
    [InlineData("ru", 1, "1 файл")]
    [InlineData("ru", 2, "2 файла")]
    [InlineData("ru", 5, "5 файлов")]
    [InlineData("ru", 11, "11 файлов")]
    [InlineData("ru", 21, "21 файл")]
    [InlineData("ru", 22, "22 файла")]
    [InlineData("ru", 112, "112 файлов")]
    [InlineData("ja", 5, "5 一つ")]
    public void PluralTokenPicksTheFormOfTheLanguage(string language, int count, string expected)
    {
        var loc = Build(("en", """{"metadata":{"language_name":"English"},"k":"{n} {n:file|files}"}"""), ("ru", """{"metadata":{"language_name":"ru"},"k":"{n} {n:файл|файла|файлов}"}"""), ("ja", """{"metadata":{"language_name":"ja"},"k":"{n} {n:一つ|複数}"}"""));
        loc.SetLanguage(language);
        Assert.Equal(expected, loc.Format("k", ("n", count)));
    }

    [Fact]
    public void PluralTokenWithoutAWholeNumberTakesTheLastForm()
    {
        var loc = Build(("en", """{"metadata":{"language_name":"English"},"k":"{n:one|other}"}"""));
        Assert.Equal("other", loc.Format("k", ("n", "1")));
        Assert.Equal("other", loc.Format("k"));
    }

    // A token names a number given to Format, so it needs a plain {name} placeholder of the same name in English (or n, a count passed next to a formatted one), and no brace may survive Format.
    [Fact]
    public void PluralTokensAreWellFormedInEveryLanguage()
    {
        var all = LoadAll();
        var loc = Localizer.LoadEmbedded();
        foreach (var (code, strings) in all)
        {
            loc.SetLanguage(code);
            foreach (var (key, text) in strings.Where(p => IsString(p.Key)))
            {
                var allowed = Placeholder().Matches(all["en"][key]).Select(m => m.Groups[1].Value).Append("n").ToHashSet();
                foreach (Match token in PluralToken().Matches(text))
                {
                    Assert.True(allowed.Contains(token.Groups[1].Value), $"{code}:{key} plural token {token.Value} names no number of the English text");
                    var forms = token.Groups[2].Value.Split('|').Length;
                    Assert.True(code switch { "ru" => forms is 2 or 3, "ja" or "ko" or "zh_cn" or "zh_tw" => forms >= 1, _ => forms == 2 }, $"{code}:{key} plural token {token.Value} has {forms} forms");
                }

                var args = allowed.Select(n => (n, (object)2)).ToArray();
                Assert.False(AnyBrace().IsMatch(loc.Format(key, args)), $"{code}:{key} keeps a brace after Format");
            }
        }
    }

    // Values that stay the same as English on purpose: product and technical names, key names, Spanish words that are spelled the same.
    private static readonly string[] SameAsEnglishPrefixes = ["metadata.", "events.sub.vk_", "properties.label.", "properties.choice."];

    private static readonly HashSet<string> SameAsEnglishKeys = ["app.title", "settings.shortcutNum", "git.failed", "strings.regex", "panel.git", "dock.DocumentTabStripItemLayoutModeMdiString", "explorer.nullEntry"];

    private static readonly HashSet<string> SameAsEnglishSpanish =
    [
        "menu.mods", "menu.sprite", "explorer.zoomLabel", "explorer.typeOne.scripts", "explorer.typeOne.sprites", "explorer.typeOne.shaders", "explorer.typeOne.variables",
        "explorer.type.sprites", "explorer.type.scripts", "explorer.type.shaders", "explorer.type.variables", "code.hover.variable", "code.outlineKind.enum", "code.outlineKind.macro",
        "notify.error", "editor.typingInsert", "quick.kind.variable", "settings.scaleAuto", "git.commit", "git.fetch", "git.pull", "git.push", "uiLayer.prop.Visible", "transfer.groupMods",
        "strings.kindVariable", "fonts.presetNormal", "fonts.previewZoom", "fonts.presetHiragana", "fonts.presetKatakana", "plugins.error", "rooms.layerKind.addTiles", "panel.inspector",
        "image.blend.Normal", "pages.optPageAuto", "pages.optMaxAuto", "colors.Bg.Surface", "colors.Danger", "colors.Syntax.Variable", "included.kind.audio", "editors.mask.auto",
        "editors.mask.manual", "editors.object.sprite", "editors.particles.solo",
    ];

    [Fact]
    public void NoTranslationIsLeftInEnglish()
    {
        var all = LoadAll();
        foreach (var (code, strings) in all.Where(p => p.Key != "en"))
            foreach (var (key, text) in strings.Where(p => IsString(p.Key)))
            {
                var words = Placeholder().Replace(text, "");
                if (text != all["en"][key] || !LetterRun().IsMatch(words)) continue;
                var ok = SameAsEnglishPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)) || SameAsEnglishKeys.Contains(key) || (code == "es" && SameAsEnglishSpanish.Contains(key));
                Assert.True(ok, $"{code}:{key} is still the English text: {text}");
            }
    }

    [GeneratedRegex(@"[A-Za-z]{3,}")]
    private static partial Regex LetterRun();

    // Text, Content, Header, Title and placeholder assignments, file type names, tooltips and automation names must come from Loc.Current. Short symbols and units are allowed.
    [GeneratedRegex(@"(?:\b(?:Text|Content|Header|Title|Watermark|PlaceholderText)\s*=\s*|FilePickerFileType\(|ToolTip\.SetTip\(\s*\w+\s*,\s*|AutomationProperties\.SetName\(\s*\w+\s*,\s*)""([^""\r\n]*)""")]
    private static partial Regex LiteralUiText();

    // CSV is a file format name, the same in every language.
    private static readonly HashSet<string> LiteralAllowed = ["MB", "KB", "GB", "CSV"];

    [Fact]
    public void NoUserVisibleTextIsHardCodedInTheApp()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "G3MStudio"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)).Where(p => !p.l.TrimStart().StartsWith("//", StringComparison.Ordinal)))
                foreach (Match m in LiteralUiText().Matches(line))
                {
                    var text = Placeholder().Replace(m.Groups[1].Value, "");
                    if (LetterRun().IsMatch(text) && !LiteralAllowed.Contains(text) && !text.StartsWith("http", StringComparison.Ordinal))
                        offenders.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{number} {m.Groups[1].Value}");
                }

        Assert.True(offenders.Count == 0, "hard-coded UI text, move it to the language files:\n" + string.Join("\n", offenders));
    }
}
