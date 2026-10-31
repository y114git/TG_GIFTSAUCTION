using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;

namespace G3MStudio.Tests;

/// <summary>A temp folder that is removed with the test.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "g3mstudio-tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // Git writes its objects read-only, which blocks the delete on Windows.
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                if (new FileInfo(file).IsReadOnly) System.IO.File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* a handle still open in a background export must not fail the test */ }
    }
}

/// <summary>Reports on the calling thread, so a test can cancel from inside a report.</summary>
public sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

public static class TestData
{
    /// <summary>A generated data file model: <paramref name="objects"/> objects and as many scripts, no game files needed.</summary>
    public static GameMakerData Create(int objects = 3, bool withCode = false)
    {
        var data = GameMakerData.CreateNew2026LTS();
        if (withCode)
        {
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_probe", "return 1;");
            group.Import();
        }

#pragma warning disable CA2000 // the data model owns what is added to it
        for (var i = 0; i < objects; i++)
        {
            data.GameObjects.Add(new GameMakerGameObject { Name = data.Strings.MakeString($"obj_{i}") });
            data.Scripts.Add(new GameMakerScript { Name = data.Strings.MakeString($"scr_{i}") });
        }
#pragma warning restore CA2000

        return data;
    }

    public static string Write(string path, GameMakerData data)
    {
        using var stream = File.Create(path);
        GameMakerIO.Write(stream, data);
        return path;
    }

    public static byte[] Bytes(GameMakerData data)
    {
        using var stream = new MemoryStream();
        GameMakerIO.Write(stream, data);
        return stream.ToArray();
    }

    public static string WriteSample(TempDir dir, string name = "data.win", int objects = 3, bool withCode = false)
    {
        using var data = Create(objects, withCode);
        return Write(dir.File(name), data);
    }
}
