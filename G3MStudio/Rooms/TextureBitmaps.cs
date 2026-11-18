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
    // The picture a decode started from: an edit swaps the page's image, so a bitmap of an older image is never handed out again.
    private readonly Dictionary<GameMakerEmbeddedTexture, (Task<Bitmap?> Bitmap, object? Source)> _pages = [];

    // The newest bitmap of each replaced picture. A view may still draw an older one until its new scene is built (nothing orders the two), so a bitmap is never disposed
    // when it is replaced: older ones are left to the finalizer, the newest is disposed with the workspace.
    private readonly List<(GameMakerEmbeddedTexture Texture, Task<Bitmap?> Bitmap)> _retired = [];

    /// <summary>The page as a bitmap, or null when it has no picture (an empty or unsupported page). Asking twice shares one decode; a page whose image was replaced is decoded again.</summary>
    public Task<Bitmap?> GetAsync(GameMakerEmbeddedTexture texture)
    {
        var source = texture.TextureData?.Image;
        if (_pages.TryGetValue(texture, out var page) && ReferenceEquals(page.Source, source)) return page.Bitmap;
        if (page.Bitmap is not null) Retire(texture, page.Bitmap);
        var decode = DecodeAsync(texture);
        _pages[texture] = (decode, source);
        return decode;
    }

    private async Task<Bitmap?> DecodeAsync(GameMakerEmbeddedTexture texture)
    {
        var raw = await Task.Run(() =>
        {
            using (gate?.Read()) return RoomScene.DecodePage(texture);
        });
        return RoomScene.ToBitmap(raw);
    }

    /// <summary>Drops the decoded page so the next ask decodes it again, after its picture changed. Not disposed: an open view may still draw it; the bitmap frees its pixels when it is finalized.</summary>
    public void Forget(GameMakerEmbeddedTexture texture)
    {
        if (_pages.Remove(texture, out var page)) Retire(texture, page.Bitmap);
    }

    private void Retire(GameMakerEmbeddedTexture texture, Task<Bitmap?> bitmap)
    {
        _retired.RemoveAll(r => r.Texture == texture);
        _retired.Add((texture, bitmap));
    }

    /// <summary>
    /// Forgets the decoded pages so they can be collected and decoded again when asked for. They are not disposed here: an open view may still draw one,
    /// and the bitmap frees its pixels when it is finalized.
    /// </summary>
    public void Trim() => _pages.Clear();

    public void Dispose()
    {
        foreach (var page in _pages.Values) DisposeDone(page.Bitmap);
        _pages.Clear();
        foreach (var retired in _retired) DisposeDone(retired.Bitmap);
        _retired.Clear();
    }

    private static void DisposeDone(Task<Bitmap?> page)
    {
        // The task has completed here, so Result does not block.
        if (page.IsCompletedSuccessfully) page.Result?.Dispose();
    }
}
