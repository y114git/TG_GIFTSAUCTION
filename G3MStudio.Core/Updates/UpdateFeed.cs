using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace G3MStudio.Core.Updates;

/// <summary>Version with the SemVer ordering that matters here: numeric core, then pre-release identifiers (a release beats its pre-releases).</summary>
public readonly record struct SemVer(int Major, int Minor, int Patch, string Pre) : IComparable<SemVer>
{
    public static bool TryParse(string? text, out SemVer version)
    {
        version = default;
        var s = (text ?? "").TrimStart('v', 'V');
        var plus = s.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-', StringComparison.Ordinal);
        var pre = dash >= 0 ? s[(dash + 1)..] : "";
        var core = (dash >= 0 ? s[..dash] : s).Split('.');
        if (core.Length != 3 || !int.TryParse(core[0], out var major) || !int.TryParse(core[1], out var minor) || !int.TryParse(core[2], out var patch)) return false;
        version = new SemVer(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(SemVer other)
    {
        var c = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (c != 0) return c;
        if (Pre == other.Pre) return 0;
        if (Pre.Length == 0) return 1;
        if (other.Pre.Length == 0) return -1;
        var a = Pre.Split('.');
        var b = other.Pre.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var an = int.TryParse(a[i], out var x);
            var bn = int.TryParse(b[i], out var y);
            c = an && bn ? x.CompareTo(y) : an ? -1 : bn ? 1 : string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Pre.Length > 0 ? "-" + Pre : "");

    public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;

    public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;

    public static bool operator <=(SemVer a, SemVer b) => a.CompareTo(b) <= 0;

    public static bool operator >=(SemVer a, SemVer b) => a.CompareTo(b) >= 0;
}

/// <summary>The zip a newer release offers for this system. Sha256 is the digest GitHub publishes for the asset.</summary>
public sealed record UpdateOffer(SemVer Version, string AssetName, Uri Url, long Size, string Sha256);

/// <summary>Reads the releases list of the owner's repository (ADR-0005). DownloadPrefix keeps assets to that repository's releases.</summary>
public sealed class UpdateFeed(Uri releasesUrl, string downloadPrefix)
{
    public static UpdateFeed GitHub(string repository) =>
        new(new Uri($"https://api.github.com/repos/{repository}/releases"), $"https://github.com/{repository}/releases/download/");

    public static string CurrentOs => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    public static string CurrentArch => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    public async Task<UpdateOffer?> CheckAsync(HttpClient http, SemVer current, bool prerelease, string os, string arch, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, releasesUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("G3MStudio", current.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return Pick(await response.Content.ReadAsStringAsync(cancellationToken), current, prerelease, os, arch);
    }

    /// <summary>The highest eligible release above current that has a zip for os and arch with a SHA-256 digest; null when none (never a downgrade).</summary>
    public UpdateOffer? Pick(string json, SemVer current, bool prerelease, string os, string arch)
    {
        UpdateOffer? best = null;
        using var doc = JsonDocument.Parse(json);
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean() && !prerelease) continue;
            if (!SemVer.TryParse(release.GetProperty("tag_name").GetString(), out var version) || version <= current || best is not null && version <= best.Version) continue;
            var name = $"G3MStudio-{os}-{arch}-{version}.zip";
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                if (asset.GetProperty("name").GetString() != name || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !IsTrusted(url)) continue;
                best = new UpdateOffer(version, name, new Uri(url), asset.GetProperty("size").GetInt64(), digest[7..].ToLowerInvariant());
            }
        }
        return best;
    }

    // HTTPS from the repository's releases only; loopback may use http so tests can run a local feed.
    private bool IsTrusted(string url) =>
        url.StartsWith(downloadPrefix, StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback);
}
