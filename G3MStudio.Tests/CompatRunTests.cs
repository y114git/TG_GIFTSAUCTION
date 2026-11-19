using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A machine in memory: Unix paths, the files and folders given, a PATH of named programs.</summary>
internal sealed class FakeProbe(bool mac = false) : ICompatProbe
{
    private readonly Dictionary<string, string> _files = [];
    private readonly HashSet<string> _folders = [];

    public Dictionary<string, string> Environment { get; } = [];

    public Dictionary<string, string> Path { get; } = [];

    public bool IsWindows => false;

    public bool IsMacOS => mac;

    public string Home => "/home/u";

    /// <summary>Adds a file with its text, and its folders.</summary>
    public FakeProbe File(string path, string text = "")
    {
        _files[path] = text;
        for (var folder = CompatRun.Parent(path); folder is not "/" and not "."; folder = CompatRun.Parent(folder)) _folders.Add(folder);
        return this;
    }

    /// <summary>Adds a program that is on PATH.</summary>
    public FakeProbe OnPath(string name, string where = "/usr/bin")
    {
        Path[name] = where + "/" + name;
        return File(where + "/" + name);
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _folders.Contains(path.TrimEnd('/'));

    public IReadOnlyList<string> Files(string folder) => [.. _files.Keys.Where(f => CompatRun.Parent(f) == folder.TrimEnd('/'))];

    public IReadOnlyList<string> Folders(string folder) => [.. _folders.Where(f => CompatRun.Parent(f) == folder.TrimEnd('/'))];

    public string? ReadText(string path) => _files.GetValueOrDefault(path);

    public string? Env(string name) => Environment.GetValueOrDefault(name);

    string? ICompatProbe.OnPath(string name) => Path.GetValueOrDefault(name);
}

internal static class ProcessInfoExtensions
{
    public static string[] Args(this System.Diagnostics.ProcessStartInfo info) => [.. info.ArgumentList];
}

public sealed class CompatRunTests
{
    private static readonly string[] ProtonNames = ["Proton 9.0", "Proton 10.0", "Proton 8.0", "Proton 10.1"];

    private const string Game = "/home/u/Games/DELTARUNE/versions/WINDOWS/DELTARUNE_LATEST/chapter1_windows";
    private const string Data = Game + "/data.win";
    private const string Exe = Game + "/DELTARUNE.exe";

    private static FakeProbe Linux() => new FakeProbe().File(Data).File(Exe);

    private static CompatOptions Options(CompatMode mode = CompatMode.Auto, string program = "", string prefix = "", string command = "", string environment = "", string workingDir = "") =>
        new(mode, program, prefix, command, environment, workingDir);

    private static CompatDecision Decide(ICompatProbe probe, CompatOptions? options = null, string data = Data, string? explicitRunner = null) =>
        CompatRun.Decide(probe, options ?? Options(), data, "DELTARUNE.exe", explicitRunner);

    private static CompatLaunch Launch(CompatDecision decision)
    {
        Assert.Equal(CompatKind.Compat, decision.Kind);
        return decision.Launch!;
    }

    // ---- Auto on a plain Linux ----

    [Fact]
    public void WithOnlyAnExeAndNoToolTheDecisionIsNoTool()
    {
        var decision = Decide(Linux());
        Assert.Equal(CompatKind.NoTool, decision.Kind);
        Assert.Equal(Exe, decision.Runner);
    }

    [Fact]
    public void WineOnPathStartsTheExeWithTheDefaultPrefixAndQuietLogging()
    {
        var launch = Launch(Decide(Linux().OnPath("wine")));
        Assert.Equal(("/usr/bin/wine", CompatMode.Wine), (launch.Program, launch.Tool));
        Assert.Equal([Exe], launch.Leading);
        Assert.Equal("/home/u/.wine", launch.Prefix);
        Assert.Equal("-all", launch.Environment["WINEDEBUG"]);
        Assert.DoesNotContain("WINEPREFIX", launch.Environment.Keys);
    }

