using System.Security.Cryptography;
using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class UtmtImportTests
{
    internal const string Comment = "// kept comment";

    /// <summary>A generated game, then a UTMT 0.9 project that adds one resource of most types the format holds, written by the library in UTMT layout.</summary>
    internal sealed class Fixture : IDisposable
    {
        public static readonly string[] Types = ["objects", "scripts", "paths", "sprites", "shaders", "rooms", "sequences", "animationCurves", "particleSystems", "sounds"];

        public TempDir Dir { get; } = new();

        public string BaseFile => Dir.File("game/data.win");

        public string Project => Dir.File("utmt");

        public string Main => Path.Combine(Project, "project.json");

        public string Out => Dir.File("out");

        public UserData UserData { get; }

        public ProjectService Projects { get; }

        public UtmtProjectImporter Importer { get; }

        public Fixture()
        {
            UserData = new UserData(Dir.File("user"));
            Projects = new ProjectService(UserData);
            Importer = new UtmtProjectImporter(Projects, new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "t", ["GIT_AUTHOR_EMAIL"] = "t@example.com", ["GIT_COMMITTER_NAME"] = "t", ["GIT_COMMITTER_EMAIL"] = "t@example.com" });
            Directory.CreateDirectory(Dir.File("game"));
            using var data = TestData.Create(1);
            TestData.AddPage(data);
#pragma warning disable CA2000 // the data model owns what is added to it
            data.AudioGroups.Add(new GameMakerAudioGroup { Name = data.Strings.MakeString("audiogroup_default"), Path = data.Strings.MakeString("") });
#pragma warning restore CA2000
            TestData.Write(BaseFile, data);
            foreach (var type in Types) ResourceTemplates.New(data, type, "mod_" + type).Apply(data);
            data.Sounds.Single(x => x.Name.Content == "mod_sounds").GroupID = data.GetBuiltinSoundGroupID();
            var group = new CodeImportGroup(data);
            group.QueueReplace("gml_Script_mod_scripts", "return 7;");
            group.Import();
            var context = new ProjectContext(data, BaseFile, Dir.File("game/out.win"), Main, "Test Mod");
            foreach (var asset in new IEnumerable<IProjectAsset>[] { data.Sprites, data.Sounds, data.Shaders, data.Rooms, data.GameObjects, data.Paths, data.Scripts, data.Sequences, data.AnimationCurves, data.ParticleSystems, data.Code }.SelectMany(a => a))
                if (asset is { ProjectExportable: true }) context.MarkAssetForExport(asset);
            context.Export(true);
            // The source a modder wrote keeps its comments; the export alone writes decompiled text.
            File.WriteAllText(Path.Combine(Project, "code", "gml_Script_mod_scripts.gml"), Comment + Environment.NewLine + "return 7;" + Environment.NewLine);
        }

        public UtmtImportRequest Request(bool git = true) => new(Project, BaseFile, Out, git);

        public void Dispose() => Dir.Dispose();
    }

    private static string Fingerprint(string folder) => string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        .Select(f => Path.GetRelativePath(folder, f) + "=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))));

    [Fact]
    public async Task ConvertsEveryResourceTypeKeepsTheCodeSourceAndSetsUpGit()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        var report = await f.Importer.ImportAsync(f.Request(), cancellationToken: ct);

        Assert.Equal(UtmtGitState.Initialized, report.Git);
        Assert.Empty(report.Warnings);
        Assert.True(report.Info.Total >= Fixture.Types.Length);
        var project = await G3MProjectService.OpenAsync(report.ProjectFile, null, ct);
        foreach (var (type, name) in new[] { ("Sprites", "mod_sprites"), ("Sounds", "mod_sounds"), ("Shaders", "mod_shaders"), ("Rooms", "mod_rooms"), ("Paths", "mod_paths"), ("Scripts", "mod_scripts"),
            ("AnimationCurves", "mod_animationCurves"), ("ParticleSystems", "mod_particleSystems"), ("Sequences", "mod_sequences"), ("CodeEntries", "gml_Script_mod_scripts") })
            Assert.Contains(project.OfType(type), r => r.Name == name);
        Assert.Contains(project.OfType("GameObjects"), r => r.Name.StartsWith("mod_objects", StringComparison.Ordinal));

        using var data = await GameMakerIO.ReadAsync(report.DataFile, cancellationToken: ct);
        Assert.Contains(data.Sprites, s => s.Name.Content == "mod_sprites");
        Assert.Contains(data.Sounds, s => s.Name.Content == "mod_sounds");
        Assert.Contains(data.Rooms, s => s.Name.Content == "mod_rooms");
        Assert.Contains(data.GameObjects, s => s.Name.Content == "obj_0");

        var gml = await File.ReadAllTextAsync(Path.Combine(f.Out, "code", "gml_Script_mod_scripts", "gml_Script_mod_scripts.gml"), ct);
        Assert.StartsWith(Comment, gml, StringComparison.Ordinal);

        var git = new GitService(f.Out);
        Assert.True(git.IsRepository);
        Assert.Single(await git.LogAsync(cancellationToken: ct));
        Assert.Empty(await git.StatusAsync(ct));

        var identity = await DataFileIdentity.ComputeAsync(report.DataFile, ct);
        Assert.NotNull(f.Projects.Registry.Find(identity));
    }

    [Fact]
    public async Task AFullDriveRefusesTheConversionBeforeAnythingIsWritten()
    {
        using var f = new Fixture();
        var importer = new UtmtProjectImporter(f.Projects) { FreeSpace = _ => 1024 };
        var error = await Assert.ThrowsAsync<UtmtImportException>(() => importer.ImportAsync(f.Request(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("Not enough free space", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public async Task TheOriginalProjectAndTheBaseDataFileAreNeverChanged()
    {
        using var f = new Fixture();
        var (project, game) = (Fingerprint(f.Project), Fingerprint(f.Dir.File("game")));
        await f.Importer.ImportAsync(f.Request(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(project, Fingerprint(f.Project));
        Assert.Equal(game, Fingerprint(f.Dir.File("game")));
    }

    [Fact]
    public async Task AnExistingRepositoryIsReusedWithoutACommit()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(f.Out);
        Assert.True((await new GitService(f.Out).InitAsync(ct)).Ok);
        var report = await f.Importer.ImportAsync(f.Request(git: false), cancellationToken: ct);
        Assert.Equal(UtmtGitState.Existing, report.Git);
        Assert.Empty(await new GitService(f.Out).LogAsync(cancellationToken: ct));
        Assert.Contains(await new GitService(f.Out).StatusAsync(ct), c => c.Path == "Test_Mod.g3mproj");
    }

    private sealed class Sync<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    [Fact]
    public async Task ACancelAfterTheExportLeavesNoRecordNoFolderAndNoBaseCopy()
    {
        using var f = new Fixture();
        using var cts = new CancellationTokenSource();
        var stage = new Sync<string>(s => { if (s == "git") cts.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Importer.ImportAsync(f.Request(), stage, cancellationToken: cts.Token));
        Assert.False(Directory.Exists(f.Out));
        Assert.Empty(f.Projects.Registry.Projects);
        Assert.False(Directory.Exists(f.UserData.Combine("projects")) && Directory.EnumerateFiles(f.UserData.Combine("projects"), "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task AGitSetupThatFailsIsReportedAsNoGitAndTheProjectIsStillRegistered()
    {
        using var f = new Fixture();
        // No identity anywhere: the first commit cannot be made.
        var importer = new UtmtProjectImporter(f.Projects, new Dictionary<string, string>
        {
            ["GIT_CONFIG_GLOBAL"] = f.Dir.File("no-such-config"),
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "user.useConfigOnly",
            ["GIT_CONFIG_VALUE_0"] = "true",
        });
        var report = await importer.ImportAsync(f.Request(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(UtmtGitState.None, report.Git);
        Assert.Contains(report.Warnings, w => w.Key == "gitFailed");
        Assert.Single(f.Projects.Registry.Projects);
    }

    [Fact]
    public async Task NoGitWhenTheUserDeclines()
    {
        using var f = new Fixture();
        var report = await f.Importer.ImportAsync(f.Request(git: false), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(UtmtGitState.None, report.Git);
        Assert.False(new GitService(f.Out).IsRepository);
    }

    [Fact]
    public async Task WhatTheProjectOptionsCannotCarryIsReported()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(f.Project, "notes"));
        var text = await File.ReadAllTextAsync(f.Main, ct);
        await File.WriteAllTextAsync(f.Main, text.Replace("\"Flags\": []", "\"Flags\": [\"_custom\"]", StringComparison.Ordinal), ct);
        var report = await f.Importer.ImportAsync(f.Request(git: false), cancellationToken: ct);
        Assert.Contains(report.Warnings, w => w is { Key: "options", Detail: "Flags" });
        Assert.Contains(report.Warnings, w => w is { Key: "ignoredFolders", Detail: "notes" });
    }

    [Fact]
    public async Task DetectFindsTheProjectByFolderOrFileAndNothingElse()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(f.Main, UtmtProjectImporter.Detect(f.Project));
        Assert.Equal(f.Main, UtmtProjectImporter.Detect(f.Main));
        Assert.Null(UtmtProjectImporter.Detect(f.Dir.File("game")));
        Assert.Null(UtmtProjectImporter.Detect(Path.Combine(f.Project, "objects", "mod_objects.json")));
        Assert.Null(UtmtProjectImporter.Detect(f.Dir.File("missing")));
        await File.WriteAllTextAsync(f.Dir.File("other.json"), "{\"Name\":\"x\"}", ct);
        Assert.Null(UtmtProjectImporter.Detect(f.Dir.File("other.json")));

        var info = UtmtProjectImporter.Inspect(f.Project);
        Assert.Equal("Test Mod", info.Name);
        Assert.Equal(1, info.Resources["sprites"]);
        Assert.Equal(1, info.Resources["code"]);
        Assert.Contains("rooms", info.Resources.Keys);
        Assert.False(info.InGitRepository);
    }

    [Fact]
    public async Task BrokenProjectsAreRefusedWithAReasonAndLeaveNothingBehind()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;

        // A resource file that is cut off.
        var file = Path.Combine(f.Project, "objects", "mod_objects.json");
        var good = await File.ReadAllTextAsync(file, ct);
        await File.WriteAllTextAsync(file, good[..(good.Length / 2)], ct);
        var failure = await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request(), cancellationToken: ct));
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.False(Directory.Exists(f.Out));
        Assert.Empty(f.Projects.Registry.Projects);

        // A sprite whose picture is missing.
        await File.WriteAllTextAsync(file, good, ct);
        File.Delete(Path.Combine(f.Project, "sprites", "mod_sprites", "mod_sprites_0.png"));
        await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request(), cancellationToken: ct));
        Assert.False(Directory.Exists(f.Out));

        // A main file that is not JSON.
        await File.WriteAllTextAsync(f.Main, "{ not json", ct);
        await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request(), cancellationToken: ct));
    }

    [Fact]
    public async Task EmptyProjectsMissingDataFilesAndBadDestinationsAreRefused()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request() with { BaseDataFile = f.Dir.File("none.win") }, cancellationToken: ct));
        await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request() with { Destination = Path.Combine(f.Project, "inside") }, cancellationToken: ct));
        await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request() with { Destination = f.Project }, cancellationToken: ct));
        Directory.CreateDirectory(f.Out);
        await File.WriteAllTextAsync(Path.Combine(f.Out, "mine.txt"), "x", ct);
        var busy = await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request(), cancellationToken: ct));
        Assert.Contains("not empty", busy.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(f.Out, "mine.txt")));

        using var empty = new TempDir();
        await File.WriteAllTextAsync(empty.File("project.json"), "{\"Name\":\"E\",\"Flags\":[]}", ct);
        var none = await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request() with { ProjectPath = empty.Path, Destination = f.Dir.File("o2") }, cancellationToken: ct));
        Assert.Contains("no resources", none.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScriptsOnlyRunWhenAllowedAndTheCheckOfTheBaseFileWorks()
    {
        using var f = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        var text = await File.ReadAllTextAsync(f.Main, ct);
        await File.WriteAllTextAsync(f.Main, text.Replace("\"PostImportScripts\": []", "\"PostImportScripts\": [\"x.csx\"]", StringComparison.Ordinal), ct);
        var info = UtmtProjectImporter.Inspect(f.Project);
        Assert.Equal(["x.csx"], info.Scripts);
        var refused = await Assert.ThrowsAsync<UtmtImportException>(() => f.Importer.ImportAsync(f.Request(), cancellationToken: ct));
        Assert.Contains("scripts", refused.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));

        Assert.Equal(UtmtBaseCheck.Unrecorded, await UtmtProjectImporter.CheckBaseAsync(info, f.BaseFile, ct));
        var recorded = info with { BaseSize = new FileInfo(f.BaseFile).Length, BaseMd5 = (await DataFileIdentity.ComputeAsync(f.BaseFile, ct)).Md5 };
        Assert.Equal(UtmtBaseCheck.Match, await UtmtProjectImporter.CheckBaseAsync(recorded, f.BaseFile, ct));
        Assert.Equal(UtmtBaseCheck.Mismatch, await UtmtProjectImporter.CheckBaseAsync(recorded with { BaseMd5 = "00" }, f.BaseFile, ct));
    }
}
