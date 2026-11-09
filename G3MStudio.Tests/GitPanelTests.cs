using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Projects;
using G3MStudio.Git;

namespace G3MStudio.Tests;

public sealed class GitPanelTests
{
    private static T Find<T>(Window window, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    private static async Task Wait(Func<bool> done, Func<string>? why = null)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), why?.Invoke());
    }

    private static async Task Click(Window window, string name, Func<bool> done, Func<string>? why = null)
    {
        Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(done, why);
    }

    private static void Set(Window window, string name, string text) => Find<TextBox>(window, name).Text = text;

    [Fact]
    public async Task ThePanelInitializesAndStagesAProjectFolder()
    {
        using var dir = new TempDir();
        Assert.SkipUnless(await GitService.IsInstalledAsync(TestContext.Current.CancellationToken), "git is not installed");
        var folder = Directory.CreateDirectory(dir.File("project")).FullName;
        await HeadlessApp.Run(async () =>
        {
            var panel = new GitPanel(GitServiceTests.Identity);
            var window = new Window { Content = panel };
            window.Show();
            try
            {
                await panel.BindAsync(null);
                Assert.StartsWith("Git tracks the project folder", panel.Info, StringComparison.Ordinal);

                await panel.BindAsync(folder);
                Assert.Equal("This project is not a Git repository.", panel.Info);
                await Click(window, "GitInit", () => new GitService(folder).IsRepository && panel.Info != "This project is not a Git repository.");

                await File.WriteAllTextAsync(Path.Combine(folder, "a.gml"), "x = 1;\n", TestContext.Current.CancellationToken);
                await panel.RefreshAsync();
                Assert.Contains(panel.Changes, c => c.Path == "a.gml" && !c.Staged);

                await Click(window, "GitStageAll", () => panel.Changes.Count > 0 && panel.Changes.All(c => c.Staged));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ThePanelCommitsSwitchesBranchesAndPushesToARemote()
    {
        using var dir = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await GitService.IsInstalledAsync(ct), "git is not installed");
        var folder = Directory.CreateDirectory(dir.File("project")).FullName;
        var remote = dir.File("remote.git");
        await GitServiceTests.RunGitAsync(ct, "init", "--bare", "--quiet", remote);
        await new GitService(folder, GitServiceTests.Identity).InitAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.gml"), "x = 1;\n", TestContext.Current.CancellationToken);

        await HeadlessApp.Run(async () =>
        {
            var panel = new GitPanel(GitServiceTests.Identity);
            var window = new Window { Content = panel, Width = 900, Height = 700 };
            window.Show();
            try
            {
                await panel.BindAsync(folder);
                Assert.Single(panel.Changes, c => c.Path == "a.gml");

                // A change is selected to see its diff (a new file has none yet); the commit takes what is staged.
                var list = Find<ListBox>(window, "GitChanges");
                list.SelectedIndex = 0;
                await Wait(() => Find<TextBox>(window, "GitDiff").IsVisible);
                await Click(window, "GitStageAll", () => panel.Changes.All(c => c.Staged));

                Set(window, "GitMessage", "first");
                await Click(window, "GitCommit", () => panel.Commits.Count == 1, () => "panel info: " + panel.Info);

                Set(window, "GitNewBranchName", "feature");
                await Click(window, "GitNewBranch", () => Find<ComboBox>(window, "GitBranch").SelectedItem is "feature");

                Set(window, "GitRemoteUrl", remote);
                await Click(window, "GitAddRemote", () => Find<Button>(window, "GitPush").IsVisible);
                await Click(window, "GitPush", () => File.Exists(Path.Combine(remote, "refs", "heads", "feature")) || Directory.EnumerateFiles(remote, "packed-refs").Any());
            }
            finally { window.Close(); }
        });
    }
}
