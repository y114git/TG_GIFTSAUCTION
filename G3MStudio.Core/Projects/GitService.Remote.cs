namespace G3MStudio.Core.Projects;

public sealed partial class GitService
{
    public async Task<IReadOnlyList<string>> RemotesAsync(CancellationToken cancellationToken = default) =>
        [.. (await RunAsync(["remote"], cancellationToken)).Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public Task<GitResult> AddRemoteAsync(string name, string url, CancellationToken cancellationToken = default) => RunAsync(["remote", "add", name, url], cancellationToken);

    public Task<GitResult> FetchAsync(CancellationToken cancellationToken = default) => RunAsync(["fetch", "--all", "--prune"], cancellationToken);

    // Fast-forward only: a pull that needs a merge is left to the user, who sees the message, instead of a merge made behind their back.
    public Task<GitResult> PullAsync(CancellationToken cancellationToken = default) => RunAsync(["pull", "--ff-only"], cancellationToken);

    /// <summary>Pushes the current branch; a branch without an upstream is published to the first remote and tracked.</summary>
    public async Task<GitResult> PushAsync(CancellationToken cancellationToken = default)
    {
        var upstream = await RunAsync(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"], cancellationToken);
        if (upstream.Ok) return await RunAsync(["push"], cancellationToken);
        if (await RemotesAsync(cancellationToken) is not [var remote, ..]) return new GitResult(1, "", "No remote is set. Add one first.");
        return await RunAsync(["push", "--set-upstream", remote, await CurrentBranchAsync(cancellationToken)], cancellationToken);
    }

    /// <summary>Commits ahead of and behind the upstream, or null when the branch has none.</summary>
    public async Task<(int Ahead, int Behind)?> AheadBehindAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["rev-list", "--left-right", "--count", "HEAD...@{upstream}"], cancellationToken);
        var parts = result.Output.Split('\t', StringSplitOptions.TrimEntries);
        return result.Ok && parts.Length == 2 ? (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)) : null;
    }
}
