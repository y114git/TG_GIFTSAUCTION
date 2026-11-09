#pragma warning disable CA1849 // the timings compare the blocking calls with their async twins on purpose
#pragma warning disable CA2000 // the data model owns what is added to it
using System.Diagnostics;
using G3MLib.DataFile;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Timings of startup, file open and Explorer rebuilds. Always runs, at a smaller size unless G3MSTUDIO_PERF is set; numbers go to the test output.</summary>
public sealed class PerfTimingTests(ITestOutputHelper output)
{
    private static void LoadLanguages() => Localizer.LoadEmbedded();

    private double Time(string label, Action action, int runs = 5)
    {
        var times = new List<double>();
        for (var i = 0; i < runs; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"{label}: first {times[0]:F1} ms, best {times.Min():F1} ms");
        return times.Min();
    }

    [Fact]
    public void LocalizerLoad()
    {
        Time("Localizer.LoadEmbedded (7 languages)", LoadLanguages);
    }

    [Fact]
    public void ExplorerRebuilds()
    {
        using var data = ExplorerModelTests.Generate(Perf.Count(50_000));
        var model = new ExplorerModel(key => key);
        Time("ExplorerModel.Load 50k", () => model.Load(data), 3);
        Time("ExplorerModel.Reload 50k (every edit)", model.Reload);
        Time("ExplorerModel.SetFilter 'e'", () => { model.SetFilter("e"); model.SetFilter(""); });
        Time("ExplorerModel.Toggle objects", () => model.Toggle("objects"));
    }

    [Fact]
    public async Task OpenFiles()
    {
        using var dir = new TempDir();
        using (var big = TestData.Create(Perf.Count(20_000))) TestData.Write(dir.File("big.win"), big);
        var files = new List<string> { dir.File("big.win"), Path.Combine(Corpus.Fixtures, "uty", "data.win") };
        foreach (var file in files)
        {
            var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
            for (var i = 0; i < 3; i++)
            {
                var raw = Stopwatch.StartNew();
                using (var read = await G3MLib.DataFile.GameMakerIO.ReadAsync(file, new Progress<G3MLib.DataFile.GameMakerProgress>(), TestContext.Current.CancellationToken)) output.WriteLine($"  bare ReadAsync: {raw.Elapsed.TotalMilliseconds:F1} ms {read.Code.Count}");
                raw.Restart();
                _ = await DataFileIdentity.ComputeAsync(file, TestContext.Current.CancellationToken);
                output.WriteLine($"  identity hash: {raw.Elapsed.TotalMilliseconds:F1} ms");
                var watch = Stopwatch.StartNew();
                var workspace = await service.OpenAsync(file, null, TestContext.Current.CancellationToken);
                var open = watch.Elapsed.TotalMilliseconds;
                var model = new ExplorerModel(key => key);
                watch.Restart();
                model.Load(workspace.Data);
                output.WriteLine($"{Path.GetFileName(file)} ({new FileInfo(file).Length / 1024} KB) run {i}: OpenAsync {open:F1} ms, explorer load {watch.Elapsed.TotalMilliseconds:F1} ms, {model.Total} entries");
            }

            service.Close();
        }
    }

