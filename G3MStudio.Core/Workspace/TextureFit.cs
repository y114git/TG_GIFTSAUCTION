using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>How a picture finds room on the texture pages, cheapest first.</summary>
public enum FitKind
{
    /// <summary>The picture is not larger than the item's own rectangle: it stays where it is.</summary>
    InPlace,

    /// <summary>A free area of the item's page (or of another page of its group) holds it; nothing else moves.</summary>
    FreeSpace,

    /// <summary>The pictures of the page are packed again within the page to make room; the moved items keep their pixels and their references.</summary>
    Repack,

    /// <summary>The page grows to the right and down (the next power of two); every other item keeps its coordinates.</summary>
    Grow,

    /// <summary>Nothing above fits: a new page joins the texture group.</summary>
    NewPage,
}

/// <summary>Where one picture goes. <see cref="PageBefore"/> and <see cref="PageAfter"/> differ only for a page that grows or a new page (before is 0 x 0).</summary>
public sealed record FitStep(FitKind Kind, string Page, (int Width, int Height) PageBefore, (int Width, int Height) PageAfter, int Moved);

/// <summary>One picture to place: the item it is for (null for a new item made from <see cref="Template"/>) and its size.</summary>
public sealed record FitRequest(GameMakerTexturePageItem? Item, int Width, int Height, GameMakerTexturePageItem? Template = null)
{
    /// <summary>Where the picture starts from: the item itself, or for a new item its template (a new item has no page yet).</summary>
    public GameMakerTexturePageItem Origin => Item ?? Template ?? throw new InvalidOperationException("A request needs an item or a template.");
}

/// <summary>
/// The advanced choices of a placement: a page to try first (its free space, then growing it), whether a page may grow, whether a new page may be made, and the
/// largest side a page may grow to or be made with (0: the largest page of the file, at least 2048). The journal keeps them with the edit.
/// </summary>
public sealed record FitOptions(string? Page = null, bool Grow = true, bool NewPage = true, int MaxSide = 0)
{
    public static readonly FitOptions Default = new();

    public string ToText() => string.Join(';', Grow ? "1" : "0", NewPage ? "1" : "0", MaxSide.ToString(System.Globalization.CultureInfo.InvariantCulture), Page ?? "");

    public static FitOptions Parse(string text) => text.Split(';', 4) is [var grow, var made, var max, var page]
        ? new(page.Length == 0 ? null : page, grow == "1", made == "1", int.Parse(max, System.Globalization.CultureInfo.InvariantCulture))
        : throw new InvalidDataException($"'{text}' are not texture page options.");

    internal static FitOptions? From(IReadOnlyDictionary<string, string> args) => args.TryGetValue("fit", out var text) ? Parse(text) : null;
}

/// <summary>Adds <see cref="FitOptions"/> to the arguments of an edit.</summary>
internal static class FitArgs
{
    /// <summary>The edit's arguments with the options added when they are not the default ones (older journals have none).</summary>
    public static Dictionary<string, string> With(this Dictionary<string, string> args, FitOptions? options)
    {
        if (options is not null && options != FitOptions.Default) args["fit"] = options.ToText();
        return args;
    }
}

/// <summary>
/// Places pictures of a new size on the texture pages without touching texture page item properties by hand. For each picture, in this order: its own place when it is
/// not larger; free space on its page; the page packed again; the page grown to the next power of two (up to the largest page size of the file, at least 2048);
/// free space on another page of the same texture group; a new page in that group. Pages made by an earlier picture of the same edit are used by the next ones, so an
/// edit never makes more pages than it needs. Items that share a rectangle (frames that point at the same pixels) move together.
/// <see cref="Plan"/> only works it out (the dialogs show it); <see cref="TextureFitCommand"/> and <see cref="PageResizeCommand"/> do it as one undoable edit.
/// </summary>
public static class TextureFit
{
    /// <summary>Pixels kept free between pictures, so a filtered sample at the edge of one does not pick up its neighbour.</summary>
    public const int Gap = 2;

    /// <summary>The largest texture page side GameMaker makes; a page or a picture larger than this is refused.</summary>
    public const int MaxPageSide = 8192;

    private const int SmallestPage = 64;

    internal sealed class Space(GameMakerEmbeddedTexture? page, int width, int height, GameMakerTextureGroupInfo? group, string name)
    {
        public GameMakerEmbeddedTexture? Page { get; set; } = page;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
        public GameMakerTextureGroupInfo? Group { get; } = group;
        public string Name { get; } = name;
        public (int Width, int Height) Start { get; } = (width, height);
        public List<(Box Rect, List<GameMakerTexturePageItem> Items)> Used { get; } = [];
        public bool Created => Page is null;
    }

