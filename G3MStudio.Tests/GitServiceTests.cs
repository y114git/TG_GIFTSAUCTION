using G3MStudio.Core.Projects;

namespace G3MStudio.Tests;

public sealed class GitServiceTests
{
    internal static readonly Dictionary<string, string> Identity = new()
    {
        ["GIT_AUTHOR_NAME"] = "Tester",
        ["GIT_AUTHOR_EMAIL"] = "tester@example.com",
        ["GIT_COMMITTER_NAME"] = "Tester",
        ["GIT_COMMITTER_EMAIL"] = "tester@example.com",
        ["GIT_CONFIG_GLOBAL"] = Path.GetTempFileName(),
        ["GIT_CONFIG_NOSYSTEM"] = "1",
    };

    /// <summary>Runs git for a test with the isolated identity and configuration, so the default branch and settings do not depend on the machine.</summary>
    internal static async Task RunGitAsync(CancellationToken cancellationToken, params string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var (key, value) in Identity) info.Environment[key] = value;
        using var process = System.Diagnostics.Process.Start(info)!;
        await process.WaitForExitAsync(cancellationToken);
        Assert.Equal(0, process.ExitCode);
    }

    private static async Task<GitService> NewRepoAsync(TempDir dir)
    {
        Assert.SkipUnless(await GitService.IsInstalledAsync(TestContext.Current.CancellationToken), "git is not installed");
        var git = new GitService(dir.File("project"), Identity);
        Directory.CreateDirectory(git.Folder);
        Assert.True((await git.InitAsync(TestContext.Current.CancellationToken)).Ok);
        return git;
    }

    [Fact]
    public async Task InitWritesIgnoreAndAttributesButKeepsTheUsersOwn()
    {
        using var dir = new TempDir();
        Assert.SkipUnless(await GitService.IsInstalledAsync(TestContext.Current.CancellationToken), "git is not installed");
        var folder = Directory.CreateDirectory(dir.File("project")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, ".gitattributes"), "*.txt text\n", TestContext.Current.CancellationToken);
        var git = new GitService(folder, Identity);
        Assert.True((await git.InitAsync(TestContext.Current.CancellationToken)).Ok);
        Assert.True(git.IsRepository);
        Assert.Equal(GitFiles.Ignore, (await File.ReadAllTextAsync(Path.Combine(folder, ".gitignore"), TestContext.Current.CancellationToken)));
        Assert.Equal("*.txt text\n", (await File.ReadAllTextAsync(Path.Combine(folder, ".gitattributes"), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task StatusStageCommitAndLogWork()
    {
        using var dir = new TempDir();
        var git = await NewRepoAsync(dir);
        await File.WriteAllTextAsync(Path.Combine(git.Folder, "a.gml"), "x = 1;\n", TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(git.Folder, "objects", "obj"));
        await File.WriteAllTextAsync(Path.Combine(git.Folder, "objects", "obj", "o.json"), "{}\n", TestContext.Current.CancellationToken);

        var status = await git.StatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains(status, c => c.Path == "objects/obj/o.json" && c.Work == '?');
        Assert.DoesNotContain(status, c => c.Staged);

        await git.StageAllAsync(TestContext.Current.CancellationToken);
        Assert.All(await git.StatusAsync(TestContext.Current.CancellationToken), c => Assert.True(c.Staged));
        await git.UnstageAllAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(await git.StatusAsync(TestContext.Current.CancellationToken), c => c.Staged);

        Assert.Empty(await git.LogAsync(cancellationToken: TestContext.Current.CancellationToken));
        await git.StageAllAsync(TestContext.Current.CancellationToken);
        Assert.True((await git.CommitAsync("first commit", TestContext.Current.CancellationToken)).Ok);
        Assert.Empty(await git.StatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["first commit"], (await git.LogAsync(cancellationToken: TestContext.Current.CancellationToken)).Select(c => c.Subject));

        await File.WriteAllTextAsync(Path.Combine(git.Folder, "a.gml"), "x = 2;\n", TestContext.Current.CancellationToken);
        Assert.Contains("+x = 2;", await git.DiffAsync("a.gml", false, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACloneWithAutoCrlfKeepsEveryByte()
    {
        using var dir = new TempDir();
        var git = await NewRepoAsync(dir);
        byte[] json = /*lang=json,strict*/ "{\n  \"a\": 1\n}\n"u8.ToArray(), gml = "x = 1;\n"u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(git.Folder, "o.json"), json, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(git.Folder, "s.gml"), gml, TestContext.Current.CancellationToken);
        await git.StageAllAsync(TestContext.Current.CancellationToken);
        Assert.True((await git.CommitAsync("files", TestContext.Current.CancellationToken)).Ok);

        var clone = dir.File("clone");
        await RunGitAsync(TestContext.Current.CancellationToken, "-c", "core.autocrlf=true", "clone", "--quiet", git.Folder, clone);
        Assert.Equal(json, (await File.ReadAllBytesAsync(Path.Combine(clone, "o.json"), TestContext.Current.CancellationToken)));
        Assert.Equal(gml, (await File.ReadAllBytesAsync(Path.Combine(clone, "s.gml"), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void LargeAudioAndImagesAreSuggestedForLfs()
    {
        using var dir = new TempDir();
        File.WriteAllBytes(dir.File("big.ogg"), new byte[2048]);
        File.WriteAllBytes(dir.File("small.png"), new byte[10]);
        File.WriteAllBytes(dir.File("big.json"), new byte[2048]);
        Assert.Equal(["big.ogg"], GitFiles.LargeAssets(dir.Path, 1024));
    }

    [Fact]
    public async Task BranchesAndSingleFileStagingWork()
    {
        using var dir = new TempDir();
        var git = await NewRepoAsync(dir);
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(git.Folder, "a.gml"), "a\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(git.Folder, "b.gml"), "b\n", TestContext.Current.CancellationToken);

        await git.StageAsync("a.gml", ct);
        var status = await git.StatusAsync(ct);
        Assert.True(status.Single(c => c.Path == "a.gml").Staged);
        Assert.False(status.Single(c => c.Path == "b.gml").Staged);
        await git.UnstageAsync("a.gml", ct);
        Assert.DoesNotContain(await git.StatusAsync(ct), c => c.Staged);

        await git.StageAllAsync(ct);
        Assert.True((await git.CommitAsync("one", ct)).Ok);
        var first = await git.CurrentBranchAsync(ct);
        Assert.True((await git.NewBranchAsync("feature", ct)).Ok);
        Assert.Equal("feature", await git.CurrentBranchAsync(ct));
        Assert.Equal(new[] { first, "feature" }.Order(), (await git.BranchesAsync(ct)).Select(b => b.Name).Order());
        Assert.Equal("feature", (await git.BranchesAsync(ct)).Single(b => b.Current).Name);
        Assert.True((await git.SwitchAsync(first, ct)).Ok);
        Assert.False((await git.SwitchAsync("missing", ct)).Ok);
    }

    [Fact]
    public async Task PushPullAndFetchWorkAgainstALocalRemote()
    {
        using var dir = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var git = await NewRepoAsync(dir);
        var remote = Directory.CreateDirectory(dir.File("remote.git")).FullName;
        await RunGitAsync(ct, "init", "--bare", "--quiet", remote);

        await File.WriteAllTextAsync(Path.Combine(git.Folder, "a.gml"), "a\n", TestContext.Current.CancellationToken);
        await git.StageAllAsync(ct);
        await git.CommitAsync("one", ct);
        Assert.False((await git.PushAsync(ct)).Ok); // no remote yet
        Assert.True((await git.AddRemoteAsync("origin", remote, ct)).Ok);
        Assert.Equal(["origin"], await git.RemotesAsync(ct));
        Assert.True((await git.PushAsync(ct)).Ok);
        Assert.Equal((0, 0), await git.AheadBehindAsync(ct));

        // A second copy commits and pushes; the first one fetches, sees it is behind, and pulls.
        var other = new GitService(dir.File("other"), Identity);
        await RunGitAsync(ct, "clone", "--quiet", remote, other.Folder);
        await File.WriteAllTextAsync(Path.Combine(other.Folder, "b.gml"), "b\n", TestContext.Current.CancellationToken);
        await other.StageAllAsync(ct);
        await other.CommitAsync("two", ct);
        Assert.True((await other.PushAsync(ct)).Ok);

        Assert.True((await git.FetchAsync(ct)).Ok);
        Assert.Equal((0, 1), await git.AheadBehindAsync(ct));
        Assert.True((await git.PullAsync(ct)).Ok);
        Assert.True(File.Exists(Path.Combine(git.Folder, "b.gml")));
        Assert.Equal(["two", "one"], (await git.LogAsync(cancellationToken: ct)).Select(c => c.Subject));
    }
}