    [Fact]
    public void Wine64IsTheFallbackNameAndWineWinsWhenBothExist()
    {
        Assert.Equal("/usr/bin/wine64", Launch(Decide(Linux().OnPath("wine64"))).Program);
        Assert.Equal("/usr/bin/wine", Launch(Decide(Linux().OnPath("wine64").OnPath("wine"))).Program);
    }

    [Fact]
    public void AWinePrefixSettingBecomesWineprefixAndAUserWinedebugIsKept()
    {
        var probe = Linux().OnPath("wine");
        var launch = Launch(Decide(probe, Options(prefix: "~/prefixes/dr")));
        Assert.Equal("/home/u/prefixes/dr", launch.Environment["WINEPREFIX"]);
        Assert.Equal("/home/u/prefixes/dr", launch.Prefix);
        Assert.DoesNotContain("WINEDEBUG", Launch(Decide(probe, Options(environment: "A=1;WINEDEBUG=+timestamp"))).Environment.Keys);
        probe.Environment["WINEDEBUG"] = "err+all";
        Assert.DoesNotContain("WINEDEBUG", Launch(Decide(probe)).Environment.Keys);
    }

    [Fact]
    public void ExeNamesMatchInAnyLetterCaseAndOneFolderUp()
    {
        var probe = new FakeProbe().File(Game + "/assets/data.win").File(Game + "/deltarune.EXE").OnPath("wine");
        Assert.Equal(Game + "/deltarune.EXE", Launch(Decide(probe, data: Game + "/assets/data.win")).Exe);
        var runner = new FakeProbe().File(Data).File(Game + "/Runner.exe").OnPath("wine");
        Assert.Equal(Game + "/Runner.exe", Launch(Decide(runner)).Exe);
    }

    [Fact]
    public void ANativeRunnerWinsOverAnExeAndNativeModeNeverUsesATool()
    {
        var probe = Linux().File(Game + "/DELTARUNE.x86_64").OnPath("wine");
        var decision = Decide(probe);
        Assert.Equal((CompatKind.Native, Game + "/DELTARUNE.x86_64", RunnerCheck.Known), (decision.Kind, decision.Runner, decision.Check));
        Assert.Equal(CompatKind.Native, Decide(Linux().File(Game + "/DELTARUNE.sh").OnPath("wine")).Kind); // a script named after the game is native
        Assert.Equal(CompatKind.None, Decide(Linux().OnPath("wine"), Options(CompatMode.Native)).Kind);
        Assert.Equal(CompatKind.Native, Decide(probe, Options(CompatMode.Native)).Kind);
        Assert.Equal(CompatKind.None, Decide(new FakeProbe().File(Data).File(Game + "/start.sh")).Kind); // an unknown script is not a runner
    }

    [Fact]
    public void AnExplicitExeRunnerGoesThroughTheToolAndAnExplicitNativeOneStaysNative()
    {
        var probe = new FakeProbe().File(Data).OnPath("wine").File("/opt/gm/Runner.exe");
        Assert.Equal("/opt/gm/Runner.exe", Launch(Decide(probe, explicitRunner: "/opt/gm/Runner.exe")).Exe);
        Assert.Equal(CompatKind.Native, Decide(probe, explicitRunner: "/opt/gm/runner").Kind);
        Assert.Equal(CompatKind.Native, Decide(probe, Options(CompatMode.Native), explicitRunner: "/opt/gm/Runner.exe").Kind);
    }

    // ---- Steam and Proton ----

    private const string Steamapps = "/home/u/.local/share/Steam/steamapps";
    private const string SteamData = Steamapps + "/common/DELTARUNE/data.win";

    private static FakeProbe SteamGame(string library = "/home/u/.local/share/Steam")
    {
        var probe = new FakeProbe().File(library + "/steamapps/common/DELTARUNE/data.win").File(library + "/steamapps/common/DELTARUNE/DELTARUNE.exe");
        probe.File(library + "/steamapps/appmanifest_1671210.acf", "\"AppState\"\n{\n\t\"appid\"\t\t\"1671210\"\n\t\"installdir\"\t\t\"DELTARUNE\"\n}");
        probe.File(library + "/steamapps/appmanifest_5.acf", "\"AppState\"\n{\n\t\"appid\"\t\t\"5\"\n\t\"installdir\"\t\t\"Other\"\n}");
        return probe;
    }