    /// <summary>The result of one placement: the request, its step, its place, and the items the page packing moved (with their new rectangles on the same page).</summary>
    internal sealed record Placement(FitRequest Request, FitStep Step, Space Space, Box Rect, List<(GameMakerTexturePageItem Item, Box From, Box To)> Moves)
    {
        /// <summary>Every item that takes <see cref="Rect"/>: the request's item, and for a picture moved off a page cut smaller, every item that shared its rectangle.</summary>
        public IReadOnlyList<GameMakerTexturePageItem> Items { get; init; } = Request.Item is { } item ? [item] : [];
    }

    internal sealed class Layout
    {
        public List<Space> Spaces { get; } = [];
        public int MaxSide { get; init; }
        public FitOptions Options { get; init; } = FitOptions.Default;

        /// <summary>The page being cut smaller, which never grows again for the pictures moved off it.</summary>
        public Space? Resized { get; set; }
    }

    /// <summary>The steps of placing the pictures, without changing the data file.</summary>
    public static IReadOnlyList<FitStep> Plan(GameMakerData data, IReadOnlyList<FitRequest> requests, GameMakerEmbeddedTexture? resizePage = null, (int Width, int Height) resizeTo = default, FitOptions? options = null) =>
        [.. Run(data, requests, resizePage, resizeTo, options).Placements.Select(p => p.Step)];

