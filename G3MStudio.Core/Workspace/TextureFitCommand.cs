using System.Globalization;
using G3MLib.DataFile;
using G3MStudio.Core.Settings;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The base of the edits that place pictures with <see cref="TextureFit"/>: it works out the places, reads the pixels that move, makes or grows pages, writes the pixels
/// and sets the page items, all as one step. Undo puts every page image, page size, item rectangle and list back; redo puts the result back without working it out again.
/// </summary>
public abstract class TextureFitEdit : EditCommand
{
    private State? _before, _after;
    private List<GameMakerTexturePageItem> _items = [];
    private List<GameMakerTexturePageItem?> _made = [];
    private List<GameMakerTexturePageItem?> _templates = [];

    /// <summary>The steps the last apply took (for the status line and the tests).</summary>
    public IReadOnlyList<FitStep> Steps { get; private set; } = [];

    /// <summary>The advanced choices of the placement (see <see cref="FitOptions"/>); null for the default ones.</summary>
    protected virtual FitOptions? Fit => null;

    /// <summary>The pictures to place, each with its pixels (null keeps the pixels the item has), and a page to resize first.</summary>
    protected abstract (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data);

    /// <summary>Points the resource at the new item made for request <paramref name="index"/> (a frame that shared its item gets one of its own), or back at the old one on undo.</summary>
    protected virtual void Bind(GameMakerData data, int index, GameMakerTexturePageItem item)
    {
    }

    public override void Apply(GameMakerData data)
    {
        if (_after is not null)
        {
            _after.Restore(data, _items, true);
            for (var i = 0; i < _made.Count; i++)
                if (_made[i] is { } item) Bind(data, i, item);
            return;
        }

        var (requests, pictures, resize, size) = Prepare(data);
        var (layout, placements, evicted) = TextureFit.Run(data, requests, resize, size, Fit);
        Steps = [.. placements.Select(p => p.Step)];
        var touched = placements.SelectMany(p => p.Moves.Select(m => m.Item).Concat(p.Request.Item is { } i ? [i] : [])).Concat(evicted).Distinct().ToList();
        _before = State.Of(data, touched);
        _templates = [.. requests.Select(r => r.Template)];
        _made = [.. Enumerable.Repeat<GameMakerTexturePageItem?>(null, requests.Count)];
        try
        {
            Place(data, layout, placements, evicted, pictures);
        }
        catch (Exception e)
        {
            FailureReport.Report(e, "texture.fit");
            // a step that throws halfway leaves no half edit behind: the history records nothing, so the data must be as before
            _items = [.. _made.OfType<GameMakerTexturePageItem>()];
            Revert(data);
            (_before, _items, _made, _templates) = (null, [], [], []);
            throw;
        }

        _items = [.. _made.OfType<GameMakerTexturePageItem>()];
        _after = State.Of(data, [.. touched, .. _items]);
    }

