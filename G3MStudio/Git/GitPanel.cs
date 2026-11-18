using Avalonia.Controls;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Git;

/// <summary>The Git panel (design/shell.md 15): changes, commit and the latest commits of the project folder. The controls are named so tests can find them.</summary>
public sealed partial class GitPanel : UserControl
{
    private readonly TextBlock _info = new() { Name = "GitInfo", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _init = new Button { Name = "GitInit" }.Tr(ContentControl.ContentProperty, "git.init");
    private readonly ListBox _changes = new() { Name = "GitChanges", MaxHeight = 200 };
    private readonly TextBox _message = new TextBox { Name = "GitMessage", AcceptsReturn = true, MaxHeight = 120 }.Tr(TextBox.PlaceholderTextProperty, "git.message").Labeled("git.message");
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
        var buttons = new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Children = { _stage, _unstage, _refresh, _commit } };
        // The message box and the buttons stay at the bottom of the panel while the changes and the log scroll above them.
        var actions = new StackPanel { Margin = new Avalonia.Thickness(8, 0, 8, 8), Spacing = 6, Children = { _message, buttons } };
        DockPanel.SetDock(actions, Avalonia.Controls.Dock.Bottom);
        Content = new DockPanel { Children = { actions, new ScrollViewer { Content = new StackPanel { Margin = new Avalonia.Thickness(8), Spacing = 6, Children = { _info, _init, BuildRemoteBars(), _changes, _diff, _log } } } } };
        _init.Click += async (_, _) => await RunAsync(g => g.InitAsync());
        _stage.Click += async (_, _) => await RunAsync(g => g.StageAllAsync());
        _unstage.Click += async (_, _) => await RunAsync(g => g.UnstageAllAsync());
        _refresh.Click += async (_, _) => await RefreshAsync();
        _commit.IsEnabled = false;
        _message.TextChanged += (_, _) => _commit.IsEnabled = !string.IsNullOrWhiteSpace(_message.Text);
        _commit.Click += async (_, _) =>
        {
            var message = _message.Text?.Trim() ?? "";
            if (message.Length == 0) return;
            if (await RunAsync(g => g.CommitAsync(message))) _message.Text = "";
        };
        _changes.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GitChange>((c, _) => new TextBlock { Text = c is null ? "" : $"{c.Index}{c.Work} {c.Path}" });
        _log.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GitCommitInfo>((c, _) => new TextBlock { Text = c is null ? "" : $"{c.Hash[..7]} {c.Subject}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        Show("git.noProject");
        // The messages are read when shown, so a language change (or a tab that was hidden during it) shows them again.
        AttachedToVisualTree += (_, _) =>
        {
            Loc.Current.PropertyChanged += OnLanguageChanged;
            if (_shownLanguage != Loc.Current.Language) Relabel();
        };
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= OnLanguageChanged;
    }

    private string? _shownLanguage = Loc.Current.Language;

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Loc.Current.Language)) Relabel();
    }

    // The messages are kept as the text they make, so a language change writes them again without asking git.
    private Func<string> _infoText = () => "", _syncText = () => "";

    private void Relabel()
    {
        _shownLanguage = Loc.Current.Language;
        _info.Text = _infoText();
        _sync.Text = _syncText();
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
        _shownLanguage = Loc.Current.Language;
        var git = _git;
        var empty = git is null || !_installed || !git.IsRepository;
        _init.IsVisible = git is not null && _installed && !git.IsRepository;
        foreach (var control in new Control[] { _changes, _message, _commit, _stage, _unstage, _refresh, _log, _branches, _switch, _newBranchName, _newBranch }) control.IsVisible = !empty;
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
            Show(() => Loc.Current.Format("git.failed", ("reason", e.Message)));
            return;
        }

        var large = GitFiles.LargeAssets(git.Folder);
        Show(large.Count > 0 ? () => Loc.Current.Format("git.lfs", ("count", large.Count), ("first", large[0])) : _changes.ItemCount == 0 ? () => Loc.Current["git.clean"] : () => "");
    }

    private void Show(string key) => Show(() => Loc.Current[key]);

    private void Show(Func<string> text)
    {
        _infoText = text;
        _info.Text = text();
        _info.IsVisible = _info.Text.Length > 0;
    }

    private async Task<bool> RunAsync(Func<GitService, Task<GitResult>> action)
    {
        if (_git is null) return false;
        var result = await action(_git);
        await RefreshAsync();
        if (!result.Ok) Show(() => Loc.Current.Format("git.failed", ("reason", result.Error)));
        return result.Ok;
    }
}