    /// <summary>The largest side a page grows to without options: the largest page of the file, at least 2048, at most <see cref="MaxPageSide"/>.</summary>
    public static int AutomaticMaxSide(GameMakerData data)
    {
        var largest = data.EmbeddedTextures.Where(t => t.TextureData?.Image is not null).Select(t => Math.Max(t.TextureData.Image.Width, t.TextureData.Image.Height)).DefaultIfEmpty(0).Max();
        return Math.Min(MaxPageSide, Math.Max(2048, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, largest))));
    }

    internal static (Layout Layout, List<Placement> Placements, List<GameMakerTexturePageItem> Evicted) Run(GameMakerData data, IReadOnlyList<FitRequest> requests, GameMakerEmbeddedTexture? resizePage, (int Width, int Height) resizeTo, FitOptions? options = null)
    {
        foreach (var r in requests)
            if (r.Width is < 1 or > MaxPageSide || r.Height is < 1 or > MaxPageSide)
                throw new InvalidDataException($"A picture of {r.Width}x{r.Height} cannot be placed: a texture page side is 1 to {MaxPageSide} pixels.");
        var layout = Read(data, options ?? FitOptions.Default);
        var evicted = new List<GameMakerTexturePageItem>();
        var all = requests.Select(r => (Request: r, Items: (IReadOnlyList<GameMakerTexturePageItem>)(r.Item is { } i ? [i] : []))).ToList();
        if (resizePage is not null)
        {
            var space = layout.Spaces.FirstOrDefault(s => s.Page == resizePage) ?? throw new InvalidOperationException(Outside(resizePage));
            (space.Width, space.Height) = resizeTo;
            layout.Resized = space;
            // Pictures that no longer lie on the smaller page find a new place like any other picture.
            foreach (var slot in space.Used.Where(u => u.Rect.X + u.Rect.Width > resizeTo.Width || u.Rect.Y + u.Rect.Height > resizeTo.Height).ToList())
            {
                space.Used.Remove(slot);
                evicted.AddRange(slot.Items);
                all.Add((new FitRequest(slot.Items[0], slot.Rect.Width, slot.Rect.Height), slot.Items));
            }
        }

        var placements = new List<Placement>();
        foreach (var (request, items) in all) placements.Add(Place(layout, request, items, data, evicted, placements));
        return (layout, placements, evicted);
    }

    // External pages are loaded by the game from files of their own, so a change made here would never reach them.
    private static string Outside(GameMakerEmbeddedTexture? page) => page is null
        ? "The picture has no texture page."
        : $"Texture page '{page.Name?.Content}' {(page.TextureExternal ? "is stored outside the data file" : "has no picture")}, so its pictures and its size cannot be changed here.";

    private static Layout Read(GameMakerData data, FitOptions options)
    {
        var layout = new Layout { MaxSide = options.MaxSide > 0 ? Math.Clamp(options.MaxSide, SmallestPage, MaxPageSide) : AutomaticMaxSide(data), Options = options };
        var spaces = new Dictionary<GameMakerEmbeddedTexture, Space>();
        foreach (var page in data.EmbeddedTextures.Where(t => !t.TextureExternal && t.TextureData?.Image is not null))
        {
            var space = new Space(page, page.TextureData.Image.Width, page.TextureData.Image.Height, page.TextureInfo, page.Name?.Content ?? "");
            spaces[page] = space;
            layout.Spaces.Add(space);
        }

        foreach (var item in data.TexturePageItems.Where(i => i?.TexturePage is not null))
        {
            if (!spaces.TryGetValue(item.TexturePage, out var space)) continue;
            var rect = new Box(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight);
            var slot = space.Used.FindIndex(u => u.Rect == rect);
            if (slot >= 0) space.Used[slot].Items.Add(item);
            else space.Used.Add((rect, [item]));
        }

        return layout;
    }

    private static Placement Place(Layout layout, FitRequest request, IReadOnlyList<GameMakerTexturePageItem> together, GameMakerData data, List<GameMakerTexturePageItem> evicted, List<Placement> done)
    {
        var (w, h) = (request.Width, request.Height);
        var origin = request.Origin;
        var home = layout.Spaces.FirstOrDefault(s => s.Page == origin.TexturePage) ?? throw new InvalidOperationException(Outside(origin.TexturePage));
        // The request's own item leaves its slot (its rectangle is free for it again) unless other items share that slot and stay.
        if (request.Item is { } own)
        {
            // A picture an earlier packing of this edit moved is placed by this request alone, so its pixels are written once, at its last place.
            foreach (var (i, p) in done.Index().ToList())
                if (p.Moves.RemoveAll(m => ReferenceEquals(m.Item, own)) > 0) done[i] = p with { Step = p.Step with { Moved = p.Moves.Count } };

            var slot = home.Used.FindIndex(u => u.Items.Contains(own));
            if (slot >= 0)
            {
                var (rect, items) = home.Used[slot];
                if (items.Count == 1 && !evicted.Contains(own) && w <= rect.Width && h <= rect.Height)
                {
                    home.Used[slot] = (rect with { Width = w, Height = h }, items);
                    return new Placement(request, Step(FitKind.InPlace, home, 0), home, rect with { Width = w, Height = h }, []);
                }

                if (items.Count == 1) home.Used.RemoveAt(slot);
                else items.Remove(own);
            }
        }

        Placement Done(FitKind kind, Space space, Box at, List<(GameMakerTexturePageItem, Box, Box)>? moves = null)
        {
            space.Used.Add((at, [.. together]));
            return new Placement(request, Step(kind, space, moves?.Count ?? 0), space, at, moves ?? []) { Items = together };
        }

        var options = layout.Options;
        // a page chosen in the dialog comes first: its free space, then the page grown
        if (options.Page is { } name && layout.Spaces.FirstOrDefault(s => s.Name == name) is { } chosen && chosen != home)
        {
            if (Free(chosen, w, h) is { } there) return Done(chosen.Created ? FitKind.NewPage : FitKind.FreeSpace, chosen, there);
            if (Grown(layout, chosen, w, h) is { } spot) return Done(FitKind.Grow, chosen, spot);
        }

        if (Free(home, w, h) is { } free) return Done(FitKind.FreeSpace, home, free);
        if (Repack(home, w, h) is { } packed) return Done(FitKind.Repack, home, packed.Spot, Repacked(home, packed.Others, done));
        if (Grown(layout, home, w, h) is { } grown) return Done(FitKind.Grow, home, grown);

        foreach (var other in layout.Spaces.Where(s => s != home && s.Group == home.Group && s.Page is not { TextureExternal: true }))
            if (Free(other, w, h) is { } there) return Done(other.Created ? FitKind.NewPage : FitKind.FreeSpace, other, there);

        if (!options.NewPage) throw new InvalidDataException($"No page of the texture group has room for a picture of {w}x{h}, and a new page is not allowed.");
        var side = Math.Max(SmallestPage, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(w, h)));
        var limit = options.MaxSide > 0 ? layout.MaxSide : MaxPageSide; // a chosen largest size holds for new pages too
        if (side > limit) throw new InvalidDataException($"A picture of {w}x{h} is larger than a texture page can be ({limit}x{limit}).");
        // a page edit is journaled by page name, so the new page takes a name no page has
        var number = data.EmbeddedTextures.Count + layout.Spaces.Count(s => s.Created);
        while (data.EmbeddedTextures.Any(t => t.Name?.Content == $"Texture {number}") || layout.Spaces.Any(s => s.Name == $"Texture {number}")) number++;
        var page = new Space(null, side, side, home.Group, $"Texture {number}");
        layout.Spaces.Add(page);
        return Done(FitKind.NewPage, page, new Box(0, 0, w, h));
    }

    // The page grown to the first size of its growth where the picture fits; null when growing is off, the page is being cut, or no size up to the largest fits.
    private static Box? Grown(Layout layout, Space space, int w, int h)
    {
        if (!layout.Options.Grow || space == layout.Resized) return null;
        foreach (var (gw, gh) in Growth(space.Width, space.Height, layout.MaxSide))
        {
            if (Free(space, w, h, gw, gh) is not { } spot) continue;
            (space.Width, space.Height) = (gw, gh);
            return spot;
        }

        return null;
    }

    // Puts the page's rectangles at their packed places and returns the moves of the items that move for the first time in this edit.
    private static List<(GameMakerTexturePageItem, Box, Box)> Repacked(Space home, Box[] packed, List<Placement> done)
    {
        var moves = new List<(GameMakerTexturePageItem, Box, Box)>();
        var repacked = new List<(Box Rect, List<GameMakerTexturePageItem> Items)>();
        // Places are matched as they were before this packing: a rectangle moved onto the old place of another one is not moved a second time.
        var rects = done.Select(p => p.Rect).ToList();
        var targets = done.Select(p => p.Moves.Select(m => m.To).ToList()).ToList();
        foreach (var (used, now) in home.Used.Zip(packed))
        {
            repacked.Add((now, used.Items));
            if (used.Rect == now) continue;
            // A rectangle placed or moved earlier in this edit only changes where it goes: its pixels are still read from where they were before the edit.
            var earlier = Enumerable.Range(0, done.Count).FirstOrDefault(i => done[i].Space == home && rects[i] == used.Rect, -1);
            if (earlier >= 0)
            {
                done[earlier] = done[earlier] with { Rect = now };
                continue;
            }

            var movedBefore = false;
            foreach (var (i, p) in done.Index())
                foreach (var (j, to) in targets[i].Index())
                    if (p.Space == home && to == used.Rect)
                    {
                        p.Moves[j] = p.Moves[j] with { To = now };
                        movedBefore = true;
                    }

            if (!movedBefore) foreach (var item in used.Items) moves.Add((item, used.Rect, now));
        }

        home.Used.Clear();
        home.Used.AddRange(repacked);
        return moves;
    }

    private static FitStep Step(FitKind kind, Space space, int moved) =>
        new(kind, space.Name, space.Created ? (0, 0) : space.Start, (space.Width, space.Height), moved);

    // Doubles the shorter side first, so the page stays near square, up to the largest side allowed.
    private static IEnumerable<(int Width, int Height)> Growth(int width, int height, int max)
    {
        var (w, h) = ((int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)width), (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)height));
        if ((w, h) != (width, height) && w <= max && h <= max) yield return (w, h);
        while (w < max || h < max)
        {
            if (w <= h && w < max) w *= 2;
            else h *= 2;
            yield return (w, h);
        }
    }

    private static bool Overlaps(Box a, Box b) =>
        a.X < b.X + b.Width + Gap && b.X < a.X + a.Width + Gap && a.Y < b.Y + b.Height + Gap && b.Y < a.Y + a.Height + Gap;

    // The top-most, then left-most spot next to a used rectangle (or the corner) where the picture fits.
    private static Box? Free(Space space, int w, int h, int? width = null, int? height = null)
    {
        var (pw, ph) = (width ?? space.Width, height ?? space.Height);
        if (w > pw || h > ph) return null;
        var xs = space.Used.Select(u => u.Rect.X + u.Rect.Width + Gap).Prepend(0).Distinct().Order().ToList();
        var ys = space.Used.Select(u => u.Rect.Y + u.Rect.Height + Gap).Prepend(0).Distinct().Order().ToList();
        var band = new List<Box>();
        foreach (var y in ys)
        {
            if (y + h > ph) break;
            // only the rectangles that reach into this row can block a spot in it
            band.Clear();
            foreach (var (rect, _) in space.Used)
                if (rect.Y < y + h + Gap && y < rect.Y + rect.Height + Gap) band.Add(rect);
            foreach (var x in xs)
            {
                if (x + w > pw) break;
                var at = new Box(x, y, w, h);
                if (!band.Exists(r => Overlaps(r, at))) return at;
            }
        }

        return null;
    }

    // Shelf packing of every rectangle of the page and the new one, tallest first; null when they do not fit in the page as it is.
    private static (Box Spot, Box[] Others)? Repack(Space space, int w, int h)
    {
        var sizes = space.Used.Select((u, i) => (Index: i, u.Rect.Width, u.Rect.Height)).Append((Index: -1, Width: w, Height: h)).OrderByDescending(s => s.Height).ThenByDescending(s => s.Width).ToList();
        var others = new Box[space.Used.Count];
        Box spot = default;
        int x = 0, y = 0, shelf = 0;
        foreach (var (index, sw, sh) in sizes)
        {
            if (sw > space.Width) return null;
            if (x + sw > space.Width)
            {
                (x, y, shelf) = (0, y + shelf + Gap, 0);
            }

            if (y + sh > space.Height) return null;
            var at = new Box(x, y, sw, sh);
            if (index < 0) spot = at;
            else others[index] = at;
            x += sw + Gap;
            shelf = Math.Max(shelf, sh);
        }

        return (spot, others);
    }
}
