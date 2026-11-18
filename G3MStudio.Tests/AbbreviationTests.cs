using System.Text.Json;
using System.Text.RegularExpressions;
using G3MStudio.Core.Localization;

namespace G3MStudio.Tests;

/// <summary>An abbreviation of several words is upper case in every text a person reads. Only the literal file ending is lower case, with its dot (".dll").</summary>
public sealed partial class AbbreviationTests
{
    private const string Words = "API|ASCII|AVI|BGRA|BMP|BZ2|CJK|CLI|CPU|CRC32|CSS|CSV|D3D|DDS|DLL|DPI|EXE|FFT|FPS|GIF|GLSL|GML|GPU|GUI|GUID|HLSL|HSV|HTML|IAP|ID|IDE|INI|IP|JPEG|JPG|JS|JSON|KB|MB|MDI|MP3|MP4|OGG|OS|PC|PCM|PDF|PNG|PSSL|QOI|RGB|RGBA|RLE|RMS|SDF|SDK|SQL|SVG|SWF|TGA|TIFF|TPAG|TTF|TXT|UI|UID|URI|URL|UTF|UTMT|VM|WAV|WEBP|XML|XOR|YYC|YYP|YYZ|ZIP";

    // A letter, digit, path or placeholder character next to the word means it is part of a longer word, a name or a path. A dot before it is a literal file ending.
    [GeneratedRegex(@"(?<![A-Za-z0-9\u00C0-\u024F\u0400-\u04FF_\-/\\%{}$#.@])(" + Words + @")(?![A-Za-z0-9\u00C0-\u024F\u0400-\u04FF_\-/\\%{}(])", RegexOptions.IgnoreCase)]
    private static partial Regex Whole();

    // "language word" lines for a word that is not the abbreviation in the language that uses it. Empty while nothing needs it.
    private static readonly HashSet<string> Allowed = [];

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "G3MStudio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("G3MStudio.slnx not found above the test output folder.");
    }

    /// <summary>The abbreviations of the text that are not written in capitals.</summary>
    private static IEnumerable<string> Misspelled(string text, string language) =>
        Whole().Matches(text).Select(m => m.Value).Where(w => !string.Equals(w, w.ToUpperInvariant(), StringComparison.Ordinal) && !Allowed.Contains(language + " " + w));

    [Fact]
    public void EveryLanguageFileWritesAbbreviationsInCapitals()
    {
        var found = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "G3MStudio.Core", "Languages"), "lang_*.json"))
        {
            var language = Path.GetFileNameWithoutExtension(file)["lang_".Length..];
            using var stream = File.OpenRead(file);
            foreach (var (key, text) in Localizer.Flatten(stream))
                found.AddRange(Misspelled(text, language).Select(w => $"{language} {key}: {w} in \"{text}\""));
        }

        Assert.True(found.Count == 0, string.Join('\n', found));
    }

    [Fact]
    public void TheFunctionReferenceWritesAbbreviationsInCapitals()
    {
        var found = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "G3MStudio.Core", "Resources"), "gml-reference*.json"))
        {
            var fileName = Path.GetFileName(file);
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var entries = doc.RootElement.TryGetProperty("entries", out var list) ? list.EnumerateArray().Select(e => (e.GetProperty("n").GetString()!, e))
                : doc.RootElement.EnumerateObject().Select(p => (p.Name, p.Value));
            foreach (var (name, entry) in entries)
            {
                var texts = new List<string>();
                if (entry.TryGetProperty("s", out var summary)) texts.Add(summary.GetString()!);
                if (entry.TryGetProperty("p", out var parameters))
                    texts.AddRange(parameters.ValueKind == JsonValueKind.Object ? parameters.EnumerateObject().Select(p => p.Value.GetString()!) : parameters.EnumerateArray().Select(p => p[2].GetString()!));
                found.AddRange(texts.SelectMany(t => Misspelled(t, fileName).Select(w => $"{fileName} {name}: {w} in \"{t}\"")));
            }
        }

        Assert.True(found.Count == 0, string.Join('\n', found));
    }

    [Theory]
    [InlineData("Open a png or Json file.", 2)]
    [InlineData("Use the dll file.", 1)]
    [InlineData("The id of the room.", 1)]
    [InlineData("Open a PNG, JSON or DLL file.", 0)]
    [InlineData("Files like .dll, .png and settings.json keep their ending.", 0)]
    [InlineData("The {id} of mod_config and the path C:/id/ok.", 0)]
    [InlineData("Diseños guardados", 0)]
    public void TheCheckSeesLowerAndMixedCaseWholeWordsOnly(string text, int count) => Assert.Equal(count, Misspelled(text, "en").Count());
}
