using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using Xunit;

namespace G3MLib.Tests;

/// <summary>
/// Reads each data file of the local game corpus, writes it back and compares the bytes. Skipped when no corpus folder exists.
/// The folder is G3MLIB_CORPUS, or Downloads/DATAS plus the Steam installs of Undertale and Deltarune.
/// </summary>
public sealed class CorpusRoundTripTests
{
    public static IEnumerable<string> CorpusFiles()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common");
        string? configured = Environment.GetEnvironmentVariable("G3MLIB_CORPUS");
        string[] roots = string.IsNullOrWhiteSpace(configured)
            ? [Path.Combine(home, "Downloads", "DATAS"), Path.Combine(steam, "Undertale"), Path.Combine(steam, "DELTARUNE")]
            : configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roots.Where(Directory.Exists).SelectMany(root => Directory.GetFiles(root, "data.win", SearchOption.AllDirectories)).Order(StringComparer.Ordinal);
    }

    [Fact]
    public void WrittenCorpusFilesEqualTheOriginals()
    {
        List<string> files = [.. CorpusFiles()];
        Assert.SkipWhen(files.Count == 0, "No corpus folder found");

        List<string> differences = [];
        foreach (string path in files)
        {
            byte[] original = File.ReadAllBytes(path);
            using GameMakerData data = GameMakerIO.Read(new MemoryStream(original));
            using MemoryStream output = new(original.Length);
            GameMakerIO.Write(output, data);
            ReadOnlySpan<byte> written = output.GetBuffer().AsSpan(0, (int)output.Length);
            if (!written.SequenceEqual(original))
                differences.Add($"{path}: {original.Length} bytes read, {written.Length} written, first difference at {written.CommonPrefixLength(original)}");
        }
        Assert.Empty(differences);
    }
}
