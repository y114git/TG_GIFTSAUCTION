using G3MStudio.Core.Commands;
using G3MStudio.Core.Plugins;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PluginTests
{
    private static (PluginHost Host, CommandRegistry Registry, List<string> Messages) NewHost(TempDir dir)
    {
        var registry = new CommandRegistry();
        var messages = new List<string>();
        return (new PluginHost(new UserData(dir.Path), registry, () => null, (_, text) => messages.Add(text)), registry, messages);
    }

    [Fact]
    public void ACompiledPluginAddsACommandThatGoesAwayWhenItIsTurnedOff()
    {
        using var dir = new TempDir();
        var (host, registry, messages) = NewHost(dir);
        var folder = host.CreateTemplate("Hello Plugin");
        host.Load();

        var plugin = Assert.Single(host.Plugins);
        Assert.True(plugin.State == PluginState.Active, plugin.Error);
        var id = Assert.Single(plugin.CommandIds);
        Assert.Equal("Hello Plugin: Say Hello", registry.Get(id).Title);
        Assert.True(registry.Execute(id));
        Assert.Equal(["Hello from Hello Plugin."], messages);

        host.Disable(plugin);
        Assert.Null(registry.Find(id));
        Assert.Equal(PluginState.Disabled, plugin.State);

        // The choice survives a restart.
        var (again, _, _) = NewHost(dir);
        again.Load();
        Assert.Equal(PluginState.Disabled, Assert.Single(again.Plugins).State);
        again.Enable(again.Plugins[0]);
        Assert.Equal(PluginState.Active, again.Plugins[0].State);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void APluginThatDoesNotCompileIsTurnedOffWithItsError()
    {
        using var dir = new TempDir();
        var (host, registry, _) = NewHost(dir);
        var folder = host.CreateTemplate("Broken");
        File.WriteAllText(Path.Combine(folder, "src", "Main.cs"), "public sealed class Main : { }");
        host.Load();

        var plugin = Assert.Single(host.Plugins);
        Assert.Equal(PluginState.Failed, plugin.State);
        Assert.False(string.IsNullOrEmpty(plugin.Error));
        Assert.Empty(registry.All);
    }

    [Fact]
    public void ACommandThatThrowsReportsTheErrorAndLeavesTheHostRunning()
    {
        using var dir = new TempDir();
        var (host, registry, messages) = NewHost(dir);
        var folder = host.CreateTemplate("Thrower");
        File.WriteAllText(Path.Combine(folder, "src", "Main.cs"), """
            using G3MStudio.Core.Plugins;
            public sealed class Main : IStudioPlugin
            {
                public void Activate(IStudioContext context) => context.AddCommand("boom", "Boom", () => throw new System.InvalidOperationException("bad"));
                public void Deactivate() { }
            }
            """);
        host.Load();

        Assert.True(registry.Execute(Assert.Single(host.Plugins[0].CommandIds)));
        Assert.Contains(messages, m => m.Contains("bad", StringComparison.Ordinal));
    }

    [Fact]
    public void ReloadPicksUpAnEditedSource()
    {
        using var dir = new TempDir();
        var (host, registry, _) = NewHost(dir);
        var folder = host.CreateTemplate("Editable");
        host.Load();
        var plugin = host.Plugins[0];
        var path = Path.Combine(folder, "src", "Main.cs");
        File.WriteAllText(path, File.ReadAllText(path).Replace("Say Hello", "Say Goodbye", StringComparison.Ordinal));
        host.Reload(plugin);

        Assert.Equal(PluginState.Active, plugin.State);
        Assert.Equal("Editable: Say Goodbye", registry.Get(Assert.Single(plugin.CommandIds)).Title);
    }

    [Fact]
    public async Task ReloadAsyncPicksUpAnEditedSourceAndTurnsOffOneThatDoesNotCompile()
    {
        using var dir = new TempDir();
        var (host, registry, _) = NewHost(dir);
        var folder = host.CreateTemplate("Editable");
        host.Load();
        var plugin = host.Plugins[0];
        var path = Path.Combine(folder, "src", "Main.cs");
        var source = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, source.Replace("Say Hello", "Say Goodbye", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        await host.ReloadAsync(plugin);

        Assert.Equal(PluginState.Active, plugin.State);
        Assert.Equal("Editable: Say Goodbye", registry.Get(Assert.Single(plugin.CommandIds)).Title);

        await File.WriteAllTextAsync(path, "public sealed class Main : { }", TestContext.Current.CancellationToken);
        await host.ReloadAsync(plugin);

        Assert.Equal(PluginState.Failed, plugin.State);
        Assert.False(string.IsNullOrEmpty(plugin.Error));
        Assert.Empty(registry.All);
    }

    [Fact]
    public void InstallingAZipOrFolderLoadsThePluginAndAZipCannotWriteOutsideItsFolder()
    {
        using var dir = new TempDir();
        var (maker, _, _) = NewHost(dir);
        var folder = maker.CreateTemplate("Packed");
        var zipPath = dir.File("packed.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(folder, zipPath);
        Directory.Delete(folder, true);

        var manifest = PluginHost.Inspect(zipPath);
        Assert.Equal("Packed", manifest.Name);
        var (host, registry, _) = NewHost(dir);
        var plugin = host.Install(zipPath);
        Assert.Equal(PluginState.Active, plugin.State);
        Assert.NotNull(registry.Find(Assert.Single(plugin.CommandIds)));

        var evil = dir.File("evil.zip");
        using (var zip = System.IO.Compression.ZipFile.Open(evil, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("plugin.json").Open())) writer.Write("{\"id\":\"evil\",\"name\":\"Evil\",\"version\":\"1\"}");
            zip.CreateEntry("../outside.txt").Open().Dispose();
        }

        Assert.Throws<InvalidDataException>(() => host.Install(evil));
        Assert.False(File.Exists(Path.Combine(host.Folder, "outside.txt")));
    }

    [Fact]
    public void AFailedUpdateKeepsTheOldPluginAndASourceInsideThePluginsFolderIsRefused()
    {
        using var dir = new TempDir();
        var (host, registry, _) = NewHost(dir);
        var folder = host.CreateTemplate("Keeper");
        host.Load();
        var broken = dir.File("broken.zip");
        using (var zip = System.IO.Compression.ZipFile.Open(broken, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("plugin.json").Open())) writer.Write("{\"id\":\"keeper\",\"name\":\"Keeper\",\"version\":\"2\"}");
            zip.CreateEntry("../outside.txt").Open().Dispose();
        }

        Assert.Throws<InvalidDataException>(() => host.Install(broken));
        var plugin = Assert.Single(host.Plugins);
        Assert.Equal(PluginState.Active, plugin.State);
        Assert.NotNull(registry.Find(Assert.Single(plugin.CommandIds)));
        Assert.True(File.Exists(Path.Combine(folder, "src", "Main.cs")));
        Assert.Equal([folder], Directory.GetDirectories(host.Folder)); // no half-built folder is left

        Assert.Throws<InvalidDataException>(() => host.Install(folder));
        Assert.True(File.Exists(Path.Combine(folder, "src", "Main.cs")));
    }

    [Fact]
    public void ThePluginCacheKeyFollowsTheBuildOfTheHost()
    {
        using var dir = new TempDir();
        var source = dir.File("Main.cs");
        File.WriteAllText(source, "class A {}");
        Assert.Equal(PluginHost.CacheKey([source], Guid.Empty), PluginHost.CacheKey([source], Guid.Empty));
        Assert.NotEqual(PluginHost.CacheKey([source], Guid.Empty), PluginHost.CacheKey([source], Guid.NewGuid()));
    }

    [Fact]
    public void AWatcherThatCannotBeStartedMeansNoHotReloadNotAFailedStart()
    {
        using var dir = new TempDir();
        var blocker = dir.File("file");
        File.WriteAllText(blocker, "x");
        Assert.Null(PluginWatcher.TryStart(Path.Combine(blocker, "plugins"), _ => { })); // a folder under a file cannot be made
    }

    [Fact]
    public async Task TheWatcherReportsAPluginFolderAfterItsFilesChange()
    {
        using var dir = new TempDir();
        var plugins = Path.Combine(dir.Path, "plugins");
        Directory.CreateDirectory(Path.Combine(plugins, "alpha", "src"));
        var seen = new TaskCompletionSource<string>();
        using var watcher = new PluginWatcher(plugins, name => seen.TrySetResult(name), TimeSpan.FromMilliseconds(100));
        await File.WriteAllTextAsync(Path.Combine(plugins, "alpha", "src", "Main.cs"), "x", TestContext.Current.CancellationToken);
        Assert.Equal("alpha", await seen.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GitHubAddressesAreParsedStrictlyAndAZipWithOneTopFolderInstalls()
    {
        Assert.Equal(("owner", "repo"), PluginDownload.ParseGitHub("https://github.com/owner/repo"));
        Assert.Equal(("owner", "repo"), PluginDownload.ParseGitHub("https://github.com/owner/repo.git"));
        Assert.Equal(("owner", "repo"), PluginDownload.ParseGitHub("https://github.com/owner/repo/tree/main"));
        Assert.Null(PluginDownload.ParseGitHub("http://github.com/owner/repo"));
        Assert.Null(PluginDownload.ParseGitHub("https://evil.example/owner/repo"));
        Assert.Null(PluginDownload.ParseGitHub("https://github.com/owner"));

        using var dir = new TempDir();
        var (maker, _, _) = NewHost(dir);
        var folder = maker.CreateTemplate("Wrapped");
        var wrapped = dir.File("wrapped.zip");
        using (var zip = System.IO.Compression.ZipFile.Open(wrapped, System.IO.Compression.ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip, file, "owner-repo-abc123/" + Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'));
        }

        Directory.Delete(folder, true);
        var (host, _, _) = NewHost(dir);
        Assert.Equal("Wrapped", PluginHost.Inspect(wrapped).Name);
        var installed = host.Install(wrapped, "https://github.com/owner/repo");
        Assert.Equal(PluginState.Active, installed.State);
        Assert.Equal("https://github.com/owner/repo", installed.Origin);
        Assert.Equal("abc123", installed.Revision);
        Assert.False(PluginDownload.IsOutdated(installed.Revision, "abc123def4567890"));
        Assert.True(PluginDownload.IsOutdated(installed.Revision, "ffffffffffff"));
        Assert.False(PluginDownload.IsOutdated(null, "ffffffffffff"));
        Assert.Null(host.Install(wrapped).Origin);
    }

    [Fact]
    public void APluginPanelIsACommandThatReportsItsTitleAndContentFactory()
    {
        using var dir = new TempDir();
        var (host, registry, _) = NewHost(dir);
        var folder = host.CreateTemplate("Paneled");
        File.WriteAllText(Path.Combine(folder, "src", "Main.cs"), """
            using G3MStudio.Core.Plugins;
            public sealed class Main : IStudioPlugin
            {
                public void Activate(IStudioContext context) => context.AddPanel("view", "My Panel", () => "content");
                public void Deactivate() { }
            }
            """);
        string? title = null;
        object? content = null;
        host.PanelRequested += (t, create) => (title, content) = (t, create());
        host.Load();

        Assert.True(registry.Execute(Assert.Single(host.Plugins[0].CommandIds)));
        Assert.Equal(("My Panel", "content"), (title, content));
    }

    [Fact]
    public void APluginEditorBuildsAViewForItsResourceTypeAndGoesAwayWithThePlugin()
    {
        using var dir = new TempDir();
        var (host, _, messages) = NewHost(dir);
        var folder = host.CreateTemplate("Viewer");
        File.WriteAllText(Path.Combine(folder, "src", "Main.cs"), """
            using G3MStudio.Core.Plugins;
            public sealed class Main : IStudioPlugin
            {
                public void Activate(IStudioContext context)
                {
                    context.AddEditor("sprites", resource => "view of " + resource);
                    context.AddEditor("objects", _ => throw new System.InvalidOperationException("broken editor"));
                }

                public void Deactivate() { }
            }
            """);
        host.Load();

        Assert.Equal("view of thing", host.CreateEditorView("sprites", "thing"));
        Assert.Null(host.CreateEditorView("rooms", "thing"));
        Assert.Null(host.CreateEditorView("objects", "thing"));
        Assert.Contains(messages, m => m.Contains("broken editor", StringComparison.Ordinal));
        host.Disable(host.Plugins[0]);
        Assert.Null(host.CreateEditorView("sprites", "thing"));
    }

    private const string ApiPlugin = """
        using G3MStudio.Core.Plugins;

        public sealed class Main : IStudioPlugin
        {
            public void Activate(IStudioContext c)
            {
                c.AddSetting(new PluginSetting("word", "Word", PluginSettingKind.Text, "hi"));
                c.AddCodeHints(r => new[] { new CodeHint(1, c.Settings.Get("word")) });
                c.AddCodeAction("up", "Upper", r => r.Text.ToUpperInvariant());
                c.On("workspace.opened", e => c.Log("opened"));
                c.AddCommand("set", "Set", () => c.Log("result: " + (c.SetCodeAsync("gml_Script_probe", "return 2;").Result ?? "ok") + "|" + (c.SetCodeAsync("gml_Script_probe", "return (;").Result is null ? "accepted" : "refused") + "|" + c.GetCode("gml_Script_probe")?.Trim()));
            }

            public void Deactivate() { }
        }
        """;

    [Fact]
    public async Task APluginCanDeclareOptionsAddCodeHintsAndActionsListenToEventsAndEditCode()
    {
        using var dir = new TempDir();
        string file;
        using (var data = TestData.Create(1, true)) file = TestData.Write(dir.File("data.win"), data);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var registry = new CommandRegistry();
        var host = new PluginHost(userData, registry, () => workspace, (_, _) => { });
        var folder = host.CreateTemplate("Api");
        await File.WriteAllTextAsync(Path.Combine(folder, "src", "Main.cs"), ApiPlugin, TestContext.Current.CancellationToken);
        host.Load();
        var plugin = Assert.Single(host.Plugins);
        Assert.True(plugin.State == PluginState.Active, plugin.Error);

        // Options: a default, a change that is saved and survives a restart, and a refresh signal for open editors.
        Assert.Equal("hi", Assert.Single(host.CollectHints("e", "x")).Text);
        var refreshed = 0;
        host.HintsChanged += () => refreshed++;
        plugin.Settings!.Set("word", "yo");
        Assert.Equal(1, refreshed);
        Assert.Equal("yo", Assert.Single(host.CollectHints("e", "x")).Text);
        var again = new PluginHost(userData, new CommandRegistry(), () => workspace, (_, _) => { });
        again.Load();
        Assert.Equal("yo", again.Plugins[0].Settings!.Get("word"));

        // A code action returns the whole new text.
        Assert.Equal("ABC", Assert.Single(host.CodeActions).Run(new CodeRequest("e", "abc")));

        // Events reach the handler, a command can read and replace code (a text that does not compile is refused), and the global switch silences everything.
        host.Notify(new PluginEvent("workspace.opened"));
        Assert.Contains("opened", plugin.Log);
        Assert.True(registry.Execute(Assert.Single(plugin.CommandIds)));
        Assert.Contains("result: ok|refused|return 2;", plugin.Log);
        host.Enabled = false;
        Assert.Empty(host.CollectHints("e", "x"));
        Assert.Empty(host.CodeActions);
    }
}
