namespace G3MStudio.Core.Links;

/// <summary>
/// Downloads the file a g3mstudio:// web link names into a folder of the caller's choice. Only https is fetched, the size is capped while streaming,
/// and the file name comes from the last part of the address with every path character removed, so a link cannot write outside the folder.
/// The caller asks the user before calling this and decides what to do with the file.
/// </summary>
public static class LinkDownloader
{
    public const long DefaultLimit = 1L << 30;

    public static string FileNameOf(Uri uri)
    {
        var name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name.Trim('.', ' ').Length == 0 ? "download" : name;
    }

    public static async Task<string> DownloadAsync(HttpClient http, string address, string folder, long limit = DefaultLimit, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Only https addresses are downloaded.", nameof(address));
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, FileNameOf(uri));
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new IOException("The file is larger than the allowed size.");
        var partial = target + ".part";
        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(partial))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > limit) throw new IOException("The file is larger than the allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    progress?.Report(total);
                }
            }

            File.Move(partial, target, true);
            return target;
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            throw;
        }
    }
}
