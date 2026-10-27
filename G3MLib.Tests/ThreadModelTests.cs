using G3MLib.Analyzer.Decompiler;
using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;

namespace G3MLib.Tests;

public class ThreadModelTests : IDisposable
{
    private static readonly byte[] FixtureBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));

    private readonly string _root = Path.Combine(Path.GetTempPath(), "g3mlib-threads-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    public static IEnumerable<object[]> Runs => Enumerable.Range(1, 20).Select(i => new object[] { i });

    private static GameMakerData Load()
    {
        using MemoryStream input = new(FixtureBytes);
        return GameMakerIO.Read(input);
    }

    private static string Decompile(GlobalDecompileContext context, GameMakerCode code)
    {
        try
        {
            return new DecompileContext(context, code, context.Data.ToolInfo.DecompilerSettings).DecompileToString();
        }
        catch (Exception ex)
        {
            return "error: " + ex.GetType().Name;
        }
    }

    private string Export(GameMakerData data, string name)
    {
        string directory = Path.Combine(_root, name);
        HashSet<string> codeNames = data.Code.Select(c => c.Name.Content).ToHashSet();
        G3MProjectService.Export(data, directory, null, new G3MProjectOptions
        {
            Name = "P",
            Mode = ProjectExportMode.Delta,
            Only = new Dictionary<string, IReadOnlySet<string>>
            {
                ["CodeEntries"] = codeNames,
                ["Scripts"] = data.Scripts.Select(s => s.Name.Content).ToHashSet(),
                ["GameObjects"] = data.GameObjects.Select(o => o.Name.Content).ToHashSet()
            }
        });
        return directory;
    }

    private static Dictionary<string, string> Snapshot(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".g3mproj", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(directory, f), f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
    }

    private static void Edit(GameMakerData data, int step)
    {
        data.Sprites[0].OriginX = step % 50;
        data.Sprites[0].OriginY = step % 70;
        data.Sounds[0].Volume = (step % 10) / 10f;
        data.Fonts[0].EmSize = 8 + step % 8;
        data.Fonts[0].Bold = step % 2 == 0;
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void DecompileAndExportInBackgroundWhileUnrelatedResourcesAreEdited(int run)
    {
        using GameMakerData data = Load();
        GlobalDecompileContext context = new(data);
        List<GameMakerCode> codes = [.. data.Code];
        Dictionary<GameMakerCode, string> expected = codes.ToDictionary(c => c, c => Decompile(context, c));
        Dictionary<string, string> expectedFiles = Snapshot(Export(data, "expected"));
        Assert.NotEmpty(expectedFiles);

        using CancellationTokenSource stop = new();
        int step = 0;
        Task editor = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                Edit(data, step++);
            }
        });
        List<Task> jobs = [];
        for (int worker = 0; worker < 3; worker++)
        {
            jobs.Add(Task.Run(() =>
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (GameMakerCode code in codes)
                    {
                        Assert.Equal(expected[code], Decompile(context, code));
                    }
                }
            }));
        }
        jobs.Add(Task.Run(() =>
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Assert.Equal(expectedFiles, Snapshot(Export(data, $"job{run}-{pass}")));
            }
        }));

        try
        {
            Task.WaitAll([.. jobs]);
        }
        finally
        {
            stop.Cancel();
            editor.Wait();
        }
        Assert.True(step > 0);
    }

    [Fact]
    public void LockedStructuralEditsNeverReachRunningJobs()
    {
        using GameMakerData data = Load();
        using ModelLock modelLock = new();
        GlobalDecompileContext context = new(data);
        List<GameMakerCode> codes = [.. data.Code];
        Dictionary<GameMakerCode, string> expected = codes.ToDictionary(c => c, c => Decompile(context, c));

        using CancellationTokenSource stop = new();
        int edits = 0;
        Task editor = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                using (modelLock.Write())
                {
                    GameMakerSprite sprite = new() { Name = data.Strings.MakeString("tmp_sprite_" + edits) };
                    data.Sprites.Add(sprite);
                    data.Sprites.Remove(sprite);
                    edits++;
                }
            }
        });
        // The jobs may otherwise finish before the editor takes the write lock once.
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref edits) > 0, TimeSpan.FromSeconds(10)));
        Task[] jobs = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (GameMakerCode code in codes)
                {
                    using (modelLock.Read())
                    {
                        Assert.Equal(expected[code], Decompile(context, code));
                    }
                }
            }
        })).ToArray();

        try
        {
            Task.WaitAll(jobs);
        }
        finally
        {
            stop.Cancel();
            editor.Wait();
        }
        Assert.True(edits > 0);
    }

    [Fact]
    public void WriteLockExcludesReadersAndReadersShare()
    {
        using ModelLock modelLock = new();
        using (modelLock.Read())
        {
            Task<bool> second = Task.Run(() =>
            {
                using (modelLock.Read())
                {
                    return true;
                }
            });
            Assert.True(second.Wait(TimeSpan.FromSeconds(5)) && second.Result);

            Task writer = Task.Run(() =>
            {
                using (modelLock.Write())
                {
                }
            });
            Assert.False(writer.Wait(TimeSpan.FromMilliseconds(200)));
            Task.Run(() => { }).Wait();
        }
        // Reader released: the queued writer proceeds
        Assert.True(Task.Run(() => { using (modelLock.Write()) { } }).Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ReadingAndWritingInParallelGivesTheSameFile()
    {
        byte[] reference;
        using (GameMakerData data = Load())
        using (MemoryStream output = new())
        {
            GameMakerIO.Write(output, data);
            reference = output.ToArray();
        }

        byte[][] results = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                using GameMakerData data = Load();
                GlobalDecompileContext context = new(data);
                foreach (GameMakerCode code in data.Code.Take(20))
                {
                    Decompile(context, code);
                }
                using MemoryStream output = new();
                GameMakerIO.Write(output, data);
                if (!output.ToArray().AsSpan().SequenceEqual(reference))
                {
                    return Array.Empty<byte>();
                }
            }
            return reference;
        })).ToArray().Select(t => t.Result).ToArray();

        Assert.All(results, r => Assert.Equal(reference.Length, r.Length));
    }

    [Fact]
    public void ContextsBuiltConcurrentlyForOneModelAgree()
    {
        using GameMakerData data = Load();
        GlobalDecompileContext first = new(data);
        List<GameMakerCode> codes = [.. data.Code.Take(40)];
        Dictionary<GameMakerCode, string> expected = codes.ToDictionary(c => c, c => Decompile(first, c));

        Task[] jobs = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int pass = 0; pass < 3; pass++)
            {
                GlobalDecompileContext context = new(data);
                context.PrepareForCompilation();
                foreach (GameMakerCode code in codes)
                {
                    Assert.Equal(expected[code], Decompile(context, code));
                }
            }
        })).ToArray();

        Task.WaitAll(jobs);
    }
}