    private void Place(GameMakerData data, TextureFit.Layout layout, List<TextureFit.Placement> placements, List<GameMakerTexturePageItem> evicted, IReadOnlyList<PixelBuffer?> pictures)
    {
        // Every pixel that moves is read before any page changes.
        var moving = new Dictionary<(GameMakerEmbeddedTexture, Box), PixelBuffer>();
        PixelBuffer Old(GameMakerTexturePageItem item, Box rect) => moving.TryGetValue((item.TexturePage, rect), out var p) ? p : moving[(item.TexturePage, rect)] = PagePixels.Read(item.TexturePage, rect.X, rect.Y, rect.Width, rect.Height);
        var payload = new PixelBuffer[placements.Count];
        for (var i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            foreach (var (item, from, _) in p.Moves) Old(item, from);
            var given = i < pictures.Count ? pictures[i] : null;
            payload[i] = given ?? (p.Request.Item is { } kept ? Old(kept, RectOf(kept)) : throw new InvalidOperationException("A new item needs a picture."));
        }

        foreach (var space in layout.Spaces.Where(s => s.Created)) space.Page = NewPage(data, space, placements.First(p => p.Space == space).Request.Origin.TexturePage);
        foreach (var space in layout.Spaces)
            if (space.Page is { } made && (space.Width, space.Height) != (made.TextureData.Image.Width, made.TextureData.Image.Height)) PagePixels.Resize(made, space.Width, space.Height);

        // Old places are cleared where no other item still uses them, then the pictures are written, page by page.
        var writes = new Dictionary<GameMakerEmbeddedTexture, (List<(int, int, PixelBuffer)> Pictures, List<Box> Clear)>();
        (List<(int, int, PixelBuffer)> Pictures, List<Box> Clear) On(GameMakerEmbeddedTexture page) => writes.TryGetValue(page, out var w) ? w : writes[page] = ([], []);
        var keep = placements.SelectMany(p => p.Moves.Select(m => m.Item)).Concat(evicted).ToHashSet(); // moved as they are: their size and box stay
        foreach (var (i, p) in placements.Index())
        {
            if (p.Request.Item is { } own && !Shared(data, own)) On(own.TexturePage).Clear.Add(RectOf(own));
            foreach (var (item, from, to) in p.Moves)
            {
                On(item.TexturePage).Clear.Add(from);
                On(item.TexturePage).Pictures.Add((to.X, to.Y, moving[(item.TexturePage, from)]));
            }

            On(PageOf(p)).Pictures.Add((p.Rect.X, p.Rect.Y, payload[i]));
        }

        foreach (var (page, (list, clear)) in writes)
        {
            // a page cut smaller keeps no rectangle outside it
            var (pw, ph) = (page.TextureData.Image.Width, page.TextureData.Image.Height);
            PagePixels.Write(page, list.Distinct(), clear.Select(c => c with { Width = Math.Max(0, Math.Min(c.X + c.Width, pw) - c.X), Height = Math.Max(0, Math.Min(c.Y + c.Height, ph) - c.Y) }).Where(c => c.Width > 0 && c.Height > 0));
        }

        for (var i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            foreach (var (item, _, to) in p.Moves) (item.SourceX, item.SourceY) = ((ushort)to.X, (ushort)to.Y);
            var target = p.Request.Item;
            if (target is null)
            {
                var template = p.Request.Template ?? throw new InvalidOperationException("A request needs an item or a template.");
                target = new GameMakerTexturePageItem { TargetX = template.TargetX, TargetY = template.TargetY, BoundingWidth = template.BoundingWidth, BoundingHeight = template.BoundingHeight };
                data.TexturePageItems.Add(target);
                _made[i] = target;
                Bind(data, i, target);
            }

            Set(target, PageOf(p), p.Rect, p.Request.Template ?? target, keep.Contains(target), KeepsBox(data));
            foreach (var other in p.Items.Where(o => !ReferenceEquals(o, target))) Set(other, PageOf(p), p.Rect, other, true, true);
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_before is null) throw new InvalidOperationException("The edit was never applied.");
        _before.Restore(data, _items, false);
        for (var i = 0; i < _made.Count; i++)
            if (_made[i] is not null && _templates[i] is { } old) Bind(data, i, old);
    }

    // Every created space has its page by the time the pictures are written.
    private static GameMakerEmbeddedTexture PageOf(TextureFit.Placement p) => p.Space.Page ?? throw new InvalidOperationException("The texture page was not made.");

    private static Box RectOf(GameMakerTexturePageItem item) => new(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight);

    /// <summary>True when another texture page item points at the same rectangle of the same page, so writing the item's pixels in place would change it too.</summary>
    internal static bool Shared(GameMakerData data, GameMakerTexturePageItem item) =>
        data.TexturePageItems.Any(o => o is not null && !ReferenceEquals(o, item) && ReferenceEquals(o.TexturePage, item.TexturePage) && RectOf(o) == RectOf(item));

    /// <summary>True when the box of the item must not shrink, as for a frame of a sprite with other frames (they keep the sprite's size).</summary>
    protected virtual bool KeepsBox(GameMakerData data) => false;

