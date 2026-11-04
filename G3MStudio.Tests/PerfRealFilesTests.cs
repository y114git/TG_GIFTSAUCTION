#nullable disable
#pragma warning disable CA2025, CA2000
#pragma warning disable CA1849 // the timings call the blocking forms on purpose
using System.Diagnostics;
using System.Globalization;
using G3MLib.DataFile.References;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Edit and open timings on real data files. Runs only when G3MSTUDIO_PERF is set and G3MSTUDIO_PERF_FILES lists data files separated by semicolons.
/// Numbers go to the test output (use the detailed console logger).
/// </summary>
public sealed class PerfRealFilesTests(ITestOutputHelper output)
{
    public static IEnumerable<string> Files =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G3MSTUDIO_PERF")) ? [] : (Environment.GetEnvironmentVariable("G3MSTUDIO_PERF_FILES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists);

    private double Time(string label, Action action)
    {
        var watch = Stopwatch.StartNew();
        action();
        var ms = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"  {label}: {ms:F1} ms");
        return ms;
    }

    [Fact]
    public async Task OpenTimesOnRealFiles()
    {
        Assert.SkipWhen(!Files.Any(), "G3MSTUDIO_PERF and G3MSTUDIO_PERF_FILES are not set");
        foreach (var file in Files)
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            var service = new WorkspaceService(userData, RecentFiles.Load(userData));
            var timings = new List<string>();
            for (var round = 0; round < 2; round++)
            {
                var watch = Stopwatch.StartNew();
                using (var bare = await G3MLib.DataFile.GameMakerIO.ReadAsync(file, new Progress<G3MLib.DataFile.GameMakerProgress>(), TestContext.Current.CancellationToken)) timings.Add($"bare read {watch.Elapsed.TotalMilliseconds:F0}");
                watch.Restart();
                _ = await DataFileIdentity.ComputeAsync(file, TestContext.Current.CancellationToken);
                timings.Add($"identity hash {watch.Elapsed.TotalMilliseconds:F0}");
                watch.Restart();
                var workspace = await service.OpenAsync(file, null, TestContext.Current.CancellationToken);
                timings.Add($"OpenAsync {watch.Elapsed.TotalMilliseconds:F0}");
                watch.Restart();
                var model = new ExplorerModel(key => key);
                model.Load(workspace.Data);
                timings.Add($"explorer model {watch.Elapsed.TotalMilliseconds:F0}, {model.Total} entries");
                watch.Restart();
                _ = LoadChecks.Find(workspace.Data).ToList();
                timings.Add($"LoadChecks {watch.Elapsed.TotalMilliseconds:F0}");
                service.Close();
            }

            output.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(file))} ({new FileInfo(file).Length / 1048576} MB), ms, second round in brackets: " + string.Join("; ", timings.Take(5).Zip(timings.Skip(5), (first, second) => first + " [" + second.Split(' ')[^1].TrimEnd(',') + "]")));
        }
    }

    [Fact]
    public async Task MemoryBeforeAndAfterTrimOnRealFiles()
    {
        Assert.SkipWhen(!Files.Any(), "G3MSTUDIO_PERF and G3MSTUDIO_PERF_FILES are not set");
        static double Mb() => GC.GetTotalMemory(true) / 1048576.0;
        foreach (var file in Files)
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            var service = new WorkspaceService(userData, RecentFiles.Load(userData));
            var workspace = await service.OpenAsync(file, null, TestContext.Current.CancellationToken);
            var open = Mb();
            BuildIndex(workspace, TestContext.Current.CancellationToken);
            var withIndex = Mb();
            ReferenceCache.Of(workspace.Data).Invalidate();
            var indexDropped = Mb();
            BuildIndex(workspace, TestContext.Current.CancellationToken);
            using (workspace.Lock.Read()) _ = workspace.DecompileContext();
            var caches = Mb();
            output.WriteLine($"  open {open:F0}, index {withIndex:F0}, index dropped {indexDropped:F0}");
            var governor = new MemoryGovernor(new FakeHigh()) { LimitBytes = 1 };
            governor.OnPressure(MemoryLevel.High, () =>
            {
                ReferenceCache.Of(workspace.Data).Invalidate();
                workspace.ReleaseDecompileContext();
            });
            governor.Trim = () => { };
            governor.Poll();
            var trimmed = Mb();
            output.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(file))} ({new FileInfo(file).Length / 1048576} MB): managed heap after open {open:F0} MB, with reference index and decompile context {caches:F0} MB, after the release at High {trimmed:F0} MB");
            service.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void BuildIndex(Workspace workspace, CancellationToken token) => ReferenceCache.Of(workspace.Data).Get(workspace.Data, token);

    private sealed class FakeHigh : IMemorySource
    {
        public MemorySample Read() => new(900, 0, 0); // 900 bytes against a limit of 1: High
    }

    [Fact]
    public async Task EditsOnRealFiles()
    {
        Assert.SkipWhen(!Files.Any(), "G3MSTUDIO_PERF and G3MSTUDIO_PERF_FILES are not set");
        foreach (var file in Files)
        {
            using var dir = new TempDir();
            var userData = new UserData(dir.File("user"));
            var service = new WorkspaceService(userData, RecentFiles.Load(userData));
            var watch = Stopwatch.StartNew();
            var workspace = await service.OpenAsync(file, null, TestContext.Current.CancellationToken);
            var data = workspace.Data;
            output.WriteLine($"{file} ({new FileInfo(file).Length / 1048576} MB): open {watch.Elapsed.TotalMilliseconds:F0} ms; sprites {data.Sprites.Count}, objects {data.GameObjects.Count}, scripts {data.Scripts.Count}, code {data.Code.Count}, strings {data.Strings.Count}, rooms {data.Rooms.Count}, fonts {data.Fonts.Count}");
            for (var i = 0; i < 3; i++) Time("ReferenceIndex.Build", () => ReferenceIndex.Build(data));

            var obj = data.GameObjects.Select(o => o.Name.Content).First(n => n.StartsWith("obj", StringComparison.Ordinal));
            var sprite = data.Sprites[data.Sprites.Count / 2].Name.Content;
            Time("index built in the background while the rename dialog is open", () => ReferenceCache.Of(data).Get(data));
            for (var i = 0; i < 3; i++) Time("rename object " + i, () => workspace.Edit(new RenameResourceCommand("objects", i == 0 ? obj : obj + "_" + (i - 1), obj + "_" + i)));
            for (var i = 0; i < 2; i++) Time("rename sprite " + i, () => workspace.Edit(new RenameResourceCommand("sprites", i == 0 ? sprite : sprite + "_0", sprite + "_" + (i == 0 ? "0" : "1"))));
            for (var i = 0; i < 3; i++) Time("set property " + i, () => workspace.Edit(new SetPropertyCommand("objects", obj + "_2", "Depth", (i + 7).ToString(CultureInfo.InvariantCulture))));
            Time("undo", workspace.Undo);
            Time("redo", workspace.Redo);
            Time("UsageSearch.FindAsync", () => _ = UsageSearch.FindAsync(workspace, "sprites", sprite + "_1").GetAwaiter().GetResult());
            for (var n = 1; n < 40; n++)
            {
                var victim = data.GameObjects[^n].Name.Content;
                var done = false;
                var ms = Time("delete flow (usage + clear + delete) " + victim, () =>
                {
                    var hits = UsageSearch.FindAsync(workspace, "objects", victim).GetAwaiter().GetResult();
                    try { workspace.Edit(hits.Count == 0 ? new ResourceListCommand("objects", ResourceListCommand.Delete, victim) : new GroupCommand([new ClearReferencesCommand("objects", victim), new ResourceListCommand("objects", ResourceListCommand.Delete, victim)])); done = true; }
                    catch (InvalidOperationException e) { output.WriteLine("    refused: " + e.Message); }
                });
                if (done) break;
            }

            var script = data.Code.FirstOrDefault(c => c.Name.Content.StartsWith("gml_Script_", StringComparison.Ordinal) && c.Instructions.Count > 20);
            if (script is not null)
            {
                string text = "";
                Time("decompile " + script.Name.Content, () => { using (workspace.Lock.Read()) text = CodeText.Decompile(data, script.Name.Content, workspace.DecompileContext()); });
                for (var i = 0; i < 3; i++) Time("EditCodeCommand " + i, () => workspace.Edit(new EditCodeCommand(script.Name.Content, text + "\n// " + i)));
            }

            if (data.Fonts.Count > 0 && File.Exists(@"C:\Windows\Fonts\arial.ttf"))
            {
                var font = data.Fonts[0].Name.Content;
                var bytes = File.ReadAllBytes(@"C:\Windows\Fonts\arial.ttf");
                try
                {
                    var replace = new FontReplaceCommand(font, bytes);
                    Time("FontReplaceCommand.Prepare (off the UI thread) " + font, () => { using (workspace.Lock.Read()) replace.Prepare(data); });
                    Time("FontReplaceCommand edit under the lock", () => workspace.Edit(replace));
                }
                catch (InvalidOperationException e) { output.WriteLine("  font replace refused: " + e.Message); }
            }

            service.Close();
        }
    }
}
