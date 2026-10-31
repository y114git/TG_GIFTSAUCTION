using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;

namespace G3MStudio.Git;

/// <summary>The Git panel (design/shell.md 15): changes, commit and the latest commits of the project folder. The controls are named so tests can find them.</summary>
public sealed partial class GitPanel : UserControl
{
    private readonly TextBlock _info = new() { Name = "GitInfo", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _init = new Button { Name = "GitInit" }.Tr(ContentControl.ContentProperty, "git.init");
    private readonly ListBox _changes = new() { Name = "GitChanges", MaxHeight = 200 };
    private readonly TextBox _message = new TextBox { Name = "GitMessage", AcceptsReturn = true }.Tr(TextBox.PlaceholderTextProperty, "git.message");
    private readonly Button _commit = new Button { Name = "GitCommit" }.Tr(ContentControl.ContentProperty, "git.commit");
    private readonly Button _stage = new Button { Name = "GitStageAll" }.Tr(ContentControl.ContentProperty, "git.stageAll");
    private readonly Button _unstage = new Button { Name = "GitUnstageAll" }.Tr(ContentControl.ContentProperty, "git.unstageAll");
    private readonly Button _refresh = new Button { Name = "GitRefresh" }.Tr(ContentControl.ContentProperty, "git.refresh");
    private readonly ListBox _log = new() { Name = "GitLog", MaxHeight = 160 };
    private readonly IReadOnlyDictionary<string, string>? _environment;
    private GitService? _git;
    private bool _installed;

    /// <param name="environment">Extra environment for git, such as the identity of a test.</param>
    public GitPanel(IReadOnlyDictionary<string, string>? environment = null)
    {
        _environment = environment;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _stage, _unstage, _refresh, _commit } };
        Content = new ScrollViewer { Content = new StackPanel { Margin = new Avalonia.Thickness(8), Spacing = 6, Children = { _info, _init, BuildRemoteBars(), _changes, _diff, _message, buttons, _log } } };
        _init.Click += async (_, _) => await RunAsync(g => g.InitAsync());
        _stage.Click += async (_, _) => await RunAsync(g => g.StageAllAsync());
        _unstage.Click += async (_, _) => await RunAsync(g => g.UnstageAllAsync());
        _refresh.Click += async (_, _) => await RefreshAsync();
        _commit.Click += async (_, _) =>
        {
            var message = _message.Text?.Trim() ?? "";
            if (message.Length == 0) return;
            if (await RunAsync(g => g.CommitAsync(message))) _message.Text = "";
        };
        _changes.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GitChange>((c, _) => new TextBlock { Text = c is null ? "" : $"{c.Index}{c.Work} {c.Path}" });
        _log.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GitCommitInfo>((c, _) => new TextBlock { Text = c is null ? "" : $"{c.Hash[..7]} {c.Subject}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        Show("git.noProject");
    }

    public IReadOnlyList<GitChange> Changes => [.. _changes.Items.OfType<GitChange>()];

    public IReadOnlyList<GitCommitInfo> Commits => [.. _log.Items.OfType<GitCommitInfo>()];

    public string Info => _info.Text ?? "";

    /// <summary>Points the panel at the project folder, or at nothing in Direct mode.</summary>
    public async Task BindAsync(string? projectFolder)
    {
        _git = projectFolder is null ? null : new GitService(projectFolder, _environment);
        _installed = _git is not null && await GitService.IsInstalledAsync();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var git = _git;
        var empty = git is null || !_installed || !git.IsRepository;
        _init.IsVisible = git is not null && _installed && !git.IsRepository;
        foreach (var control in new Control[] { _changes, _message, _commit, _stage, _unstage, _refresh, _log, _branches, _newBranchName, _newBranch }) control.IsVisible = !empty;
        if (empty) foreach (var control in new Control[] { _fetch, _pull, _push, _sync, _remoteUrl, _addRemote, _diff }) control.IsVisible = false;
        _changes.ItemsSource = _log.ItemsSource = null;
        if (git is null) { Show("git.noProject"); return; }
        if (!_installed) { Show("git.notInstalled"); return; }
        if (!git.IsRepository) { Show("git.notRepository"); return; }

        try
        {
            var (changes, commits) = (await git.StatusAsync(), await git.LogAsync());
            if (git != _git) return; // another project was bound meanwhile
            (_changes.ItemsSource, _log.ItemsSource) = (changes, commits);
            await RefreshRemoteAsync(git);
            if (git != _git) return;
        }
        catch (IOException e)
        {
            Show(Loc.Current.Format("git.failed", ("reason", e.Message)), false);
            return;
        }

        var large = GitFiles.LargeAssets(git.Folder);
        _info.Text = large.Count > 0 ? Loc.Current.Format("git.lfs", ("count", large.Count), ("first", large[0])) : _changes.ItemCount == 0 ? Loc.Current["git.clean"] : "";
        _info.IsVisible = _info.Text.Length > 0;
    }

    private void Show(string text, bool isKey = true)
    {
        _info.Text = isKey ? Loc.Current[text] : text;
        _info.IsVisible = true;
    }

    private async Task<bool> RunAsync(Func<GitService, Task<GitResult>> action)
    {
        if (_git is null) return false;
        var result = await action(_git);
        await RefreshAsync();
        if (!result.Ok) Show(Loc.Current.Format("git.failed", ("reason", result.Error)), false);
        return result.Ok;
    }
}
