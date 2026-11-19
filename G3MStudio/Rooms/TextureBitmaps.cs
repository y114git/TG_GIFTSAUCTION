using Avalonia.Media.Imaging;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Rooms;

/// <summary>
/// The texture pages of the open data file as bitmaps, decoded when first asked for and kept while they fit the byte <see cref="DefaultBudget"/> (the least recently asked go first), or until the workspace closes.
/// Decoding a page takes a few hundred milliseconds, so it runs on a pool thread under the model's read lock; only the copy into a bitmap
/// happens on the UI thread. Ask from the UI thread.
/// </summary>
public sealed class TextureBitmaps(ModelLock? gate = null, long budget = TextureBitmaps.DefaultBudget) : IDisposable
{
    // The picture a decode started from: an edit swaps the page's image, so a bitmap of an older image is never handed out again.
    private readonly Dictionary<GameMakerEmbeddedTexture, (Task<Bitmap?> Bitmap, object? Source, long Stamp)> _pages = [];
    private long _clock;

    /// <summary>The decoded pages the cache keeps, in bytes of pixels. A page an open view still draws stays alive through that view; only the cache lets go.</summary>
    public const long DefaultBudget = 256L << 20;

    // A bitmap holds its pixels outside the managed heap, which the collector does not see: the token tells it the size until the bitmap is gone.
    private sealed class Pressure
    {
        private readonly long _bytes;

        public Pressure(long bytes)
        {
            _bytes = bytes;
            GC.AddMemoryPressure(bytes);
        }

        ~Pressure() => GC.RemoveMemoryPressure(_bytes);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bitmap, Pressure> Pressures = [];

    // The newest bitmap of each replaced picture. A view may still draw an older one until its new scene is built (nothing orders the two), so a bitmap is never disposed
    // when it is replaced: older ones are left to the finalizer, the newest is disposed with the workspace.
    private readonly List<(GameMakerEmbeddedTexture Texture, Task<Bitmap?> Bitmap)> _retired = [];

    /// <summary>The page as a bitmap, or null when it has no picture (an empty or unsupported page). Asking twice shares one decode; a page whose image was replaced is decoded again.</summary>
    public Task<Bitmap?> GetAsync(GameMakerEmbeddedTexture texture)
    {
        var source = texture.TextureData?.Image;
        if (_pages.TryGetValue(texture, out var page) && ReferenceEquals(page.Source, source))
        {
            _pages[texture] = page with { Stamp = ++_clock };
            return page.Bitmap;
        }

        if (page.Bitmap is not null) Retire(texture, page.Bitmap);
        var decode = DecodeAsync(texture);
        _pages[texture] = (decode, source, ++_clock);
        return decode;
    }

    private async Task<Bitmap?> DecodeAsync(GameMakerEmbeddedTexture texture)
    {
        var raw = await Task.Run(() =>
        {
            using (gate?.Read()) return RoomScene.DecodePage(texture);
        });
        var bitmap = RoomScene.ToBitmap(raw);
        if (bitmap is not null)
        {
            Pressures.AddOrUpdate(bitmap, new Pressure((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4));
            Enforce(texture, bitmap);
        }

        return bitmap;
    }

    // Past the budget the least recently asked pages leave the cache (not disposed: a view may draw one, and a bitmap frees its pixels when it is finalized).
    private void Enforce(GameMakerEmbeddedTexture keep, Bitmap added)
    {
        // Only a completed task is read, so Result does not block.
        static long Size(Task<Bitmap?> t) => t.IsCompletedSuccessfully && t.Result is { } b ? (long)b.PixelSize.Width * b.PixelSize.Height * 4 : 0;
        var total = _pages.Where(p => !ReferenceEquals(p.Key, keep)).Sum(p => Size(p.Value.Bitmap)) + (long)added.PixelSize.Width * added.PixelSize.Height * 4; // the page being added has not completed yet
        foreach (var old in _pages.Where(p => !ReferenceEquals(p.Key, keep)).OrderBy(p => p.Value.Stamp).Select(p => p.Key).ToList())
        {
            if (total <= budget) break;
            total -= Size(_pages[old].Bitmap);
            _pages.Remove(old);
        }
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

    /// <summary>How many decoded pages the cache holds.</summary>
    public int Cached => _pages.Count;

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
