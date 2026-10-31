using System.Text.RegularExpressions;

namespace G3MStudio.Core.Plugins;

/// <summary>Turns a GitHub repository address into a ZIP download and fetches it. Only https addresses on github.com are accepted.</summary>
public static partial class PluginDownload
{
    public const long MaxBytes = 50 * 1024 * 1024;

    /// <summary>The owner and repository of <c>https://github.com/owner/repo</c> (extra path parts and a .git suffix are ignored), or null.</summary>
    public static (string Owner, string Repo)? ParseGitHub(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return Name().IsMatch(parts[0]) && Name().IsMatch(repo) ? (parts[0], repo) : null;
    }

    /// <summary>Downloads the default branch of the repository as a ZIP into <paramref name="file"/>. Throws <see cref="InvalidDataException"/> for a bad address or a download over the size limit.</summary>
    public static async Task DownloadAsync(HttpClient http, string address, string file, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseGitHub(address) ?? throw new InvalidDataException("Use an address like https://github.com/owner/repository.");
        using var response = await http.GetAsync(new Uri($"https://api.github.com/repos/{owner}/{repo}/zipball"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("The plugin is larger than 50 MB.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(file);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes) throw new InvalidDataException("The plugin is larger than 50 MB.");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <summary>The commit id the default branch of the repository points to now.</summary>
    public static async Task<string> LatestRevisionAsync(HttpClient http, string address, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseGitHub(address) ?? throw new InvalidDataException("Use an address like https://github.com/owner/repository.");
        using var response = await http.GetAsync(new Uri($"https://api.github.com/repos/{owner}/{repo}/commits/HEAD"), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub returned no commit id.");
    }

    /// <summary>True when the installed revision (the start of the commit id) is not the latest one.</summary>
    public static bool IsOutdated(string? installed, string latest) => !string.IsNullOrEmpty(installed) && !latest.StartsWith(installed, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+$")]
    private static partial Regex Name();
}
