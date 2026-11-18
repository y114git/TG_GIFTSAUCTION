namespace G3MStudio.Core.Projects;

public sealed record GitBranch(string Name, bool Current);

public sealed partial class GitService
{
    public async Task<IReadOnlyList<GitBranch>> BranchesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["branch", "--format=%(HEAD)%(refname:short)"], cancellationToken);
        if (!result.Ok) return [];
        return [.. result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => new GitBranch(l.TrimStart('*', ' '), l.StartsWith('*')))];
    }

    /// <summary>The checked out branch; before the first commit it is the branch the first commit will create.</summary>
    public async Task<string> CurrentBranchAsync(CancellationToken cancellationToken = default) =>
        (await RunAsync(["symbolic-ref", "--short", "HEAD"], cancellationToken)).Output.Trim();

    public Task<GitResult> SwitchAsync(string branch, CancellationToken cancellationToken = default) => RunAsync(["switch", branch], cancellationToken);

    public Task<GitResult> NewBranchAsync(string branch, CancellationToken cancellationToken = default) => RunAsync(["switch", "--create", branch], cancellationToken);

    public Task<GitResult> StageAsync(string path, CancellationToken cancellationToken = default) => RunAsync(["add", "--all", "--", path], cancellationToken);

    public async Task<GitResult> UnstageAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["restore", "--staged", "--", path], cancellationToken);
        return result.Ok ? result : await RunAsync(["rm", "--cached", "--quiet", "--ignore-unmatch", "--", path], cancellationToken); // no HEAD yet
    }
}
