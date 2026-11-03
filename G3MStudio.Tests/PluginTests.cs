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
}
