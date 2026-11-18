using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The startup gate of the release build: the app starts from empty and from damaged user data, and every fixture opens, shows each kind of resource and closes again.</summary>
[Trait("Category", "Startup")]
public sealed class StartupTests
{
    private static readonly string[] Languages = ["en", "ru", "es", "ja", "ko", "zh_cn", "zh_tw"];

    private static MainWindow NewWindow(TempDir dir) => TabsWorkspaceTests.NewWindow(dir);

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertStartPage(MainWindow window)
    {
        Assert.True(window.FindControl<Control>("Start")!.IsVisible);
        Assert.Null(window.Workspaces.Current);
        Assert.Equal(Loc.Current["app.title"], window.Title);
    }

    [Fact]
    public async Task ANewWindowShowsTheStartPageWithItsTitle()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = NewWindow(dir);
            try
            {
                Settle();
                Assert.False(string.IsNullOrWhiteSpace(window.Title));
                AssertStartPage(window);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AllSevenLanguagesLoadWithTheSameKeys()
    {
        await HeadlessApp.Run(() =>
        {
            Assert.Equal(Languages.Order(StringComparer.Ordinal), Loc.Current.Languages.Keys.Order(StringComparer.Ordinal));
            try
            {
                foreach (var code in Languages)
                {
                    Loc.Current.SetLanguage(code);
                    Assert.False(string.IsNullOrWhiteSpace(Loc.Current["app.title"]), code);
                }
            }
            finally { Loc.Current.SetLanguage("en"); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryLanguageStartsAWindow()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            foreach (var code in Languages)
            {
                var window = NewWindow(dir);
                try
                {
                    Loc.Current.SetLanguage(code);
                    Settle();
                    AssertStartPage(window);
                }
                finally
                {
                    window.Close();
                    Loc.Current.SetLanguage("en");
                }
            }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("recent.json")]
    [InlineData("view")]
    [InlineData("")]
    public async Task DamagedUserDataStillStarts(string damaged)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            // A first run leaves a real settings file, recent list and view state (with a tab) behind.
            var first = NewWindow(dir);
            await first.OpenAsync(path);
            first.Docking.OpenTab("resource", "objects:obj_1", false);
            first.Close();

            var user = dir.File("user");
            foreach (var file in Directory.EnumerateFiles(user, "*.json", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(user, file);
                if (damaged.Length == 0 || relative.StartsWith(damaged, StringComparison.Ordinal)) await File.WriteAllTextAsync(file, "{ \"broken\": [", TestContext.Current.CancellationToken);
            }

            var window = NewWindow(dir);
            try
            {
                await window.StartSessionAsync();
                Settle();
                Assert.NotNull(window.Title);
                if (window.Workspaces.Current is null) AssertStartPage(window);
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheCommandLineArgumentsStartTheWindow()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                // --smoke alone opens nothing and leaves the window on the start page.
                window.OpenStartupFile(["--smoke"]);
                Settle();
                AssertStartPage(window);
                Assert.True(window.IsVisible);

                // A file argument, with and without --smoke, opens that file.
                foreach (string[] args in (string[][])[[path], ["--smoke", path]])
                {
                    window.OpenStartupFile(args);
                    for (var i = 0; i < 500 && window.Workspaces.Current is null; i++)
                    {
                        Settle();
                        await Task.Delay(10, TestContext.Current.CancellationToken);
                    }

                    Assert.Equal(Path.GetFullPath(path), window.Workspaces.Current?.Path);
                    window.Workspaces.Close();
                    Settle();
                    AssertStartPage(window);
                }
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task EveryFixtureOpensShowsEveryKindOfResourceAndClosesAgain()
    {
        using var dir = new TempDir();
        var files = Corpus.Files(Corpus.Fixtures).ToList();
        Assert.Equal(4, files.Count);
        await HeadlessApp.Run(async () =>
        {
            var window = NewWindow(dir);
            try
            {
                foreach (var file in files)
                {
                    var game = Path.GetFileName(Path.GetDirectoryName(file));
                    await window.OpenAsync(file);
                    Assert.Equal(Path.GetFullPath(file), window.Workspaces.Current?.Path);
                    var data = window.Workspaces.Current!.Data;
                    var opened = 0;
                    foreach (var type in ResourceTypes.All)
                    {
                        if (type.Items(data)?.FirstOrDefault() is not { } item) continue;
                        Assert.NotNull(window.Docking.OpenTab("resource", ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item)), false));
                        Settle();
                        opened++;
                    }

                    Assert.True(opened > 0, game);
                    Assert.Equal(opened, window.Docking.Documents().Count());
                    for (var i = 0; i < opened; i++)
                    {
                        window.Docking.CycleTab(1);
                        Settle();
                        Assert.NotNull(window.Docking.ActiveTabId);
                    }

                    window.Docking.CloseAll();
                    Settle();
                    Assert.Empty(window.Docking.Documents());
                    window.Workspaces.Close();
                    Settle();
                    AssertStartPage(window);
                    Assert.Empty(window.OwnedWindows);
                }
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }
}
