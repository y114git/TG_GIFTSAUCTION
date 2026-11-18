using System.Diagnostics;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MLib.Modding.Integrity;
using G3MStudio.Core.Workspace;

#pragma warning disable CA2000, CA5394 // the data model owns what is added to it; the random numbers only pick test cases

namespace G3MStudio.Tests;

/// <summary>
/// Random files of one to four extra pages (with and without texture groups) full of sprite frames, shared frames, a font sheet and a tile set, and random
/// sequences of canvas changes, crops, trims, scales, frame resizes, page resizes and repacks. After every step each picture reads back as expected and lies inside
/// its page without overlapping another; after the sequence the file writes, reads back and passes the integrity check, undo gives the first page pixels back
/// byte for byte, redo gives the result again, and the journal replays to the same pages.
/// Files with groups use the non-LTS branch: G3MLib 1.1.3 writes a group of a 2026 LTS file with the spine sprite list, but reads any 2026 file as non-LTS,
/// without that list, so a written LTS file with a group does not read back ("Reading misaligned" in TGIN). Font glyphs are left out of the written file for a
/// similar reason (see <see cref="Written"/>).
/// </summary>
public sealed class TextureFuzzTests
{
    private const int Runs = 300;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private sealed class World(GameMakerData data, Random random)
    {
        public GameMakerData Data { get; } = data;
        public Random Random { get; } = random;
        public EditHistory History { get; set; } = new(data);
        public Dictionary<string, PixelBuffer> Expected { get; } = [];
        public List<string> Log { get; } = [];
    }

    [Fact]
    public void RandomResizesKeepEveryPictureAndUndoRedoAndTheJournalAgree()
    {
        var watch = Stopwatch.StartNew();
        var runs = 0;
        for (var seed = 0; seed < Runs && watch.Elapsed < Budget; seed++, runs++) Run(seed);
        Assert.True(runs >= Runs / 4, $"only {runs} runs in {Budget.TotalSeconds} s");
    }

    private static void Run(int seed)
    {
        using var data = Build(seed);
        var world = new World(data, new Random(seed * 7919 + 1));
        foreach (var key in Pictures(data)) world.Expected[key] = Read(data, key);
        try
        {
            Check(world);
            var start = Snapshot(data);
            var journal = new List<(string Kind, IReadOnlyDictionary<string, string> Args)?>(); // null is a repack
            var steps = world.Random.Next(4, 12);
            for (var i = 0; i < steps; i++)
            {
                if (world.Random.Next(12) == 0)
                {
                    Repack(world);
                    journal.Add(null);
                    start = Snapshot(data); // a repack cannot be undone: undo goes back to the repacked file
                    continue;
                }

                if (Step(world) is { } command) journal.Add((command.Kind, command.Args));
                Check(world);
            }

            using (var back = GameMakerIO.Read(new MemoryStream(Written(data))))
            {
                var check = DataIntegrityService.ValidateOnly(back);
                Assert.True(check.Success, string.Join("; ", check.Errors));
                foreach (var (key, picture) in world.Expected) Assert.True(Same(picture, Read(back, key)), $"{key} differs after the file is read back");
            }

            var after = Snapshot(data);
            var done = world.History.Position;
            while (world.History.Position > 0) world.History.Undo();
            AssertSame(start, Snapshot(data), "undo");
            while (world.History.Position < done) world.History.Redo();
            AssertSame(after, Snapshot(data), "redo");

            using var fresh = Build(seed);
            var replay = new World(fresh, new Random(0));
            foreach (var entry in journal)
            {
                if (entry is not var (kind, args)) Repack(replay);
                else replay.History.Execute(EditCommand.Create(kind, args));
            }

            AssertSame(after, Snapshot(fresh), "journal replay");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new Xunit.Sdk.XunitException($"seed {seed}: {string.Join(" > ", world.Log)}\n{e}");
        }
    }

    // The file as written, with the glyphs of the fonts left out: G3MLib 1.1.3 does not read back the glyphs of a font made in a generated 2026 file (its FONT
    // version check reads past the chunk). The sheet itself is still written and compared.
    private static byte[] Written(GameMakerData data)
    {
        var glyphs = data.Fonts.Select(f => f.Glyphs.ToList()).ToList();
        foreach (var font in data.Fonts) font.Glyphs.Clear();
        try
        {
            return TestData.Bytes(data);
        }
        finally
        {
            foreach (var (font, list) in data.Fonts.Zip(glyphs))
                foreach (var glyph in list) font.Glyphs.Add(glyph);
        }
    }