    [Fact]
    public async Task PluginLoad()
    {
        using var dir = new TempDir();
        var host = new Core.Plugins.PluginHost(new UserData(dir.Path), new Core.Commands.CommandRegistry(), () => null, (_, _) => { });
        host.CreateTemplate("Timing");
        var watch = Stopwatch.StartNew();
        host.Load();
        output.WriteLine($"PluginHost.Load, one plugin, cold compile: {watch.Elapsed.TotalMilliseconds:F0} ms, state {host.Plugins[0].State}");
        watch.Restart();
        host.Load();
        output.WriteLine($"PluginHost.Load, one plugin, cached assembly: {watch.Elapsed.TotalMilliseconds:F0} ms");
        watch.Restart();
        host.Load();
        output.WriteLine($"PluginHost.Load, again: {watch.Elapsed.TotalMilliseconds:F0} ms");
        var main = Path.Combine(dir.Path, "plugins", "timing", "src", "Main.cs");
        File.WriteAllText(main, File.ReadAllText(main).Replace("Say Hello", "Say Goodbye", StringComparison.Ordinal));
        watch.Restart();
        var reload = host.ReloadAsync(host.Plugins[0]);
        output.WriteLine($"ReloadAsync after an edit, time until it returns control: {watch.Elapsed.TotalMilliseconds:F0} ms");
        await reload;
        output.WriteLine($"ReloadAsync after an edit, total: {watch.Elapsed.TotalMilliseconds:F0} ms");
        File.WriteAllText(main, File.ReadAllText(main).Replace("Say Goodbye", "Say Again", StringComparison.Ordinal));
        watch.Restart();
        host.Reload(host.Plugins[0]);
        output.WriteLine($"Reload after an edit (blocks the caller): {watch.Elapsed.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public void SentryInit()
    {
        var watch = Stopwatch.StartNew();
        CrashReporting.Apply(true, "https://0123456789abcdef0123456789abcdef@localhost:9/1");
        output.WriteLine($"CrashReporting.Apply with a key: {watch.Elapsed.TotalMilliseconds:F1} ms");
        CrashReporting.Apply(false);
    }

    [Fact]
    public async Task CodeEditAndRenameOnBigFile()
    {
        using var dir = new TempDir();
        using (var data = TestData.Create(Perf.Count(20_000), withCode: true))
        {
            for (var i = 0; i < Perf.Count(2000); i++) data.Code.Add(new G3MLib.DataFile.Models.GameMakerCode { Name = data.Strings.MakeString("gml_Object_obj_" + i + "_Create_0") });
            TestData.Write(dir.File("big.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        var workspace = await service.OpenAsync(dir.File("big.win"), null, TestContext.Current.CancellationToken);
        for (var i = 0; i < 4; i++)
        {
            var watch = Stopwatch.StartNew();
            workspace.Edit(new EditCodeCommand("gml_Script_probe", "return " + i + ";"));
            output.WriteLine($"EditCodeCommand on {workspace.Data.Code.Count} code entries, {workspace.Data.GameObjects.Count} objects: {watch.Elapsed.TotalMilliseconds:F1} ms");
        }

        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            workspace.Edit(new RenameResourceCommand("objects", "obj_" + i, "renamed_" + i));
            output.WriteLine($"RenameResourceCommand: {watch.Elapsed.TotalMilliseconds:F1} ms");
        }

        var text = Stopwatch.StartNew();
        using (workspace.Lock.Read()) _ = CodeText.Decompile(workspace.Data, "gml_Script_probe", workspace.DecompileContext());
        output.WriteLine($"DecompileContext + decompile: {text.Elapsed.TotalMilliseconds:F1} ms");
        service.Close();
    }

    [Fact]
    public async Task EditWithJournal()
    {
        using var dir = new TempDir();
        using (var data = TestData.Create(200)) TestData.Write(dir.File("small.win"), data);
        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        var workspace = await service.OpenAsync(dir.File("small.win"), null, TestContext.Current.CancellationToken);
        var times = new List<double>();
        for (var i = 0; i < 60; i++)
        {
            var watch = Stopwatch.StartNew();
            workspace.Edit(new SetPropertyCommand("objects", "obj_" + i, "Depth", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine($"Workspace.Edit on a small file: mean {times.Skip(5).Average():F2} ms, max {times.Max():F2} ms");
        service.Close();
    }

    [Fact]
    public async Task WindowStartupAndOpen()
    {
        using var dir = new TempDir();
        using (var big = TestData.Create(Perf.Count(20_000))) TestData.Write(dir.File("big.win"), big);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var userData = new UserData(dir.File("user"));
            var watch = Stopwatch.StartNew();
            var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
            output.WriteLine($"new MainWindow: {watch.Elapsed.TotalMilliseconds:F1} ms");
            watch.Restart();
            window.Show();
            output.WriteLine($"Show: {watch.Elapsed.TotalMilliseconds:F1} ms");
            watch.Restart();
            await window.OpenAsync(dir.File("big.win"));
            output.WriteLine($"window.OpenAsync 40k entries (read, explorer, layout): {watch.Elapsed.TotalMilliseconds:F1} ms");
            for (var i = 0; i < 3; i++)
            {
                watch.Restart();
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("objects", "obj_" + (Perf.Count(5000) + i)), false);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                output.WriteLine($"open object tab {i}: {watch.Elapsed.TotalMilliseconds:F1} ms");
            }

            var sample = window.Workspaces.Current!.Data.GameObjects[Perf.Count(5000)];
            var describe = new List<double>();
            for (var i = 0; i < 5; i++)
            {
                watch.Restart();
                var rows = PropertyModel.Describe(sample).ToList();
                describe.Add(watch.Elapsed.TotalMilliseconds);
                if (i == 0) output.WriteLine($"object has {rows.Count} property rows");
            }

            output.WriteLine($"PropertyModel.Describe(object): first {describe[0]:F1} ms, best {describe.Min():F1} ms");
            var edits = new List<double>();
            for (var i = 0; i < 12; i++)
            {
                watch.Restart();
                window.Workspaces.Current!.Edit(new SetPropertyCommand("objects", "obj_" + (Perf.Count(5000) + i), "Depth", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                if (i >= 2) edits.Add(watch.Elapsed.TotalMilliseconds);
            }

            output.WriteLine($"edit with all Changed handlers and layout: mean {edits.Average():F1} ms, min {edits.Min():F1} ms, max {edits.Max():F1} ms");

            var field = typeof(EditHistory).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
            var handlers = ((Delegate)field.GetValue(window.Workspaces.Current!.History)!).GetInvocationList();
            foreach (var handler in handlers)
            {
                watch.Restart();
                ((Action)handler)();
                var ran = watch.Elapsed.TotalMilliseconds;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                output.WriteLine($"Changed handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name}: {ran:F1} ms, with layout {watch.Elapsed.TotalMilliseconds:F1} ms");
            }

            for (var i = 0; i < 3; i++)
            {
                watch.Restart();
                window.Explorer.Refresh(true);
                var refresh = watch.Elapsed.TotalMilliseconds;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                output.WriteLine($"Explorer.Refresh {i}: {refresh:F1} ms, with jobs and layout {watch.Elapsed.TotalMilliseconds:F1} ms");
            }

            window.Workspaces.Close();
            window.Close();
        });
    }
}
