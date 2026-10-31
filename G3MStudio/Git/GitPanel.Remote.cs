using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;

namespace G3MStudio.Git;

/// <summary>Branches, remotes and the diff of the selected change.</summary>
public sealed partial class GitPanel
{
    private readonly ComboBox _branches = new() { Name = "GitBranch", MinWidth = 160 };
    private readonly TextBox _newBranchName = new TextBox { Name = "GitNewBranchName", MinWidth = 140 }.Tr(TextBox.PlaceholderTextProperty, "git.newBranchName");
    private readonly Button _newBranch = new Button { Name = "GitNewBranch" }.Tr(ContentControl.ContentProperty, "git.newBranch");
    private readonly Button _fetch = new Button { Name = "GitFetch" }.Tr(ContentControl.ContentProperty, "git.fetch");
    private readonly Button _pull = new Button { Name = "GitPull" }.Tr(ContentControl.ContentProperty, "git.pull");
    private readonly Button _push = new Button { Name = "GitPush" }.Tr(ContentControl.ContentProperty, "git.push");
    private readonly TextBox _remoteUrl = new TextBox { Name = "GitRemoteUrl", MinWidth = 260 }.Tr(TextBox.PlaceholderTextProperty, "git.remoteUrl");
    private readonly Button _addRemote = new Button { Name = "GitAddRemote" }.Tr(ContentControl.ContentProperty, "git.addRemote");
    private readonly TextBlock _sync = new() { Name = "GitSync", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _diff = new() { Name = "GitDiff", IsReadOnly = true, IsVisible = false, MaxHeight = 240, FontFamily = "Consolas, Menlo, Liberation Mono, monospace" };
    private bool _loadingBranches;

    private StackPanel BuildRemoteBars()
    {
        _branches.SelectionChanged += async (_, _) =>
        {
            if (!_loadingBranches && _branches.SelectedItem is string name) await RunAsync(g => g.SwitchAsync(name));
        };
        _newBranch.Click += async (_, _) =>
        {
            var name = _newBranchName.Text?.Trim() ?? "";
            if (name.Length > 0 && await RunAsync(g => g.NewBranchAsync(name))) _newBranchName.Text = "";
        };
        _fetch.Click += async (_, _) => await RunAsync(g => g.FetchAsync());
        _pull.Click += async (_, _) => await RunAsync(g => g.PullAsync());
        _push.Click += async (_, _) => await RunAsync(g => g.PushAsync());
        _addRemote.Click += async (_, _) =>
        {
            var url = _remoteUrl.Text?.Trim() ?? "";
            if (url.Length > 0 && await RunAsync(g => g.AddRemoteAsync("origin", url))) _remoteUrl.Text = "";
        };
        _changes.DoubleTapped += async (_, _) =>
        {
            if (_changes.SelectedItem is GitChange change) await RunAsync(g => change.Staged ? g.UnstageAsync(change.Path) : g.StageAsync(change.Path));
        };
        _changes.SelectionChanged += async (_, _) => await ShowDiffAsync();
        var branchBar = new StackPanel { Name = "GitBranchBar", Orientation = Orientation.Horizontal, Spacing = 6, Children = { _branches, _newBranchName, _newBranch, _fetch, _pull, _push, _sync } };
        var remoteBar = new StackPanel { Name = "GitRemoteBar", Orientation = Orientation.Horizontal, Spacing = 6, Children = { _remoteUrl, _addRemote } };
        return new StackPanel { Spacing = 6, Children = { branchBar, remoteBar } };
    }

    private async Task RefreshRemoteAsync(GitService git)
    {
        var branches = await git.BranchesAsync();
        var current = branches.FirstOrDefault(b => b.Current)?.Name ?? await git.CurrentBranchAsync();
        var hasRemote = (await git.RemotesAsync()).Count > 0;
        var ahead = hasRemote ? await git.AheadBehindAsync() : null;
        if (git != _git) return; // another project was bound meanwhile
        _loadingBranches = true;
        _branches.ItemsSource = branches.Count > 0 ? [.. branches.Select(b => b.Name)] : new List<string> { current };
        _branches.SelectedItem = current;
        _loadingBranches = false;
        foreach (var control in new Control[] { _fetch, _pull, _push, _sync }) control.IsVisible = hasRemote;
        _remoteUrl.IsVisible = _addRemote.IsVisible = !hasRemote;
        _sync.Text = ahead is { } counts ? Loc.Current.Format("git.aheadBehind", ("ahead", counts.Ahead), ("behind", counts.Behind)) : "";
    }

    private async Task ShowDiffAsync()
    {
        if (_git is not { } git || _changes.SelectedItem is not GitChange change) { _diff.IsVisible = false; return; }
        var text = change.Work == '?' ? Loc.Current["git.newFile"] : await git.DiffAsync(change.Path, change.Staged);
        if (git != _git) return;
        _diff.Text = text;
        _diff.IsVisible = true;
    }
}
