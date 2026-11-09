namespace G3MStudio.Tests;

/// <summary>
/// The folder of data files the corpus tests read. The reduced fixtures next to the test binary are the default, so every test runs on every machine;
/// G3MSTUDIO_CORPUS_DIR points the same tests at a folder of real games instead.
/// </summary>
internal static class Corpus
{
    /// <summary>The folder of the reduced fixtures, one folder per game.</summary>
    public static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>Where the screenshot tests write their pictures: G3MSTUDIO_SHOTS, or a temp folder.</summary>
    public static string Shots => Environment.GetEnvironmentVariable("G3MSTUDIO_SHOTS") is { Length: > 0 } set ? set : Path.Combine(Path.GetTempPath(), "g3mstudio-shots");

    /// <summary>A small font under the SIL Open Font License (Fixtures/font/OFL.txt), for the tests that need a font file.</summary>
    public static string Font => Path.Combine(AppContext.BaseDirectory, "Fixtures", "font", "VT323-Regular.ttf");

    /// <summary>The data.win files under the folder: smallest first for real games; the fixtures start with the newest format, which has the most kinds of resource.</summary>
    public static IEnumerable<string> Files(string folder) => Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories)
        .OrderBy(f => f.StartsWith(Fixtures, StringComparison.Ordinal) ? Array.IndexOf(["uty", "deltarune", "pizza", "undertale"], Path.GetFileName(Path.GetDirectoryName(f))) : 0)
        .ThenBy(f => new FileInfo(f).Length);

    public static string Folder
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
            return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? folder : Path.Combine(AppContext.BaseDirectory, "Fixtures");
        }
    }
}

/// <summary>
/// The timing tests always run. G3MSTUDIO_PERF switches them to the full sizes and to the files of G3MSTUDIO_PERF_FILES (semicolon separated);
/// without it they use a twenty-fifth of the size and the corpus fixtures, so a run stays short on a CI machine.
/// </summary>
internal static class Perf
{
    public static bool On => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G3MSTUDIO_PERF"));

    public static int Count(int full) => On ? full : full / 25;

    public static IEnumerable<string> Files =>
        On && Environment.GetEnvironmentVariable("G3MSTUDIO_PERF_FILES") is { Length: > 0 } list
            ? list.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists)
            : Directory.EnumerateFiles(Corpus.Folder, "data.win", SearchOption.AllDirectories);
}
