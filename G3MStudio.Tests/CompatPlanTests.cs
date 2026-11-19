using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>The compatibility layer through the run plan, the settings, the Settings page and the run log.</summary>
public sealed class CompatPlanTests
{
    private const string GameName = "DELTARUNE.exe";

    // The paths use '/' so a fake Linux machine can hold them, and lie in a real temp folder so run options files can be written beside them.
    private static string Folder(TempDir dir) => dir.File("game").Replace('\\', '/');

    private static FakeProbe Machine(string folder) => new FakeProbe().File(folder + "/data.win").File(folder + "/" + GameName);

    private static RunResolution Resolve(AppSettings settings, string folder, FakeProbe probe, UserData? home = null) =>
        RunPlan.Resolve(settings, folder + "/data.win", GameName, folder + "/g3mstudio_run.tmp", null, home, probe);

    [Fact]
    public void AnExeOnLinuxWithWineRunsThroughWineKeepingTheOptions()
    {
        using var dir = new TempDir();
        var folder = Folder(dir);
        var resolution = Resolve(new AppSettings { RunArguments = "-x", RunWinePrefix = "/p" }, folder, Machine(folder).OnPath("wine"));
        var spec = resolution.Spec!;
        Assert.Equal(("/usr/bin/wine", RunnerCheck.Known, folder + "/" + GameName), (spec.Runner, resolution.Check, resolution.Runner));
        Assert.Equal(("-x", folder + "/g3mstudio_run.tmp"), (spec.Arguments, spec.DataFile));
        Assert.Equal("/p", spec.Compat!.Environment["WINEPREFIX"]);
        Assert.False(resolution.NoTool);
    }

    [Fact]
    public void WithoutAToolTheResolutionSaysSoInsteadOfGuessing()
    {
        using var dir = new TempDir();
        var folder = Folder(dir);
        var auto = Resolve(new AppSettings(), folder, Machine(folder));
        Assert.Equal((null, true), (auto.Spec, auto.NoTool));
        var forced = Resolve(new AppSettings { RunCompat = CompatMode.Proton }, folder, Machine(folder).OnPath("wine"));
        Assert.Equal((null, true), (forced.Spec, forced.NoTool));
        var none = Resolve(new AppSettings(), folder, new FakeProbe().File(folder + "/data.win"));
        Assert.Equal((null, false, RunnerCheck.Missing), (none.Spec, none.NoTool, none.Check));
    }

    [Fact]
    public void ANativeRunnerStillResolvesAsBeforeOnLinux()
    {
        using var dir = new TempDir();
        var folder = Folder(dir);
        var resolution = Resolve(new AppSettings(), folder, Machine(folder).File(folder + "/DELTARUNE.x86_64").OnPath("wine"));
        Assert.Equal((folder + "/DELTARUNE.x86_64", RunnerCheck.Known), (resolution.Spec!.Runner, resolution.Check));
        Assert.Null(resolution.Spec.Compat);
        Assert.Equal(CompatKind.None, CompatRun.Decide(Machine(folder).OnPath("wine"), new CompatOptions(CompatMode.Native, "", "", "", "", ""), folder + "/data.win", GameName).Kind);
    }

    [Fact]
    public void AnOldRunOptionsFileLoadsWithTheCompatibilityDefaultsAndANewOneCarriesItsMode()
    {
        using var dir = new TempDir();
        var folder = Folder(dir);
        var home = new UserData(dir.File("user"));
        var probe = Machine(folder).OnPath("wine").OnPath("umu-run");
        var settings = new AppSettings();
        var path = RunPlan.StudioFile(home, folder + "/data.win");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"Name\":\"\",\"Runner\":\"\",\"Arguments\":\"-old\",\"DebugOutput\":false,\"WorkingDir\":\"\",\"Environment\":\"\"}");
        var old = Resolve(settings, folder, probe, home).Spec!;
        Assert.Equal(("-old", CompatMode.Umu), (old.Arguments, old.Compat!.Tool)); // Auto picks umu before wine