    // The item takes its new rectangle and size. A frame that filled its box (no trimmed margin) keeps filling it, so its box follows the new size, unless the box
    // must not shrink; a trimmed one keeps its offset, and its box grows to hold it.
    private static void Set(GameMakerTexturePageItem item, GameMakerEmbeddedTexture page, Box rect, GameMakerTexturePageItem from, bool movedOnly, bool keepsBox)
    {
        var filled = !keepsBox && from.TargetX == 0 && from.TargetY == 0 && from.BoundingWidth == from.TargetWidth && from.BoundingHeight == from.TargetHeight;
        item.TexturePage = page;
        (item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight) = ((ushort)rect.X, (ushort)rect.Y, (ushort)rect.Width, (ushort)rect.Height);
        if (movedOnly) return;
        (item.TargetWidth, item.TargetHeight) = ((ushort)rect.Width, (ushort)rect.Height);
        (item.BoundingWidth, item.BoundingHeight) = filled
            ? ((ushort)rect.Width, (ushort)rect.Height)
            : ((ushort)Math.Max(from.BoundingWidth, item.TargetX + rect.Width), (ushort)Math.Max(from.BoundingHeight, item.TargetY + rect.Height));
    }

    private static GameMakerEmbeddedTexture NewPage(GameMakerData data, TextureFit.Space space, GameMakerEmbeddedTexture like)
    {
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = new G3MLib.DataFile.Util.GMImage(space.Width, space.Height) },
            IndexInGroup = space.Group?.TexturePages.Count ?? 0,
            TextureWidth = space.Width,
            TextureHeight = space.Height,
            Scaled = like.Scaled,
            GeneratedMips = like.GeneratedMips,
            Name = new GameMakerString(space.Name),
        };
        if (space.Group is not null) page.TextureInfo = space.Group;
        PagePixels.Remember(page, like);
        data.EmbeddedTextures.Add(page);
        space.Group?.TexturePages.Add(new(page));
        return page;
    }

    /// <summary>The pages, page sizes, lists and item rectangles an edit can change, by reference: restoring them is the undo (or the redo).</summary>
    private sealed class State
    {
        private readonly List<(GameMakerEmbeddedTexture Page, G3MLib.DataFile.Util.GMImage Image, int Width, int Height)> _pages = [];
        private readonly List<(GameMakerTexturePageItem Item, GameMakerEmbeddedTexture Page, ushort[] Fields)> _items = [];

        public static State Of(GameMakerData data, IEnumerable<GameMakerTexturePageItem> items)
        {
            var state = new State();
            foreach (var page in data.EmbeddedTextures.Where(p => p.TextureData?.Image is not null)) state._pages.Add((page, page.TextureData.Image, page.TextureWidth, page.TextureHeight));
            foreach (var item in items.Distinct())
                state._items.Add((item, item.TexturePage, [item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight, item.TargetX, item.TargetY, item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight]));
            return state;
        }

        /// <summary>Puts the saved state back. Pages and items the edit made are in the data file only in the state after it.</summary>
        public void Restore(GameMakerData data, List<GameMakerTexturePageItem> madeItems, bool after)
        {
            var created = _pages.Select(p => p.Page).Where(p => !data.EmbeddedTextures.Contains(p)).ToList();
            if (after)
            {
                foreach (var page in created)
                {
                    data.EmbeddedTextures.Add(page);
                    page.TextureInfo?.TexturePages.Add(new(page));
                }

                foreach (var item in madeItems.Where(i => !data.TexturePageItems.Contains(i))) data.TexturePageItems.Add(item);
            }
            else
            {
                foreach (var page in data.EmbeddedTextures.Where(p => !_pages.Any(s => s.Page == p)).ToList())
                {
                    data.EmbeddedTextures.Remove(page);
                    if (page.TextureInfo?.TexturePages is { } list && list.FirstOrDefault(r => ReferenceEquals(r.Resource, page)) is { } entry) list.Remove(entry);
                }

                foreach (var item in madeItems) data.TexturePageItems.Remove(item);
            }

            foreach (var (page, image, width, height) in _pages)
            {
                (page.TextureData.Image, page.TextureWidth, page.TextureHeight) = (image, width, height);
                if (image.Format == G3MLib.DataFile.Util.GMImage.ImageFormat.RawBgra) PagePixels.Touch(page);
            }
            foreach (var (item, page, f) in _items)
            {
                item.TexturePage = page;
                (item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight, item.TargetX, item.TargetY, item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) = (f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9]);
            }
        }
    }
}

