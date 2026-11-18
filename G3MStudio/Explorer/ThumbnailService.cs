using Avalonia;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Rooms;

namespace G3MStudio.Explorer;

/// <summary>
/// The pictures of the Explorer rows, its tooltips and Go To. A picture is made lazily, off the UI thread where it can be, only when something asks for it,
/// and kept in a bounded cache. Texture pages are decoded once and shared through <see cref="TextureBitmaps"/>. Ask from the UI thread.
/// </summary>
public sealed partial class ThumbnailService(Func<Workspace?> workspace, Func<TextureBitmaps> textures, Func<bool> isLow) : IDisposable
{
    /// <summary>The longest side of a picture; pictures are never made larger than their source.</summary>
    public const int Side = 128;

    /// <summary>The side of the drawn paths and curves, which are line art and look the same at any size.</summary>
    public const int PlotSide = 64;

    /// <summary>The most time a room preview may spend drawing; what is drawn by then is the preview.</summary>
    public static readonly TimeSpan RoomBudget = TimeSpan.FromMilliseconds(600);

    private sealed class Job
    {
        public Task<IImage?> Task = System.Threading.Tasks.Task.FromResult<IImage?>(null);
        public int Waiters;
    }

    private sealed record Recipe(ThumbnailKind Kind, int Signature, List<(GameMakerEmbeddedTexture Page, Rect Source)> Pieces, List<List<(float X, float Y)>> Lines, bool Closed, GameMakerRoom? Room, HashSet<GameMakerEmbeddedTexture>? Pages);

    private readonly ThumbnailCache _cache = new(300);
    private readonly Dictionary<string, Job> _jobs = [];
    private readonly SemaphoreSlim _cheap = new(3), _rooms = new(1);
    private Workspace? _owner;

    public int Cached => _cache.Count;

    public void Dispose()
    {
        _cheap.Dispose();
        _rooms.Dispose();
    }

    /// <summary>Forgets every picture, for low memory.</summary>
    public void Clear() => _cache.Clear();

    // A cache belongs to one workspace; opening another starts empty.
    private void Own(Workspace ws)
    {
        if (ReferenceEquals(ws, _owner)) return;
        _cache.Clear();
        _jobs.Clear();
        _owner = ws;
    }

    /// <summary>True with the picture (null: the entry has none) when it is cached for the current edit version; false when it has to be made or is out of date.</summary>
    public bool TryGet(string key, out IImage? picture)
    {
        picture = null;
        if (workspace() is not { } ws || isLow()) return false;
        Own(ws);
        if (!_cache.TryGet(key, out var entry) || entry.Version != ws.History.Version) return false;
        picture = entry.Picture;
        return true;
    }

    /// <summary>The picture of the entry with this tab key, or null when it has none (the caller shows the icon of the type). Cancelling stops the wait; the work stops too when no one waits for it any more.</summary>
    public async Task<IImage?> GetAsync(string key, CancellationToken cancel = default)
    {
        if (workspace() is not { } ws || isLow() || ResourceTypes.SplitKey(key) is not var (type, _) || !ResourceThumbnails.CanHavePicture(type)) return null;
        if (TryGet(key, out var hit)) return hit;
        var started = !_jobs.TryGetValue(key, out var job);
        job ??= _jobs[key] = new Job();
        job.Waiters++; // counted before the work starts, which may run on at once
        if (started) job.Task = RunAsync(ws, key, type == "rooms" ? _rooms : _cheap, job);
        try { return await job.Task.WaitAsync(cancel); }
        finally { job.Waiters--; }
    }
}
