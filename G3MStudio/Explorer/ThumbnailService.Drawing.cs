using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Explorer;

public sealed partial class ThumbnailService
{
    private async Task<IImage?> RunAsync(Workspace ws, string key, SemaphoreSlim gate, Job job)
    {
        try
        {
            await gate.WaitAsync();
            try
            {
                if (job.Waiters == 0 || !ReferenceEquals(ws, _owner)) return null; // everyone scrolled away
                var version = ws.History.Version;
                var recipe = await Task.Run(() => Prepare(ws, key));
                if (!ReferenceEquals(ws, _owner)) return null;
                if (recipe is null)
                {
                    _cache.Put(key, new(version, 0, null));
                    return null;
                }

                // An edit elsewhere leaves this picture as it was: keep it under the new version.
                if (_cache.TryGet(key, out var old) && old.Signature == recipe.Signature)
                {
                    _cache.Put(key, old with { Version = version });
                    return old.Picture;
                }

                var picture = await DrawAsync(ws, recipe);
                if (ReferenceEquals(ws, _owner)) _cache.Put(key, new(version, recipe.Signature, picture));
                return picture;
            }
            finally { gate.Release(); }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or OutOfMemoryException or NotSupportedException)
        {
            return null; // a picture is optional; the row keeps its icon and the next ask tries again
        }
        finally
        {
            if (_jobs.TryGetValue(key, out var current) && current == job) _jobs.Remove(key);
        }
    }

    // Reads the model under its read lock and keeps only plain data, so the drawing needs no lock.
    private static Recipe? Prepare(Workspace ws, string key)
    {
        using (ws.Lock.Read())
        {
            var resource = ResourceTypes.Find(ws.Data, key);
            var kind = ResourceThumbnails.KindOf(resource);
            if (kind == ThumbnailKind.Icon) return null;
            var signature = ResourceThumbnails.Signature(resource);
            List<(GameMakerEmbeddedTexture, Rect)> pieces = [];
            switch (resource)
            {
                case GameMakerRoom room:
                    return new(kind, signature, pieces, [], false, room, RoomScene.PagesUsedBy(room));
                case GameMakerPath path:
                    return new(kind, signature, pieces, [ResourceThumbnails.PathPoints(path)], path.IsClosed, null, null);
                case GameMakerAnimationCurve curve:
                    return new(kind, signature, pieces, ResourceThumbnails.CurvePoints(curve), false, null, null);
                case GameMakerEmbeddedTexture page:
                    pieces.Add((page, default));
                    break;
                default:
                    if (ResourceThumbnails.ItemOf(resource) is not { TexturePage: { } texture } item) return null;
                    var origin = new Vector(item.SourceX, item.SourceY);
                    var sample = resource is GameMakerFont font ? ResourceThumbnails.FontSample(font) : [];
                    if (sample.Count == 0) pieces.Add((texture, new Rect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight)));
                    else pieces.AddRange(sample.Select(r => (texture, new Rect(r.X, r.Y, r.Width, r.Height).Translate(origin))));
                    break;
            }

            return new(kind, signature, pieces, [], false, null, null);
        }
    }

    private async Task<IImage?> DrawAsync(Workspace ws, Recipe recipe)
    {
        if (recipe.Lines.Count > 0)
        {
            var (line, point) = (Plain(CanvasTheme.Line), Plain(CanvasTheme.Point));
            return await Task.Run(() => ThumbnailDrawing.DrawLines(recipe.Lines, recipe.Closed, line, point));
        }

        // The pages are shared with the editors and decoded once.
        var pages = new Dictionary<GameMakerEmbeddedTexture, Bitmap?>();
        foreach (var page in recipe.Room is null ? recipe.Pieces.Select(p => p.Page) : recipe.Pages ?? [])
            if (!pages.ContainsKey(page)) pages[page] = await textures().GetAsync(page);
        if (recipe.Room is { } room)
            return await Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                using (ws.Lock.Read())
                using (var scene = RoomScene.Build(room, p => pages.GetValueOrDefault(p), ownsPages: false)) return ThumbnailDrawing.DrawRoom(scene, clock);
            });

        var drawn = recipe.Pieces.Select(p => (Page: pages.GetValueOrDefault(p.Page), p.Source)).ToList();
        var pieces = new List<(Bitmap Page, Rect Source)>();
        foreach (var (page, source) in drawn)
        {
            if (page is null) return null;
            pieces.Add((page, source));
        }

        return await Task.Run(() => ThumbnailDrawing.DrawPieces(pieces));
    }

    private static Color Plain(IBrush brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
}