    // The colour of a pixel says which picture it is and where in it, and a margin of clear pixels gives a trim something to cut.
    private static PixelBuffer Pattern(int id, int width, int height, int margin)
    {
        var picture = new PixelBuffer(width, height);
        for (var y = margin; y < height - margin; y++)
            for (var x = margin; x < width - margin; x++) picture.Set(x, y, new Rgba((byte)(id * 37), (byte)(x * 7 + id), (byte)(y * 11), 255));
        return picture;
    }

    private static GameMakerData Build(int seed)
    {
        var random = new Random(seed);
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var groups = new List<GameMakerTextureGroupInfo>();
        if (random.Next(2) == 0)
        {
            data.GeneralInfo.Branch = GameMakerGeneralInfo.BranchType.Post2022_0;
            for (var g = random.Next(1, 3); g > 0; g--)
            {
                var group = new GameMakerTextureGroupInfo { Name = data.Strings.MakeString($"tg_{g}") };
                data.TextureGroupInfo.Add(group);
                groups.Add(group);
            }

            foreach (var page in data.EmbeddedTextures) Join(page, groups[random.Next(groups.Count)]);
        }

        int[] sides = [64, 96, 128];
        var pages = new List<GameMakerEmbeddedTexture>();
        for (var p = random.Next(1, 5); p > 0; p--)
        {
            var (w, h) = (sides[random.Next(sides.Length)], sides[random.Next(sides.Length)]);
            var page = new GameMakerEmbeddedTexture
            {
                TextureData = new GameMakerEmbeddedTexture.TexData { Image = new GMImage(w, h).ConvertToPng() },
                TextureWidth = w,
                TextureHeight = h,
                Name = new GameMakerString($"Texture {data.EmbeddedTextures.Count}"),
            };
            data.EmbeddedTextures.Add(page);
            if (groups.Count > 0) Join(page, groups[random.Next(groups.Count)]);
            pages.Add(page);
        }

        var id = 1;
        GameMakerTexturePageItem? Put(int width, int height, int margin)
        {
            for (var tries = 0; tries < 40; tries++)
            {
                var page = pages[random.Next(pages.Count)];
                if (width > page.TextureWidth || height > page.TextureHeight) continue;
                var at = new Box(random.Next(page.TextureWidth - width + 1), random.Next(page.TextureHeight - height + 1), width, height);
                if (data.TexturePageItems.Any(i => i.TexturePage == page && Overlap(RectOf(i), at))) continue;
                PagePixels.Write(page, [(at.X, at.Y, Pattern(id++, width, height, margin))]);
                var item = new GameMakerTexturePageItem { TexturePage = page, SourceX = (ushort)at.X, SourceY = (ushort)at.Y, SourceWidth = (ushort)width, SourceHeight = (ushort)height };
                (item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) = ((ushort)width, (ushort)height, (ushort)width, (ushort)height);
                data.TexturePageItems.Add(item);
                return item;
            }

            return null;
        }

        for (var s = random.Next(4, 14); s > 0; s--)
        {
            var name = $"spr_f{s}";
            history.Execute(ResourceTemplates.New(data, "sprites", name));
            var sprite = data.Sprites.First(x => x.Name.Content == name);
            var template = sprite.Textures[0].Texture!;
            SpriteTemplate.RemoveItem(data, template);
            sprite.Textures.Clear();
            var (w, h) = (random.Next(4, 44), random.Next(4, 44));
            for (var f = random.Next(1, 4); f > 0; f--)
            {
                if (sprite.Textures.Count > 0 && random.Next(3) == 0)
                {
                    sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = sprite.Textures[^1].Texture }); // a frame that shares the picture before it
                    continue;
                }

                if (Put(w, h, random.Next(3)) is { } item) sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
            }