    private static FakeProbe WithProton(FakeProbe probe, params string[] folders)
    {
        foreach (var f in folders) probe.File(f + "/proton");
        return probe;
    }

    [Fact]
    public void ASteamGameUsesTheNewestProtonOfSteamWithItsCompatdata()
    {
        const string common = "/home/u/.local/share/Steam/steamapps/common";
        var probe = WithProton(SteamGame(), common + "/Proton 9.0", common + "/Proton 10.0", common + "/Proton 8.0", "/home/u/.local/share/Steam/compatibilitytools.d/GE-Proton1-1");
        probe.File(Steamapps + "/compatdata/1671210/pfx/system.reg").OnPath("wine");
        var launch = Launch(Decide(probe, data: SteamData));
        Assert.Equal((CompatMode.Proton, common + "/Proton 10.0/proton", "Proton 10.0"), (launch.Tool, launch.Program, launch.Detail));
        Assert.Equal(["run", Steamapps + "/common/DELTARUNE/DELTARUNE.exe"], launch.Leading);
        Assert.Equal(Steamapps + "/compatdata/1671210", launch.Environment["STEAM_COMPAT_DATA_PATH"]);
        Assert.Equal("/home/u/.local/share/Steam", launch.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
        Assert.Equal(Steamapps + "/compatdata/1671210/pfx", launch.Prefix);
        Assert.Null(launch.NewPrefix); // the prefix exists, nothing to announce
        Assert.Equal(Steamapps + "/compatdata/1671210/pfx/drive_c/users/steamuser/AppData/Local/DELTARUNE", launch.SteamSaves);
    }

    [Fact]
    public void ProtonExperimentalCountsAsNewestAndAMissingCompatdataIsAnnounced()
    {
        const string common = "/home/u/.local/share/Steam/steamapps/common";
        var launch = Launch(Decide(WithProton(SteamGame(), common + "/Proton 9.0", common + "/Proton - Experimental", common + "/Proton 10.0"), data: SteamData));
        Assert.Equal("Proton - Experimental", launch.Detail);
        Assert.Equal(Steamapps + "/compatdata/1671210", launch.NewPrefix);
    }

    [Fact]
    public void ProtonAndTheGameLiveInAnExtraSteamLibraryFromLibraryfolders()
    {
        const string library = "/mnt/games/SteamLibrary";
        var probe = SteamGame(library);
        probe.File("/home/u/.local/share/Steam/steamapps/libraryfolders.vdf", "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"/home/u/.local/share/Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + library + "\"\n\t}\n}");
        probe.File("/home/u/.local/share/Steam/steamapps/common/Proton 8.0/proton");
        WithProton(probe, library + "/steamapps/common/Proton 9.0");
        var launch = Launch(Decide(probe, data: library + "/steamapps/common/DELTARUNE/data.win"));
        Assert.Equal(library + "/steamapps/common/Proton 9.0/proton", launch.Program);
        Assert.Equal(library + "/steamapps/compatdata/1671210", launch.Environment["STEAM_COMPAT_DATA_PATH"]);
    }

    [Fact]
    public void SteamRootComesFromTheEnvironmentFirstAndFlatpakSteamIsFound()
    {
        var flatpak = "/home/u/.var/app/com.valvesoftware.Steam/.local/share/Steam";
        var probe = SteamGame(flatpak);
        probe.File(flatpak + "/compatibilitytools.d/GE-Proton9-20/proton");
        var launch = Launch(Decide(probe, data: flatpak + "/steamapps/common/DELTARUNE/data.win"));
        Assert.Equal(flatpak, launch.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
        var env = SteamGame().File("/custom/steam/compatibilitytools.d/P/proton");
        env.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = "/custom/steam";
        Assert.Equal("/custom/steam", Launch(Decide(env, data: SteamData)).Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
    }

    [Fact]
    public void ASteamGameWithoutProtonFallsBackToWineOnTheSteamPrefixAndKeepsTheHint()
    {
        var probe = SteamGame().OnPath("wine").File(Steamapps + "/compatdata/1671210/pfx/system.reg");
        var launch = Launch(Decide(probe, data: SteamData));
        Assert.Equal(CompatMode.Wine, launch.Tool);
        Assert.Equal(Steamapps + "/compatdata/1671210/pfx", launch.Environment["WINEPREFIX"]);
        Assert.NotNull(launch.SteamSaves);
        // an explicit prefix beats the Steam one; a Steam prefix that is not there is not created by Wine
        Assert.Equal("/x/p", Launch(Decide(probe, Options(prefix: "/x/p"), SteamData)).Environment["WINEPREFIX"]);
        Assert.DoesNotContain("WINEPREFIX", Launch(Decide(SteamGame().OnPath("wine"), data: SteamData)).Environment.Keys);
    }

    [Fact]
    public void AGameOutsideSteamOrWithoutAManifestIsNotASteamGame()
    {
        Assert.Null(Launch(Decide(Linux().OnPath("wine"))).SteamSaves);
        var noManifest = new FakeProbe().File(SteamData).File(Steamapps + "/common/DELTARUNE/DELTARUNE.exe").OnPath("wine");
        Assert.Null(Launch(Decide(noManifest, data: SteamData)).SteamSaves);
    }

    [Fact]
    public void NaturalOrderPutsNineBeforeTen()
    {
        var names = ProtonNames.OrderByDescending(n => n, Comparer<string>.Create(CompatRun.Natural));
        Assert.Equal(["Proton 10.1", "Proton 10.0", "Proton 9.0", "Proton 8.0"], names);
    }

    // ---- umu and PortProton ----

    [Fact]
    public void UmuComesBeforeWineWithAGameIdAndThePrefixAndProtonSettings()
    {
        var probe = Linux().OnPath("wine").OnPath("umu-run", "/home/u/.local/bin");
        var launch = Launch(Decide(probe));
        Assert.Equal((CompatMode.Umu, "/home/u/.local/bin/umu-run", "umu-0"), (launch.Tool, launch.Program, launch.Environment["GAMEID"]));
        Assert.Equal("/home/u/Games/umu/umu-0", launch.Prefix);
        probe.File("/opt/GE/proton");
        var forced = Launch(Decide(probe, Options(CompatMode.Umu, "/opt/GE", "/x/p")));
        Assert.Equal(("/opt/GE", "/x/p"), (forced.Environment["PROTONPATH"], forced.Environment["WINEPREFIX"]));
        Assert.Equal("umu-1671210", Launch(Decide(SteamGame().OnPath("umu-run"), data: SteamData)).Environment["GAMEID"]);
    }

    [Fact]
    public void PortProtonIsUsedOnlyWhenNothingElseIsThere()
    {
        Assert.Equal("/usr/bin/portproton", Launch(Decide(Linux().OnPath("portproton"))).Program);
        Assert.Equal("/usr/bin/wine", Launch(Decide(Linux().OnPath("portproton").OnPath("wine"))).Program);
        var flatpak = Linux().OnPath("flatpak").File("/home/u/.local/share/flatpak/app/ru.linux_gaming.PortProton/current/active/files/x");
        var launch = Launch(Decide(flatpak));
        Assert.Equal(("/usr/bin/flatpak", CompatMode.PortProton), (launch.Program, launch.Tool));
        Assert.Equal(["run", "ru.linux_gaming.PortProton", Exe], launch.Leading);
        var script = Launch(Decide(Linux().File("/home/u/PortProton/data/scripts/start.sh")));
        Assert.Equal("/home/u/PortProton/data/scripts/start.sh", script.Program);
        Assert.Equal([Exe], script.Leading);
    }

    // ---- forced modes ----

    [Fact]
    public void AForcedModeUsesOnlyItsToolAndAMissingToolIsNoTool()
    {
        var probe = Linux().OnPath("wine").OnPath("umu-run");
        Assert.Equal(CompatMode.Wine, Launch(Decide(probe, Options(CompatMode.Wine))).Tool);
        Assert.Equal(CompatKind.NoTool, Decide(probe, Options(CompatMode.Proton)).Kind);
        Assert.Equal(CompatKind.NoTool, Decide(probe, Options(CompatMode.PortProton)).Kind);
        Assert.Equal(CompatKind.NoTool, Decide(Linux().OnPath("wine"), Options(CompatMode.Umu)).Kind);
        Assert.Equal(CompatKind.NoTool, Decide(probe, Options(CompatMode.Wine, "/nowhere/wine")).Kind);
        Assert.Equal(CompatKind.NoTool, Decide(probe, Options(CompatMode.Custom)).Kind); // no command
        Assert.Equal("/opt/w/bin/wine", Launch(Decide(probe.File("/opt/w/bin/wine"), Options(CompatMode.Wine, "/opt/w/bin/wine"))).Program);
    }

    [Fact]
    public void AForcedWineIgnoresANativeRunnerNextToTheData()
    {
        var probe = Linux().File(Game + "/DELTARUNE.x86_64").OnPath("wine");
        Assert.Equal(CompatMode.Wine, Launch(Decide(probe, Options(CompatMode.Wine))).Tool);
    }

    [Fact]
    public void ForcedProtonOutsideSteamUsesTheSettingPrefixOrAFolderUnderTheStudioData()
    {
        var probe = Linux().File("/opt/Proton 9/proton").File("/home/u/.steam/steam/steamapps/x");
        var chosen = Launch(Decide(probe, Options(CompatMode.Proton, "/opt/Proton 9", "/x/pfxroot/pfx")));
        Assert.Equal("/x/pfxroot", chosen.Environment["STEAM_COMPAT_DATA_PATH"]);
        var fallback = Launch(Decide(probe, Options(CompatMode.Proton, "/opt/Proton 9/proton")));
        Assert.Equal("/home/u/.local/share/G3MStudio/proton/DELTARUNE", fallback.Environment["STEAM_COMPAT_DATA_PATH"]);
        Assert.Equal("/home/u/.local/share/G3MStudio/proton/DELTARUNE", fallback.NewPrefix);
    }

    // ---- custom command ----

    [Fact]
    public void ACustomCommandExpandsItsTokensAndKeepsQuotedPathsWhole()
    {
        var probe = new FakeProbe().File("/home/u/My Games/DR/data.win").File("/home/u/My Games/DR/DELTARUNE.exe").OnPath("gamemoderun");
        var template = "gamemoderun wine \"{exe}\" --prefix \"{prefix}\" --dir {dir} {args}";
        var decision = CompatRun.Decide(probe, Options(CompatMode.Custom, prefix: "/home/u/my prefix", command: template), "/home/u/My Games/DR/data.win", "DELTARUNE.exe");
        var launch = Launch(decision);
        Assert.Equal("/usr/bin/gamemoderun", launch.Program);
        Assert.Equal(["wine", "/home/u/My Games/DR/DELTARUNE.exe", "--prefix", "/home/u/my prefix", "--dir", "/home/u/My Games/DR", "{args}"], launch.Leading);
        Assert.True(launch.InlineArgs);
        Assert.Equal("/home/u/my prefix", launch.Environment["WINEPREFIX"]);
    }

    [Fact]
    public void ACustomCommandWithAMissingProgramIsNoTool() =>
        Assert.Equal(CompatKind.NoTool, Decide(Linux(), Options(CompatMode.Custom, command: "nosuchtool {exe}")).Kind);

    // ---- macOS ----

    private const string MacGame = "/Users/u/Games/Undertale";
    private const string MacData = MacGame + "/game.ios";

    [Fact]
    public void AMacBundleNextToTheDataWinsOverAnExeAndUsesTheInfoPlistName()
    {
        var plist = "<plist><dict><key>CFBundleExecutable</key>\n<string>Undertale Launcher</string></dict></plist>";
        var probe = new FakeProbe(mac: true).File(MacData).File(MacGame + "/UNDERTALE.exe").File(MacGame + "/Undertale.app/Contents/Info.plist", plist)
            .File(MacGame + "/Undertale.app/Contents/MacOS/Undertale Launcher").File(MacGame + "/Undertale.app/Contents/MacOS/other").OnPath("wine");
        var decision = CompatRun.Decide(probe, Options(), MacData, "UNDERTALE.exe");
        Assert.Equal((CompatKind.Native, MacGame + "/Undertale.app/Contents/MacOS/Undertale Launcher", RunnerCheck.Known), (decision.Kind, decision.Runner, decision.Check));
    }

    [Fact]
    public void ABundleWithoutAUsablePlistUsesTheOnlyFileInMacOsAndOneFolderUpCounts()
    {
        var probe = new FakeProbe(mac: true).File(MacGame + "/data/game.ios").File(MacGame + "/Game.app/Contents/Info.plist", "binary").File(MacGame + "/Game.app/Contents/MacOS/GameBin");
        Assert.Equal(MacGame + "/Game.app/Contents/MacOS/GameBin", CompatRun.Decide(probe, Options(), MacGame + "/data/game.ios", null).Runner);
        var two = new FakeProbe(mac: true).File(MacData).File(MacGame + "/G.app/Contents/MacOS/a").File(MacGame + "/G.app/Contents/MacOS/b");
        Assert.Equal(CompatKind.None, CompatRun.Decide(two, Options(), MacData, null).Kind);
        Assert.Equal(CompatKind.None, CompatRun.Decide(new FakeProbe().File(MacData).File(MacGame + "/G.app/Contents/MacOS/a"), Options(), MacData, null).Kind); // a bundle counts on macOS only
    }

    [Fact]
    public void OnMacOsWineComesFromHomebrewTheWineAppsCrossOverOrThePortingToolkit()
    {
        FakeProbe Mac() => new FakeProbe(mac: true).File(MacData).File(MacGame + "/UNDERTALE.exe");
        CompatLaunch Run(FakeProbe p) => Launch(CompatRun.Decide(p, Options(), MacData, "UNDERTALE.exe"));
        Assert.Equal("/opt/homebrew/bin/wine", Run(Mac().File("/opt/homebrew/bin/wine")).Program); // PATH of a desktop app does not hold Homebrew
        Assert.Equal("/usr/local/bin/wine64", Run(Mac().File("/usr/local/bin/wine64")).Program);
        Assert.Equal("/usr/local/bin/wine-stable", Run(Mac().File("/usr/local/bin/wine-stable")).Program);
        Assert.Equal("/Applications/Wine Stable.app/Contents/Resources/start/bin/wine", Run(Mac().File("/Applications/Wine Stable.app/Contents/Resources/start/bin/wine")).Program);
        Assert.Equal("/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine", Run(Mac().File("/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine")).Program);
        var toolkit = Run(Mac().File("/usr/local/bin/gameportingtoolkit"));
        Assert.Equal(["/home/u/.wine", MacGame + "/UNDERTALE.exe"], toolkit.Leading); // the script takes the prefix first
        Assert.Null(Run(Mac().OnPath("wine")).SteamSaves);
    }

    // ---- detection for Settings ----

    [Fact]
    public void DetectNamesTheToolAutoWouldUseAndIsNullWhenThereIsNone()
    {
        Assert.Null(CompatRun.Detect(Linux(), Options(), Data));
        Assert.Equal(CompatMode.Wine, CompatRun.Detect(new FakeProbe().OnPath("wine"), Options(CompatMode.Native), null)!.Tool); // the mode does not matter: this is what Auto picks
        Assert.Equal(CompatMode.Umu, CompatRun.Detect(Linux().OnPath("wine").OnPath("umu-run"), Options(), Data)!.Tool);
    }

    // ---- what the game gets on its command line ----

    private static RunSpec Spec(CompatLaunch launch, string data = Game + "/g3mstudio_run.tmp", string arguments = "", bool debug = false) =>
        new(launch.Program, data, arguments, Game, "", debug, launch);

    [Fact]
    public void WineGetsTheExeFirstAndTheDataFileAsANameOrAZPath()
    {
        var launch = Launch(Decide(Linux().OnPath("wine")));
        var plain = GameRun.Prepare(Spec(launch, arguments: "-x \"a b\""));
        Assert.Equal([Exe, "-game", "g3mstudio_run.tmp", "-x", "a b"], plain.Args());
        Assert.Equal("-all", plain.Environment["WINEDEBUG"]);
        Assert.Equal(Game, plain.WorkingDirectory);
        var elsewhere = GameRun.Prepare(Spec(launch, "/home/u/other dir/data.win", debug: true));
        Assert.Equal([Exe, "-game", "Z:\\home\\u\\other dir\\data.win", "-debugoutput", "Z:\\home\\u\\other dir\\data.win.gamelog.txt"], elsewhere.ArgumentList);
        Assert.Equal("Z:\\a\\b.win", CompatRun.WinePath("/a/b.win", Game));
        Assert.Equal("b.win", CompatRun.WinePath("/a/b.win", "/a/"));
    }

    [Fact]
    public void ProtonGetsRunAndTheUsersEnvironmentWinsOverTheToolsOwn()
    {
        const string common = "/home/u/.local/share/Steam/steamapps/common";
        var launch = Launch(Decide(WithProton(SteamGame(), common + "/Proton 9.0"), data: SteamData));
        var start = GameRun.Prepare(Spec(launch, "/home/u/.local/share/Steam/steamapps/common/DELTARUNE/g3mstudio_run.tmp") with { Environment = "WINEDEBUG=+all;STEAM_COMPAT_DATA_PATH=/mine", WorkingDir = common + "/DELTARUNE" });
        Assert.Equal(["run", common + "/DELTARUNE/DELTARUNE.exe", "-game", "g3mstudio_run.tmp"], start.Args());
        Assert.Equal(("+all", "/mine"), (start.Environment["WINEDEBUG"], start.Environment["STEAM_COMPAT_DATA_PATH"]));
        Assert.Equal("/home/u/.local/share/Steam", start.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
    }

    [Fact]
    public void ACustomCommandGetsTheGameArgumentsAtItsArgsToken()
    {
        var probe = Linux().OnPath("gamemoderun");
        var launch = Launch(Decide(probe, Options(CompatMode.Custom, command: "gamemoderun wine {exe} {args} --tail")));
        var start = GameRun.Prepare(Spec(launch, arguments: "-w"));
        Assert.Equal(["wine", Exe, "-game", "g3mstudio_run.tmp", "-w", "--tail"], start.Args());
        var data = Launch(Decide(probe, Options(CompatMode.Custom, command: "gamemoderun wine {exe} -game {data}")));
        Assert.Equal(["wine", Exe, "-game", "g3mstudio_run.tmp"], GameRun.Prepare(Spec(data)).Args());
        var none = Launch(Decide(probe, Options(CompatMode.Custom, command: "gamemoderun {exe}")));
        Assert.Equal(["wine", Exe, "-game", "g3mstudio_run.tmp", "-w"], GameRun.Prepare(Spec(Launch(Decide(probe, Options(CompatMode.Custom, command: "gamemoderun wine {exe}"))), arguments: "-w")).Args()); // no {args}: they go last
        Assert.Equal([Exe, "-game", "g3mstudio_run.tmp"], GameRun.Prepare(Spec(none)).Args());
    }

    [Fact]
    public void AnArgsTokenInsideALargerTokenQuotesEachArgumentSoAPathWithSpacesStaysWhole()
    {
        var launch = Launch(Decide(Linux().OnPath("sh"), Options(CompatMode.Custom, command: "sh -c \"wine {exe} {args}\"")));
        var start = GameRun.Prepare(Spec(launch, "/home/u/other dir/data.win", "-x \"a b\""));
        Assert.Equal(["-c", $"wine {Exe} -game \"Z:\\home\\u\\other dir\\data.win\" -x \"a b\""], start.Args());
    }

    [Fact]
    public void ANativeRunSpecIsUnchanged()
    {
        var start = GameRun.Prepare(new RunSpec("/g/runner", "/g/data.win", "-w {data}", "/g", "A=1", true));
        Assert.Equal(["-debugoutput", "/g/data.win.gamelog.txt", "-w", "/g/data.win"], start.Args());
        Assert.Equal("1", start.Environment["A"]);
    }
}