/// <summary>
/// Puts a picture of any size in place of a sprite frame or of the picture of a font, tile set, background, embedded image or texture page item, with <see cref="TextureFit"/>
/// finding the room. A frame whose item other frames share gets an item of its own, so they keep their pixels. The picture is kept in the journal.
/// </summary>
public sealed class TextureFitCommand(string type, string name, int frame, byte[] picture, FitOptions? fit = null) : TextureFitEdit
{
    public const string Name = "textureFit";

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["type"] = type,
        ["name"] = name,
        ["frame"] = frame.ToString(CultureInfo.InvariantCulture),
        ["picture"] = Convert.ToBase64String(picture),
    }.With(fit);

    protected override FitOptions? Fit => fit;

    public override IReadOnlyList<ProjectTouch>? Touches => [new(type switch
    {
        "sprites" => "Sprites",
        "fonts" => "Fonts",
        "tileSets" => "Backgrounds",
        "embeddedImages" => "EmbeddedImages",
        _ => "TexturePageItems",
    }, name), new("EmbeddedTextures", null)];

    public static TextureFitCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["type"], a["name"], int.Parse(a["frame"], CultureInfo.InvariantCulture), Convert.FromBase64String(a["picture"]), FitOptions.From(a));

    private PixelBuffer? _pixels;

    /// <summary>The texture page item of a sprite frame (counted from 0), or of the named resource of any other type.</summary>
    public static GameMakerTexturePageItem ItemOf(GameMakerData data, string type, string name, int frame) => type == "sprites"
        ? (data.Sprites.FirstOrDefault(s => s.Name.Content == name) ?? throw new InvalidOperationException($"Sprite '{name}' does not exist.")).Textures.ElementAtOrDefault(frame)?.Texture
          ?? throw new InvalidOperationException($"Sprite '{name}' has no frame {frame}.")
        : ReplaceItemCommand.ItemOf(data, type, name);

    /// <summary>The request for a picture of the given size for this item: a frame that shares its item asks for a new one.</summary>
    public static FitRequest RequestFor(GameMakerData data, string type, GameMakerTexturePageItem item, int width, int height) =>
        type == "sprites" && data.Sprites.SelectMany(s => s.Textures).Count(t => ReferenceEquals(t.Texture, item)) > 1
            ? new FitRequest(null, width, height, item)
            : new FitRequest(item, width, height);

    private FitRequest? _request;

    protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data)
    {
        _pixels ??= FramePicture.FromBytes(picture);
        _request ??= RequestFor(data, type, ItemOf(data, type, name, frame), _pixels.Width, _pixels.Height);
        return ([_request], [_pixels], null, default);
    }

    protected override bool KeepsBox(GameMakerData data) => type == "sprites" && data.Sprites.First(s => s.Name.Content == name).Textures.Count > 1;

    protected override void Bind(GameMakerData data, int index, GameMakerTexturePageItem item)
    {
        if (type == "sprites") data.Sprites.First(s => s.Name.Content == name).Textures[frame].Texture = item;
    }
}

