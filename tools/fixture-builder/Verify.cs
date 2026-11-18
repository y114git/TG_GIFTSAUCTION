using System.Security.Cryptography;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;

/// <summary>Checks that nothing of the original game is left in a fixture: strings, picture rectangles and sounds are compared by content.</summary>
static class Verify
{
    public static async Task<int> Run(string originalFile, string fixtureFile)
    {
        using var original = await GameMakerIO.ReadAsync(originalFile);
        using var fixture = await GameMakerIO.ReadAsync(fixtureFile);
        var builtIn = fixture.Variables.Select(v => v.Name.Content).Concat(fixture.Functions.Select(f => f.Name.Content)).ToHashSet();

        // Text: longer than three characters, and not a name the language itself uses (those are in the variable and function lists of the fixture and of the original).
        var originalText = original.Strings.Select(s => s.Content).Where(s => s is { Length: > 3 }).ToHashSet();
        var sharedText = fixture.Strings.Select(s => s.Content).Where(s => s is { Length: > 3 } && originalText.Contains(s) && !builtIn.Contains(s) && !s.StartsWith('.') && !s.StartsWith("_filter_", StringComparison.Ordinal) && !s.Equals("default", StringComparison.OrdinalIgnoreCase) && s != "DynTex" && !System.Text.RegularExpressions.Regex.IsMatch(s, "^GM[A-Za-z]+$")).Distinct().ToList();
        Console.WriteLine($"shared text: {sharedText.Count}");
        foreach (var s in sharedText.Take(40)) Console.WriteLine("  " + s);

        var shared = Hashes(original).Intersect(Hashes(fixture)).Count();
        Console.WriteLine($"shared picture rectangles: {shared}");

        var originalAudio = original.EmbeddedAudio.Select(a => Convert.ToHexString(SHA1.HashData(a.Data))).ToHashSet();
        var sharedAudio = fixture.EmbeddedAudio.Count(a => originalAudio.Contains(Convert.ToHexString(SHA1.HashData(a.Data))));
        Console.WriteLine($"shared sounds: {sharedAudio}");
        return sharedText.Count + shared + sharedAudio == 0 ? 0 : 1;
    }

    private static HashSet<string> Hashes(GameMakerData data)
    {
        var found = new HashSet<string>();
        using var worker = new TextureWorker();
        foreach (var item in data.TexturePageItems.Where(i => i.SourceWidth >= 8 && i.SourceHeight >= 8))
        {
            var pixels = worker.CutBgra(item, false, out _, out _).ToArray();
            if (pixels.Any(b => b != 0)) found.Add(Convert.ToHexString(SHA1.HashData(pixels)));
        }

        return found;
    }
}