        var profile = new RunProfile("", "", "-new", false, Compat: CompatMode.Wine, CompatProgram: "", WinePrefix: "/pfx", CompatCommand: "");
        RunPlan.SaveProject(settings, home, folder + "/data.win", profile);
        Assert.Contains("\"Compat\": \"Wine\"", File.ReadAllText(path), StringComparison.Ordinal);
        var chosen = Resolve(settings, folder, probe, home).Spec!;
        Assert.Equal((CompatMode.Wine, "/pfx"), (chosen.Compat!.Tool, chosen.Compat.Environment["WINEPREFIX"]));
    }

    [Fact]
    public void ACompatCommandInARunFileBesideTheDataFileCountsOnlyOnceApproved()
    {
        using var dir = new TempDir();
        var folder = Folder(dir);
        var probe = Machine(folder).OnPath("wine").OnPath("sneaky");
        var beside = new AppSettings { RunOptionsLocation = RunOptionsLocation.BesideDataFile };
        var data = folder + "/data.win";
        Directory.CreateDirectory(dir.File("game"));
        File.WriteAllText(RunPlan.ProjectFile(data), "{\"Compat\":\"Custom\",\"CompatCommand\":\"sneaky {exe}\"}");
        Assert.NotNull(RunPlan.Unapproved(beside, data));
        Assert.Equal(CompatMode.Wine, Resolve(beside, folder, probe).Spec!.Compat!.Tool); // the settings are used, not the file

        var approved = beside with { TrustedRunFiles = [RunPlan.ProjectHash(data)!] };
        var spec = Resolve(approved, folder, probe).Spec!;
        Assert.Equal(("/usr/bin/sneaky", CompatMode.Custom), (spec.Runner, spec.Compat!.Tool));
    }

    [Fact]
    public void TheCompatSettingsAreSavedLoadedAndDefaultWhenAnOldFileLacksThem()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        using (var store = new SettingsStore(user))
        {
            Assert.Equal((CompatMode.Auto, "", "", ""), (store.Current.RunCompat, store.Current.RunCompatProgram, store.Current.RunWinePrefix, store.Current.RunCompatCommand));
            store.Update(x => x with { RunCompat = CompatMode.Custom, RunCompatProgram = "/x/wine", RunWinePrefix = "/x/p", RunCompatCommand = "gamemoderun wine {exe} {args}" });
        }

        Assert.Contains("\"runCompat\": \"Custom\"", File.ReadAllText(user.Combine("settings.json")), StringComparison.Ordinal);
        using var again = new SettingsStore(user);
        Assert.Equal((CompatMode.Custom, "/x/wine", "/x/p", "gamemoderun wine {exe} {args}"), (again.Current.RunCompat, again.Current.RunCompatProgram, again.Current.RunWinePrefix, again.Current.RunCompatCommand));

        var legacy = new UserData(dir.File("legacy"));
        File.WriteAllText(legacy.Combine("settings.json"), "{\"runRunner\":\"r\",\"runArguments\":\"-a\"}");
        using var old = new SettingsStore(legacy);
        Assert.Equal(("r", CompatMode.Auto, ""), (old.Current.RunRunner, old.Current.RunCompat, old.Current.RunCompatCommand));
    }

    [Fact]
    public void AProfileCarriesTheCompatibilityFieldsBothWays()
    {
        var settings = new AppSettings { RunCompat = CompatMode.Proton, RunCompatProgram = "/p", RunWinePrefix = "/w", RunCompatCommand = "c", RunArguments = "-a" };
        var profile = settings.ToRunProfile("P");
        Assert.Equal(new RunProfile("P", "", "-a", false, "", "", CompatMode.Proton, "/p", "/w", "c"), profile);
        Assert.Equal(profile, new AppSettings().WithRunProfile(profile).ToRunProfile("P"));
    }

    [Fact]
    public async Task TheRunPageShowsTheCompatibilityGroupAndTheCustomCommandOnlyForCustom()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                var mode = Named<ComboBox>(window, "RunCompat");
                Assert.Equal(7, mode.Items.Count);
                Assert.NotNull(Named<TextBox>(window, "RunCompatProgram"));
                Assert.NotNull(Named<Button>(window, "RunCompatBrowse"));
                Assert.NotNull(Named<TextBox>(window, "RunWinePrefix"));
                Assert.NotNull(Named<Button>(window, "RunCompatDetect"));
                Assert.NotNull(Named<TextBlock>(window, "RunCompatDetected"));
                var command = Named<TextBox>(window, "RunCompatCommand");
                Assert.True(Hidden(command));

                var settings = window.Workspaces.Settings;
                mode.SelectedIndex = (int)CompatMode.Custom;
                Assert.Equal(CompatMode.Custom, settings.Current.RunCompat);
                Assert.False(Hidden(command));
                command.Text = "wine {exe} {args}";
                Named<TextBox>(window, "RunWinePrefix").Text = " /my/prefix "; Dispatcher.UIThread.RunJobs(); // the box trims
                Assert.Equal(("wine {exe} {args}", "/my/prefix"), (settings.Current.RunCompatCommand, settings.Current.RunWinePrefix));
                mode.SelectedIndex = (int)CompatMode.Wine;
                Assert.True(Hidden(command));

                Named<TextBox>(window, "RunProfileName").Text = "Wine setup";
                Named<Button>(window, "SaveRunProfile").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal((CompatMode.Wine, "/my/prefix"), (settings.Current.RunProfiles[0].Compat, settings.Current.RunProfiles[0].WinePrefix));
            }
            finally { window.Close(); }
        });
    }

    // The row of a setting that applies only sometimes hides itself with this class.
    private static bool Hidden(Control control) => control.GetVisualAncestors().OfType<Control>().Any(a => a.Classes.Contains("selfHidden"));

    private static T Named<T>(MainWindow window, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    [Fact]
    public async Task TheNotesOfACompatRunGoIntoTheRunLogBeforeTheGameOutput()
    {
        using var dir = new TempDir();
        var live = new LiveRun();
        var spec = OperatingSystem.IsWindows()
            ? new RunSpec("cmd.exe", dir.File("data.win"), $"/s /c \"echo game\"", Notes: ["Running DELTARUNE.exe with Wine (prefix /home/u/.wine)", "hint"])
            : new RunSpec("sh", dir.File("data.win"), $"-c \"echo game\" {{data}}", Notes: ["Running DELTARUNE.exe with Wine (prefix /home/u/.wine)", "hint"]);
        Assert.True(await live.StartAsync(() => Task.FromResult<RunSpec?>(spec)));
        await live.WaitIdleAsync();
        var texts = live.Log.Lines.Select(l => l.Text).ToList();
        Assert.Equal("Running DELTARUNE.exe with Wine (prefix /home/u/.wine)", texts[0]);
        Assert.Equal("hint", texts[1]);
        Assert.Contains("game", texts);
        Assert.All(live.Log.Lines.Take(2), l => Assert.False(l.Error));
    }

    [Fact]
    public void EveryLanguageHasTheCompatTextsWithTheSameTokens()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "G3MStudio.Core", "Languages");
        string[] keys = ["compat.title", "compat.modeLabel", "compat.mode.Auto", "compat.mode.Native", "compat.mode.Wine", "compat.mode.Proton", "compat.mode.Umu", "compat.mode.PortProton", "compat.mode.Custom", "compat.program", "compat.browse", "compat.prefix", "compat.command", "compat.detected", "compat.detect", "compat.detectedTool", "compat.detectedPrefix", "compat.detectedNone", "compat.notNeeded", "compat.noTool", "compat.info", "compat.infoNoPrefix", "compat.steamHint", "compat.newPrefix", "live.trustBody"];
        string[] tokens = ["{exe}", "{data}", "{dir}", "{prefix}", "{args}", "{tool}", "{folder}", "{compat}"];
        Dictionary<string, string> Read(string language)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, $"lang_{language}.json")));
            return keys.ToDictionary(k => k, k => k.Split('.').Aggregate(doc.RootElement, (e, part) => e.GetProperty(part)).GetString()!);
        }

        var english = Read("en");
        foreach (var language in new[] { "ru", "es", "ja", "ko", "zh_cn", "zh_tw" })
        {
            var other = Read(language);
            foreach (var key in keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(other[key]), $"{language} {key}");
                Assert.DoesNotContain("—", other[key], StringComparison.Ordinal);
                if (key is not ("compat.mode.Wine" or "compat.mode.Proton" or "compat.mode.Umu" or "compat.mode.PortProton")) Assert.NotEqual(english[key], other[key]);
                Assert.Equal(tokens.Where(t => english[key].Contains(t, StringComparison.Ordinal)), tokens.Where(t => other[key].Contains(t, StringComparison.Ordinal)));
            }
        }
    }
}