/// <summary>
/// One canvas change or scale of several frames of a sprite, placed as one edit: each page is written once instead of once a frame, and frames that share a
/// picture keep sharing it. A picture other sprites also show gets a new item for these frames. The frames' pixels are worked out from the operation, so the
/// journal keeps the operation only.
/// </summary>
public sealed class FramesFitCommand(string sprite, IReadOnlyList<int> frames, ImageOp op, FitOptions? fit = null) : TextureFitEdit
{
    public const string Name = "framesFit";

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["frames"] = string.Join(',', frames.Select(f => f.ToString(CultureInfo.InvariantCulture))),
        ["op"] = op.ToString(),
    }.With(fit);

    protected override FitOptions? Fit => fit;

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sprites", sprite), new("EmbeddedTextures", null)];

    public static FramesFitCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sprite"], [.. a["frames"].Split(',').Select(f => int.Parse(f, CultureInfo.InvariantCulture))], ImageOp.Parse(a["op"]), FitOptions.From(a));

    private List<List<int>> _bound = [];

    /// <summary>One request per distinct picture of the frames, and the frames each request is for; the dialog plans with the same requests.</summary>
    public static (List<FitRequest> Requests, List<List<int>> Frames) Requests(GameMakerData data, string sprite, IReadOnlyList<int> frames, int width, int height)
    {
        var found = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        var byItem = new Dictionary<GameMakerTexturePageItem, List<int>>(ReferenceEqualityComparer.Instance);
        var order = new List<GameMakerTexturePageItem>();
        foreach (var f in frames)
        {
            var item = found.Textures.ElementAtOrDefault(f)?.Texture ?? throw new InvalidOperationException($"Sprite '{sprite}' has no frame {f}.");
            if (!byItem.TryGetValue(item, out var list))
            {
                byItem[item] = list = [];
                order.Add(item);
            }

            list.Add(f);
        }

        var shown = data.Sprites.SelectMany(s => s.Textures).Select(t => t.Texture).ToList();
        // a picture shown by more than these frames stays for the others, and these frames take a new one
        return ([.. order.Select(i => shown.Count(t => ReferenceEquals(t, i)) > byItem[i].Count ? new FitRequest(null, width, height, i) : new FitRequest(i, width, height))], [.. order.Select(i => byItem[i])]);
    }

    protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data)
    {
        var pictures = new List<PixelBuffer?>();
        var requests = new List<FitRequest>();
        var (all, bound) = Requests(data, sprite, frames, 1, 1);
        foreach (var request in all)
        {
            var picture = ImageEditCommand.Run(FramePicture.ReadItem(request.Origin, sprite), null, [op]);
            pictures.Add(picture);
            requests.Add(request with { Width = picture.Width, Height = picture.Height });
        }

        _bound = bound;
        return (requests, pictures, null, default);
    }

    protected override bool KeepsBox(GameMakerData data) => data.Sprites.First(s => s.Name.Content == sprite).Textures.Count > 1;

    protected override void Bind(GameMakerData data, int index, GameMakerTexturePageItem item)
    {
        var found = data.Sprites.First(s => s.Name.Content == sprite);
        foreach (var f in _bound[index]) found.Textures[f].Texture = item;
    }
}

/// <summary>
/// Gives a texture page a new size. A larger page keeps every picture where it is; a smaller one moves the pictures that no longer lie on it to free space on it,
/// to the page packed again, to another page of its group or to a new page, with <see cref="TextureFit"/>, and every item that moves keeps its references.
/// </summary>
public sealed class PageResizeCommand(string page, int width, int height, FitOptions? fit = null) : TextureFitEdit
{
    public const string Name = "pageResize";

    public override string Kind => Name;

    public override string Editor => "embeddedTextures";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["page"] = page,
        ["width"] = width.ToString(CultureInfo.InvariantCulture),
        ["height"] = height.ToString(CultureInfo.InvariantCulture),
    }.With(fit);

    protected override FitOptions? Fit => fit;

    public override IReadOnlyList<ProjectTouch>? Touches => [new("EmbeddedTextures", null)];

    public static PageResizeCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["page"], int.Parse(a["width"], CultureInfo.InvariantCulture), int.Parse(a["height"], CultureInfo.InvariantCulture), FitOptions.From(a));

    public static GameMakerEmbeddedTexture Find(GameMakerData data, string page) =>
        data.EmbeddedTextures.FirstOrDefault(t => t.Name?.Content == page) ?? throw new InvalidOperationException($"Texture page '{page}' does not exist.");

    protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data)
    {
        if (width is < 1 or > TextureFit.MaxPageSide || height is < 1 or > TextureFit.MaxPageSide)
            throw new InvalidDataException($"A texture page side is 1 to {TextureFit.MaxPageSide} pixels.");
        return ([], [], Find(data, page), (width, height));
    }
}
