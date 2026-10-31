using Avalonia.Media.Imaging;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Rooms;

/// <summary>
/// The texture pages of the open data file as bitmaps, decoded when first asked for and kept until the workspace closes.
/// Decoding a page takes a few hundred milliseconds, so it runs on a pool thread under the model's read lock; only the copy into a bitmap
/// happens on the UI thread. Ask from the UI thread.
/// </summary>
public sealed class TextureBitmaps(ModelLock? gate = null) : IDisposable
{
    private readonly Dictionary<GameMakerEmbeddedTexture, Task<Bitmap?>> _pages = [];

    /// <summary>The page as a bitmap, or null when it has no picture (an empty or unsupported page). Asking twice shares one decode.</summary>
    public Task<Bitmap?> GetAsync(GameMakerEmbeddedTexture texture)
    {
        if (!_pages.TryGetValue(texture, out var page)) _pages[texture] = page = DecodeAsync(texture);
        return page;
    }

    private async Task<Bitmap?> DecodeAsync(GameMakerEmbeddedTexture texture)
    {
        var raw = await Task.Run(() =>
        {
            using (gate?.Read()) return RoomScene.DecodePage(texture);
        });
        return RoomScene.ToBitmap(raw);
    }

    /// <summary>Drops the decoded page so the next ask decodes it again, after its picture changed.</summary>
    public void Forget(GameMakerEmbeddedTexture texture)
    {
        if (_pages.Remove(texture, out var page) && page.IsCompletedSuccessfully) page.Result?.Dispose();
    }

    public void Dispose()
    {
        foreach (var page in _pages.Values)
            if (page.IsCompletedSuccessfully) page.Result?.Dispose();
        _pages.Clear();
    }
}
