using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class UtmtImportUiTests
{
    private static readonly string[] Languages = ["en", "ru", "ja"];
    private static readonly string[] CreateIds = ["project.new", "project.enable"];
    private static readonly string[] OnlyWithProject = ["project.close", "project.sync", "project.options", "project.resolve", "project.moveDataFile", "project.disable"];

    internal static UtmtProjectInfo SampleInfo(string wide) => new()
    {
        MainFile = @"C:\mods\one\project.json",
        Name = wide,
        Resources = new Dictionary<string, int> { ["sprites"] = 12, ["code"] = 40 },
        HasGeneralInfo = true,
        Scripts = ["scripts/run.csx"],
        Options = ["Patches"],
        IgnoredFolders = ["notes"],
        InGitRepository = true,
    };

    internal static UtmtImportReport SampleReport(string wide) => new(SampleInfo(wide), @"C:\mods\one-g3m", @"C:\mods\one-g3m\mod.g3mproj", @"C:\mods\one-g3m\build\data.win", 52, UtmtGitState.Initialized,
        [new("options", "Patches, ExternalFiles"), new("ignoredFolders", wide), new("history", @"C:\mods\one"), new("library", "a message of the library")]);

    private static IEnumerable<MenuItem> Flatten(IEnumerable<MenuItem> items) => items.SelectMany(i => new[] { i }.Concat(Flatten(i.Items.OfType<MenuItem>())));

    private static MenuItem Item(MainWindow window, string id) => Flatten(window.Bar.Items).Single(i => AutomationProperties.GetAutomationId(i) == id);

    private static string? Tip(MainWindow window, string id) => ToolTip.GetTip(Item(window, id)) as string;

    private static bool On(MainWindow window, string id) => window.Registry.IsEnabled(window.Registry.Get(id));

    private static T Named<T>(Window window, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    [Fact]
    public async Task OpenProjectTakesAUtmtProjectFileThenConvertsAndTheReportOffersToOpenIt()
    {
        using var f = new UtmtImportTests.Fixture();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(f.Dir);
            try
            {
                Assert.True(On(window, "project.open"));
                Assert.DoesNotContain(Flatten(window.Bar.Items), i => AutomationProperties.GetAutomationId(i) == "file.openUtmt");

                UtmtProjectInfo? info = null;
                UtmtImportReport? report = null;
                window.FilePicker = (_, _) => Task.FromResult<string?>(f.Main);
                window.UtmtAsker = i =>
                {
                    info = i;
                    return Task.FromResult<UtmtImportRequest?>(f.Request(git: false));
                };
                window.UtmtReporter = r =>
                {
                    report = r;
                    return Task.FromResult(true); // open the project
                };
                await window.OpenProjectAsync();
                Assert.Equal("Test Mod", info?.Name);
                Assert.NotNull(report);
                Assert.Equal(report.DataFile, window.Workspaces.Current?.Path);
                Assert.NotNull(window.Workspaces.Current?.Project); // the converted project is found when its data file opens
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ABrokenProjectShowsAnErrorAndNothingIsWritten()
    {
        using var f = new UtmtImportTests.Fixture();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(f.Dir);
            try
            {
                await File.WriteAllTextAsync(f.Main, "{ broken", TestContext.Current.CancellationToken);
                var before = window.Notifications.Unread;
                await window.OpenUtmtProjectAsync(f.Project);
                Assert.True(window.Notifications.Unread > before);
                Assert.False(Directory.Exists(f.Out));
                Assert.Null(window.Workspaces.Current);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AFailureThatIsNotAnImportErrorStillEndsInAMessage()
    {
        using var f = new UtmtImportTests.Fixture();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(f.Dir);
            try
            {
                window.UtmtAsker = _ => throw new KeyNotFoundException("not an import error");
                var before = window.Notifications.Unread;
                await window.OpenUtmtProjectAsync(f.Project);
                Assert.True(window.Notifications.Unread > before);
                Assert.False(Directory.Exists(f.Out));
                Assert.Null(window.Workspaces.Current);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheReportDialogListsWarningsAndDetailsInTheLanguageOfTheProgram()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                foreach (var language in Languages)
                {
                    Loc.Current.SetLanguage(language);
                    var report = SampleReport("name");
                    var before = owner.OwnedWindows.Count;
                    var shown = ProjectDialogs.ShowUtmtReportAsync(owner, report);
                    var dialog = owner.OwnedWindows[before];
                    var warnings = Named<StackPanel>(dialog, "Warnings");
                    Assert.Equal(report.Warnings.Count, warnings.Children.Count);
                    Assert.Equal(ProjectDialogs.WarningText(report.Warnings[0]), ((TextBlock)warnings.Children[0]).Text);
                    Assert.Contains("Patches, ExternalFiles", ((TextBlock)warnings.Children[0]).Text, StringComparison.Ordinal);
                    Assert.Contains("52", Named<TextBlock>(dialog, "Summary").Text, StringComparison.Ordinal);
                    Assert.Equal(Loc.Current["project.utmt.report.gitInitialized"], Named<TextBlock>(dialog, "GitLine").Text);
                    Named<Expander>(dialog, "Details").IsExpanded = true;
                    Assert.Contains("sprites: 12", Named<TextBox>(dialog, "DetailsText").Text, StringComparison.Ordinal);
                    Named<Button>(dialog, "Ok").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Assert.True(await shown);
                }

                // No warnings says so.
                Loc.Current.SetLanguage("en");
                var empty = SampleReport("x") with { Warnings = [] };
                var count = owner.OwnedWindows.Count;
                var task = ProjectDialogs.ShowUtmtReportAsync(owner, empty);
                var clean = owner.OwnedWindows[count];
                Assert.Equal(Loc.Current["project.utmt.report.none"], Named<TextBlock>(clean, "NoWarnings").Text);
                clean.Close();
                Assert.False(await task);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task TheAskDialogNeedsTheDataFileAndOffersScriptsOnlyWhenTheProjectHasThem()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var before = owner.OwnedWindows.Count;
                var asked = ProjectDialogs.AskUtmtAsync(owner, SampleInfo("Mod"), @"C:\out", true);
                var dialog = owner.OwnedWindows[before];
                Assert.True(Named<CheckBox>(dialog, "Scripts").IsVisible);
                Assert.False(Named<CheckBox>(dialog, "Scripts").IsChecked);
                Named<Button>(dialog, "Ok").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Loc.Current["project.utmt.needFields"], Named<TextBlock>(dialog, "Error").Text);
                Named<TextBox>(dialog, "DataFile").Text = @"C:\game\data.win";
                Named<CheckBox>(dialog, "Scripts").IsChecked = true;
                Named<Button>(dialog, "Ok").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var request = await asked;
                Assert.Equal(new UtmtImportRequest(@"C:\mods\one\project.json", @"C:\game\data.win", @"C:\out", true, true), request);

                var plain = SampleInfo("Mod") with { Scripts = [] };
                before = owner.OwnedWindows.Count;
                var second = ProjectDialogs.AskUtmtAsync(owner, plain, @"C:\out", false);
                var other = owner.OwnedWindows[before];
                Assert.False(Named<CheckBox>(other, "Scripts").IsVisible);
                Assert.False(Named<CheckBox>(other, "Git").IsEnabled);
                other.Close();
                Assert.Null(await second);
            }
            finally { owner.Close(); }
        });
    }

    [Fact]
    public async Task ProjectMenuItemsFollowTheOpenFileAndEveryOffItemSaysWhy()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                string[] needFile = ["project.new", "project.enable", "project.close", "project.sync", "project.options", "project.resolve", "project.moveDataFile", "project.disable"];
                Assert.True(On(window, "project.open"));
                foreach (var id in needFile)
                {
                    Assert.False(On(window, id), id);
                    Assert.Equal(Loc.Current["project.why.noFile"], Tip(window, id));
                }

                await window.OpenAsync(path);
                Dispatcher.UIThread.RunJobs();
                foreach (var id in CreateIds) Assert.True(On(window, id), id);
                foreach (var id in OnlyWithProject)
                {
                    Assert.False(On(window, id), id);
                    Assert.Equal(Loc.Current["project.why.noProject"], Tip(window, id));
                }

                await window.Workspaces.Projects.EnableAsync(window.Workspaces.Current!, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
                window.CloseProject(); // closed again below, after the states of an enabled project
                Assert.Null(window.Workspaces.Current!.Project);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AnEnabledProjectTurnsNewOffAndCloseOnAndTheProjectComesBackWithOpenProject()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var link = await window.Workspaces.Projects.EnableAsync(window.Workspaces.Current!, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
                window.Workspaces.Current!.Project = link;
                window.Registry.SetContext(Core.Commands.AppCommands.ProjectOn, true);
                window.Registry.SetContext(Core.Commands.AppCommands.ProjectOff, false);
                foreach (var id in CreateIds)
                {
                    Assert.False(On(window, id), id);
                    Assert.Equal(Loc.Current["project.why.hasProject"], Tip(window, id));
                }

                Assert.True(On(window, "project.close"));
                window.CloseProject();
                Assert.Same(window.Workspaces.Current, window.Workspaces.Current);
                Assert.NotNull(window.Workspaces.Current);
                Assert.Null(window.Workspaces.Current.Project);
                Assert.True(On(window, "project.new"));
                Assert.Equal(link.ProjectFile, await window.Workspaces.Projects.FindClosedAsync(window.Workspaces.Current, TestContext.Current.CancellationToken));

                await window.OpenProjectAsync(link.ProjectFile);
                Assert.NotNull(window.Workspaces.Current!.Project);
                Assert.Null(await window.Workspaces.Projects.FindClosedAsync(window.Workspaces.Current, TestContext.Current.CancellationToken));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task OpenProjectOfAnUnknownProjectAsksForTheDataFileAndRefusesAnotherOne()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        var other = TestData.WriteSample(dir, "other.win", objects: 5);
        string? projectFile = null;
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var link = await window.Workspaces.Projects.EnableAsync(window.Workspaces.Current!, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
                projectFile = link.ProjectFile;
                window.Workspaces.Projects.Registry.Remove(link.Record.Id); // as on another computer: the project folder is not known here
                await window.CloseWorkspaceAsync();
                await Task.Delay(1, TestContext.Current.CancellationToken);

                var asked = new List<string>();
                var answer = other;
                window.FilePicker = (title, _) =>
                {
                    asked.Add(title);
                    return Task.FromResult<string?>(answer);
                };
                await window.OpenProjectAsync(projectFile);
                Assert.Equal([Loc.Current["project.open.pickData"]], asked);
                Assert.Null(window.Workspaces.Current); // a data file the project was not made from is refused

                answer = path;
                await window.OpenProjectAsync(Path.GetDirectoryName(projectFile)); // the folder works as well as the file
                Assert.Equal(path, window.Workspaces.Current?.Path);
                Assert.NotNull(window.Workspaces.Current?.Project);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task OpenProjectOfAKnownProjectOpensItsDataFileEvenAfterStudioChangedIt()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var link = await window.Workspaces.Projects.EnableAsync(window.Workspaces.Current!, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
                await window.CloseWorkspaceAsync();
                TestData.WriteSample(dir, "data.win", objects: 5); // saved in Studio since: no longer the base file of the project
                window.FilePicker = (_, _) => Task.FromResult<string?>(null);
                await window.OpenProjectAsync(link.ProjectFile);
                Assert.Equal(path, window.Workspaces.Current?.Path);
                Assert.NotNull(window.Workspaces.Current?.Project);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task OpenProjectRoutesAUtmtProjectToTheConversionAndRefusesOtherFiles()
    {
        using var f = new UtmtImportTests.Fixture();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(f.Dir);
            try
            {
                UtmtProjectInfo? info = null;
                window.UtmtAsker = i =>
                {
                    info = i;
                    return Task.FromResult<UtmtImportRequest?>(null);
                };
                await window.OpenProjectAsync(f.Main);
                Assert.Equal("Test Mod", info?.Name);

                info = null;
                var before = window.Notifications.Unread;
                await window.OpenProjectAsync(f.BaseFile);
                Assert.Null(info);
                Assert.True(window.Notifications.Unread > before);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task RecentProjectsListTheKnownProjectsAndSayWhenThereAreNone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var menu = Item(window, "project.open").Parent as MenuItem;
                var recent = Flatten(window.Bar.Items).Single(i => i.Name == "RecentProjects");
                Assert.Same(menu, recent.Parent);
                window.RefreshRecentProjects();
                Assert.Single(recent.Items);
                Assert.False(((MenuItem)recent.Items[0]!).IsEnabled);

                await window.OpenAsync(path);
                var link = await window.Workspaces.Projects.EnableAsync(window.Workspaces.Current!, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
                window.RefreshRecentProjects();
                var entry = Assert.IsType<MenuItem>(Assert.Single(recent.Items));
                Assert.Equal(link.Record.Name, entry.Header);
                Assert.Equal(link.Folder, ToolTip.GetTip(entry));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ThePaletteAndTheProjectMenuHaveTheSameProjectCommandsInOneOrder()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var menu = Item(window, "project.open").Parent as MenuItem;
                var ids = menu!.Items.OfType<MenuItem>().Select(i => AutomationProperties.GetAutomationId(i)).Where(i => !string.IsNullOrEmpty(i)).ToList();
                Assert.Equal(["project.open", "project.close", "project.new", "project.enable", "project.sync", "project.resolve", "project.options", "project.moveDataFile", "project.disable"], ids);
                Assert.All(ids, id => Assert.NotNull(window.Registry.Find(id!)));
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