            if (sprite.Textures.Count == 0) sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = Put(4, 4, 0) ?? template });
            (sprite.Width, sprite.Height) = (sprite.Textures[0].Texture!.BoundingWidth, sprite.Textures[0].Texture!.BoundingHeight);
            if (!data.TexturePageItems.Contains(sprite.Textures[0].Texture!))
            {
                data.TexturePageItems.Add(template);
                (template.TexturePage, template.SourceX, template.SourceY) = (data.EmbeddedTextures[0], 0, 0); // the empty first page holds a fallback frame
            }
        }

        if (Put(24, 16, 0) is { } sheet)
        {
            history.Execute(ResourceTemplates.New(data, "fonts", "fnt_fuzz"));
            var font = data.Fonts.First(f => f.Name.Content == "fnt_fuzz");
            font.Texture = sheet;
            font.Glyphs.Add(new GameMakerFont.Glyph { Character = 32 });
            font.Glyphs.Add(new GameMakerFont.Glyph { Character = 65, SourceX = 4, SourceY = 4, SourceWidth = 6, SourceHeight = 8 });
        }

        if (Put(16, 16, 0) is { } tiles)
        {
            var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_fuzz", out _), Texture = tiles, GMS2TileWidth = 8, GMS2TileHeight = 8, GMS2TileColumns = 2, GMS2TileCount = 4, GMS2ItemsPerTileCount = 1 };
            for (var t = 0u; t < 4; t++) set.GMS2TileIds.Add(new GameMakerBackground.TileID { ID = t });
            data.Backgrounds.Add(set);
        }

        return data;
    }

    private static void Join(GameMakerEmbeddedTexture page, GameMakerTextureGroupInfo group)
    {
        page.TextureInfo = group;
        page.IndexInGroup = group.TexturePages.Count;
        group.TexturePages.Add(new(page));
    }

    // Every picture a resource shows: each sprite frame, the font sheet and the tile set.
    private static IEnumerable<string> Pictures(GameMakerData data) =>
    [
        .. data.Sprites.SelectMany(s => s.Textures.Select((t, f) => (s, t, f))).Where(x => x.t.Texture is not null).Select(x => $"sprites|{x.s.Name.Content}|{x.f}"),
        .. data.Fonts.Where(f => f.Texture is not null).Select(f => $"fonts|{f.Name.Content}|0"),
        .. data.Backgrounds.Where(b => b.Texture is not null).Select(b => $"tileSets|{b.Name.Content}|0"),
    ];

    private static PixelBuffer Read(GameMakerData data, string key)
    {
        var (type, name, frame) = Split(key);
        return type == "sprites" ? FramePicture.Read(data, name, frame) : FramePicture.ReadItem(ReplaceItemCommand.ItemOf(data, type, name), name);
    }

    private static (string Type, string Name, int Frame) Split(string key)
    {
        var parts = key.Split('|');
        return (parts[0], parts[1], int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Box RectOf(GameMakerTexturePageItem item) => new(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight);

    private static bool Overlap(Box a, Box b) => a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static bool Same(PixelBuffer a, PixelBuffer b) => a.Width == b.Width && a.Height == b.Height && a.Data.AsSpan().SequenceEqual(b.Data);

    // One random edit; null when the edit was refused (then nothing may have changed).
    private static EditCommand? Step(World world)
    {
        var (data, random) = (world.Data, world.Random);
        var keys = Pictures(data).ToList();
        var key = keys[random.Next(keys.Count)];
        var (type, name, frame) = Split(key);
        var picture = world.Expected[key];
        var kind = random.Next(7);
        EditCommand command;
        var changed = new Dictionary<string, PixelBuffer>();
        if (kind == 6)
        {
            var pages = data.EmbeddedTextures.Where(t => !t.TextureExternal && t.TextureData?.Image is not null).ToList();
            var page = pages[random.Next(pages.Count)];
            double[] factors = [0.5, 0.75, 1.25, 2];
            var (w, h) = (Math.Max(1, (int)(page.TextureWidth * factors[random.Next(4)])), Math.Max(1, (int)(page.TextureHeight * factors[random.Next(4)])));
            command = new PageResizeCommand(page.Name.Content!, w, h);
            world.Log.Add($"page {page.Name.Content} {page.TextureWidth}x{page.TextureHeight}>{w}x{h}");
        }
        else if (type != "sprites")
        {
            // a tile set keeps its grid only when it grows to the right and down; a font sheet moves its glyphs along (or refuses a cut)
            var (w, h) = (Math.Max(1, picture.Width + random.Next(type == "tileSets" ? 0 : -6, 12)), Math.Max(1, picture.Height + random.Next(type == "tileSets" ? 0 : -6, 12)));
            var (ax, ay) = type == "tileSets" ? (0, 0) : (random.Next(3), random.Next(3));
            var next = picture.Resized(w, h, ax, ay);
            world.Log.Add($"{key} canvas {w}x{h} at {ax},{ay}");
            try
            {
                command = new ItemTarget(type, name).Apply(data, next, true, ((w - picture.Width) * ax / 2, (h - picture.Height) * ay / 2));
            }
            catch (InvalidOperationException)
            {
                world.Log.Add("refused");
                return null;
            }

            changed[key] = next;
        }
        else
        {
            var sprite = data.Sprites.First(s => s.Name.Content == name);
            var (w, h) = (Math.Max(1, picture.Width + random.Next(-8, 24)), Math.Max(1, picture.Height + random.Next(-8, 24)));
            var (ax, ay) = (random.Next(3), random.Next(3));
            PixelSelection? selection = null;
            ImageOp op;
            var all = false;
            switch (kind)
            {
                case 0 or 1:
                    all = random.Next(2) == 0;
                    op = new ResizeSpec(kind == 1, w, h, ax, ay).Op;
                    break;
                case 2:
                    var (x0, y0) = (random.Next(picture.Width), random.Next(picture.Height));
                    selection = PixelSelection.Rectangle(picture.Width, picture.Height, x0, y0, random.Next(x0, picture.Width), random.Next(y0, picture.Height));
                    op = new ImageOp(ImageOps.Crop);
                    break;
                case 3:
                    op = new ImageOp(ImageOps.Trim);
                    break;
                default:
                    op = new ResizeSpec(false, w, h, ax, ay).Op; // one frame, maybe one that shares its picture
                    break;
            }

            world.Log.Add($"{key} {op}{(all ? " all" : "")}");
            command = kind is 0 or 1 ? SpriteResize.Build(data, name, frame, new ResizeSpec(kind == 1, w, h, ax, ay), all, null) : new ImageEditCommand(name, frame, selection, [op]);
            foreach (var f in all ? Enumerable.Range(0, sprite.Textures.Count) : [frame])
            {
                var k = $"sprites|{name}|{f}";
                changed[k] = ImageEditCommand.Run(world.Expected[k], f == frame ? selection : null, [op]);
            }
        }

        try
        {
            world.History.Execute(command);
        }
        catch (InvalidOperationException e)
        {
            world.Log.Add($"refused: {e.Message}");
            return null;
        }
        catch (InvalidDataException e)
        {
            world.Log.Add($"refused: {e.Message}");
            return null;
        }

        foreach (var (k, p) in changed) world.Expected[k] = p;
        return command;
    }

    private static void Repack(World world)
    {
        world.Log.Add("repack");
        PagePixels.EncodeAll(world.Data);
        var repacker = new TexturePageRepacker(world.Data);
        repacker.Apply(repacker.Plan(new RepackOptions()));
        world.History = new EditHistory(world.Data);
        Check(world);
    }

    // Every item lies on its page, no two different rectangles of a page overlap, and every picture is as expected.
    private static void Check(World world)
    {
        var data = world.Data;
        foreach (var page in data.EmbeddedTextures.Where(t => t.TextureData?.Image is not null))
        {
            Assert.Equal((page.TextureWidth, page.TextureHeight), (page.TextureData.Image.Width, page.TextureData.Image.Height));
            var rects = data.TexturePageItems.Where(i => i.TexturePage == page).Select(RectOf).Distinct().ToList();
            foreach (var r in rects) Assert.True(r.X + r.Width <= page.TextureWidth && r.Y + r.Height <= page.TextureHeight, $"{r} lies outside {page.Name.Content}");
            for (var i = 0; i < rects.Count; i++)
                for (var j = i + 1; j < rects.Count; j++) Assert.False(Overlap(rects[i], rects[j]), $"{rects[i]} overlaps {rects[j]} on {page.Name.Content}");
        }

        foreach (var (key, picture) in world.Expected) Assert.True(Same(picture, Read(data, key)), $"{key} differs");
    }

    private sealed record State(List<byte[]> Pages, List<string> Items);

    private static State Snapshot(GameMakerData data) => new(
        [.. data.EmbeddedTextures.Where(t => t.TextureData?.Image is not null).Select(t => PagePixels.Read(t, 0, 0, t.TextureWidth, t.TextureHeight).Data)],
        [.. data.TexturePageItems.Select(i => $"{data.EmbeddedTextures.IndexOf(i.TexturePage)}:{RectOf(i)}:{i.TargetX},{i.TargetY},{i.TargetWidth},{i.TargetHeight},{i.BoundingWidth},{i.BoundingHeight}")]);

    private static void AssertSame(State expected, State actual, string what)
    {
        Assert.True(expected.Pages.Count == actual.Pages.Count, $"{what}: {actual.Pages.Count} pages, expected {expected.Pages.Count}");
        for (var i = 0; i < expected.Pages.Count; i++) Assert.True(expected.Pages[i].AsSpan().SequenceEqual(actual.Pages[i]), $"{what}: page {i} differs");
        Assert.Equal(expected.Items, actual.Items);
    }
}
