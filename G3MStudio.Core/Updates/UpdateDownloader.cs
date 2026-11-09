using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace G3MStudio.Core.Updates;

public enum UpdateError { DigestMismatch, NotWritable, BadArchive }

/// <summary>Path is the file or folder the message names (the user may need it).</summary>
public sealed class UpdateException(UpdateError error, string message, string? path = null) : Exception(message)
{
    public UpdateError Error { get; } = error;

    public string? Path { get; } = path;
}

public static class UpdateDownloader
{
    /// <summary>
    /// Downloads the offer into folder and returns the verified zip. A leftover .part file resumes with a Range request;
    /// a server that ignores Range, a rejected range or a wrong digest after a resume restarts from zero.
    /// An error during the transfer leaves the .part file for the next call.
    /// </summary>
    public static async Task<string> DownloadAsync(HttpClient http, UpdateOffer offer, string folder, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(folder);
        var zip = Path.Combine(folder, offer.AssetName);
        var part = zip + ".part";
        if (File.Exists(zip))
        {
            if (await Sha256Async(zip, cancellationToken) == offer.Sha256) return zip;
            File.Delete(zip);
        }
        for (var restart = false; ; restart = true)
        {
            if (restart) File.Delete(part);
            var resumed = await FetchAsync(http, offer, part, cancellationToken);
            if (await Sha256Async(part, cancellationToken) == offer.Sha256)
            {
                File.Move(part, zip, true);
                return zip;
            }
            File.Delete(part);
            if (!resumed) throw new UpdateException(UpdateError.DigestMismatch, "The update file did not match its checksum and was deleted.", zip);
        }
    }

    // Returns true when the bytes were appended to an existing part.
    private static async Task<bool> FetchAsync(HttpClient http, UpdateOffer offer, string part, CancellationToken cancellationToken)
    {
        var have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (have > offer.Size) { File.Delete(part); have = 0; }
        using var request = new HttpRequestMessage(HttpMethod.Get, offer.Url);
        if (have > 0) request.Headers.Range = new RangeHeaderValue(have, null);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(part);
            return await FetchAsync(http, offer, part, cancellationToken);
        }
        response.EnsureSuccessStatusCode();
        var append = have > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        await using var file = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(file, cancellationToken);
        return append;
    }

    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
