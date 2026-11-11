#pragma warning disable CA2000 // model objects and bitmaps made for a lookup only
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class ThumbnailTests
{
    private static GameMakerString Name(string text) => new(text);

    /// <summary>One of each picture source on a red 64 x 64 page: sprite, tile set, font, objects with and without a sprite, page item, path, curve and a room that holds an instance.</summary>
    internal static Workspace Generate()
    {
        var data = GameMakerData.CreateNew2026LTS();
        using (var canvas = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 64, 64))
            data.EmbeddedTextures.Add(new GameMakerEmbeddedTexture
            {
                TextureData = new GameMakerEmbeddedTexture.TexData { Image = G3MLib.DataFile.Util.GMImage.FromMagickImage(canvas).ConvertToPng() },
                TextureWidth = 64,
                TextureHeight = 64,
                Name = Name("Texture 0"),
            });

        GameMakerTexturePageItem Item(string name, ushort x, ushort y, ushort size)
        {
            var item = new GameMakerTexturePageItem { Name = Name(name), SourceX = x, SourceY = y, SourceWidth = size, SourceHeight = size, TargetWidth = size, TargetHeight = size, BoundingWidth = size, BoundingHeight = size, TexturePage = data.EmbeddedTextures[0] };
            data.TexturePageItems.Add(item);
            return item;
        }

        var sprite = new GameMakerSprite { Name = Name("spr_a") };
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = Item("frame", 0, 0, 32) });
        data.Sprites.Add(sprite);
        data.Backgrounds.Add(new GameMakerBackground { Name = Name("ts_a"), Texture = Item("tiles", 0, 0, 16) });
        var font = new GameMakerFont { Name = Name("fnt_a"), Texture = Item("glyphs", 0, 0, 48) };
        foreach (var (c, x) in new[] { ('A', 0), ('g', 16) }) font.Glyphs.Add(new GameMakerFont.Glyph { Character = c, SourceX = (ushort)x, SourceY = 0, SourceWidth = 12, SourceHeight = 16 });
        data.Fonts.Add(font);
        var parent = new GameMakerGameObject { Name = Name("obj_parent"), Sprite = sprite };
        data.GameObjects.Add(parent);
        data.GameObjects.Add(new GameMakerGameObject { Name = Name("obj_child"), ParentId = parent });
        data.GameObjects.Add(new GameMakerGameObject { Name = Name("obj_bare") });
        var path = new GameMakerPath { Name = Name("pth_a") };
        path.Points.Add(new GameMakerPath.PathPoint { X = 0, Y = 0 });
        path.Points.Add(new GameMakerPath.PathPoint { X = 100, Y = 50 });
        data.Paths.Add(path);
        var curve = new GameMakerAnimationCurve { Name = Name("curve_a"), Channels = [] };
        var channel = new GameMakerAnimationCurve.Channel { Name = Name("c"), Points = [] };
        channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = 0, Value = 0 });
        channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = 1, Value = 1 });
        curve.Channels.Add(channel);
        data.AnimationCurves.Add(curve);
        var room = new GameMakerRoom { Name = Name("rm_a"), Width = 320, Height = 240, BackgroundColor = 0x00202020 };
        room.GameObjects.Add(new GameMakerRoom.GameObject { ObjectDefinition = parent, X = 40, Y = 40 });
        data.Rooms.Add(room);
        data.Scripts.Add(new GameMakerScript { Name = Name("scr_a") });
        return new Workspace("t.win", new DataFileIdentity("t.win", 0, ""), data, []);
    }

    internal static ThumbnailService Shared(Workspace ws, TextureBitmaps textures, Func<bool>? low = null) => new(() => ws, () => textures, low ?? (() => false));

    internal static string Key(string type, string name) => ResourceTypes.KeyOf(type, name);

    private static bool HasColour(IImage picture, Func<Color, bool> match)
    {
        var bitmap = (RenderTargetBitmap)picture;
        var size = bitmap.PixelSize;
        var pixels = new byte[size.Width * size.Height * 4];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), pin.AddrOfPinnedObject(), pixels.Length, size.Width * 4); }
        finally { pin.Free(); }

        for (var i = 0; i < pixels.Length; i += 4)
            // The byte order is the platform's (BGRA or RGBA), so both readings are tried.
            if (match(Color.FromArgb(pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i])) || match(Color.FromArgb(pixels[i + 3], pixels[i], pixels[i + 1], pixels[i + 2]))) return true;
        return false;
    }

    [Fact]
    public async Task EachPictureSourceMakesAPictureAndOthersMakeNone()
    {
        await HeadlessApp.Run(async () =>
        {
            using var ws = Generate();
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = Shared(ws, textures);
            foreach (var (type, name) in new[] { ("sprites", "spr_a"), ("tileSets", "ts_a"), ("fonts", "fnt_a"), ("objects", "obj_parent"), ("objects", "obj_child"), ("texturePageItems", "frame"), ("embeddedTextures", "Texture 0"), ("paths", "pth_a"), ("animationCurves", "curve_a"), ("rooms", "rm_a") })
            {
                var picture = await service.GetAsync(Key(type, name));
                Assert.True(picture is not null, $"{type}:{name} has no picture");
                Assert.InRange(picture.Size.Width, 1, ThumbnailService.Side);
                Assert.InRange(picture.Size.Height, 1, ThumbnailService.Side);
            }

            Assert.Null(await service.GetAsync(Key("objects", "obj_bare")));
            Assert.Null(await service.GetAsync(Key("scripts", "scr_a")));
            Assert.Null(await service.GetAsync(Key("strings", "nothing")));
            // The sprite shows its red frame, the room its red instance on the dark fill, the font its two glyphs cut from the sheet.
            Assert.True(HasColour((await service.GetAsync(Key("sprites", "spr_a")))!, c => c.R > 200 && c.G < 50));
            Assert.True(HasColour((await service.GetAsync(Key("rooms", "rm_a")))!, c => c.R > 200 && c.G < 50));
            Assert.Equal(24, (await service.GetAsync(Key("fonts", "fnt_a")))!.Size.Width);
            Assert.True(HasColour((await service.GetAsync(Key("paths", "pth_a")))!, c => c.A > 200));
        });
    }

    [Fact]
    public async Task APictureIsKeptUntilItsOwnResourceChanges()
    {
        await HeadlessApp.Run(async () =>
        {
            using var ws = Generate();
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = Shared(ws, textures);
            var (sprite, path) = (Key("sprites", "spr_a"), Key("paths", "pth_a"));
            var (first, line) = (await service.GetAsync(sprite), await service.GetAsync(path));
            Assert.True(service.TryGet(sprite, out var hit));
            Assert.Same(first, hit);

            // An edit elsewhere moves the version: the entry is stale, but made again it is the same picture.
            ws.History.MarkSaved();
            Assert.False(service.TryGet(sprite, out _));
            Assert.Same(first, await service.GetAsync(sprite));
            Assert.True(service.TryGet(sprite, out _));

            // An edit of the path itself makes a new picture, and the sprite stays.
            ws.Data.Paths.First(p => p.Name.Content == "pth_a").Points[1].Y = 400;
            ws.History.MarkSaved();
            Assert.NotSame(line, await service.GetAsync(path));
            Assert.Same(first, await service.GetAsync(sprite));
        });
    }

    [Fact]
    public async Task LowMemoryGivesNoPicturesAndClearForgetsThem()
    {
        await HeadlessApp.Run(async () =>
        {
            using var ws = Generate();
            using var textures = new TextureBitmaps(ws.Lock);
            var low = false;
            using var service = Shared(ws, textures, () => low);
            var sprite = Key("sprites", "spr_a");
            Assert.NotNull(await service.GetAsync(sprite));
            Assert.Equal(1, service.Cached);
            service.Clear();
            Assert.Equal(0, service.Cached);
            low = true;
            Assert.Null(await service.GetAsync(sprite));
            Assert.False(service.TryGet(sprite, out _));
        });
    }

    [Fact]
    public async Task ARowThatScrolledAwayStopsTheWork()
    {
        await HeadlessApp.Run(async () =>
        {
            using var ws = Generate();
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = Shared(ws, textures);
            using var gone = new CancellationTokenSource();
            await gone.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(Key("rooms", "rm_a"), gone.Token));
            // Nobody waited for it, so it was dropped before drawing and a later ask still works.
            Assert.NotNull(await service.GetAsync(Key("rooms", "rm_a")));
        });
    }

    [Fact]
    public async Task ARoomOverTheBudgetStillGivesAPicture()
    {
        await HeadlessApp.Run(() =>
        {
            using var scene = RoomScene.Synthetic(200, 200);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var picture = ThumbnailDrawing.DrawRoom(scene, clock);
            Assert.NotNull(picture);
            Assert.True(clock.Elapsed < ThumbnailService.RoomBudget + TimeSpan.FromSeconds(3));
            Assert.InRange(picture.Size.Width, 1, ThumbnailService.Side);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TheCacheKeepsTheMostRecentAndDropsTheOldest()
    {
        var cache = new ThumbnailCache(2);
        cache.Put("a", new(1, 10, null));
        cache.Put("b", new(1, 20, null));
        Assert.True(cache.TryGet("a", out var a)); // a is used now, so b is the oldest
        Assert.Equal(10, a.Signature);
        cache.Put("c", new(1, 30, null));
        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        cache.Put("a", new(2, 11, null)); // an entry is replaced, not added
        Assert.Equal(2, cache.Count);
        Assert.Equal(2, cache.TryGet("a", out var again) ? again.Version : 0);
        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void TheFingerprintChangesWithThePictureOnly()
    {
        using var ws = Generate();
        var sprite = ws.Data.Sprites[0];
        var before = ResourceThumbnails.Signature(sprite);
        ws.Data.Sprites[0].Name = Name("renamed");
        Assert.Equal(before, ResourceThumbnails.Signature(sprite));
        sprite.Textures[0].Texture!.SourceX = 8;
        Assert.NotEqual(before, ResourceThumbnails.Signature(sprite));
        var room = ws.Data.Rooms.First(r => r.Name.Content == "rm_a");
        var roomBefore = ResourceThumbnails.Signature(room);
        room.GameObjects[0].X += 10;
        Assert.NotEqual(roomBefore, ResourceThumbnails.Signature(room));
    }

    [Fact]
    public async Task EveryResourceTypeHasAPictureSourceOrItsOwnIcon()
    {
        await HeadlessApp.Run(() =>
        {
            Assert.All(ResourceTypes.All, type =>
            {
                Assert.True(ResourceIcons.Keys.Contains(type.Key), $"no icon for {type.Key}");
                Assert.NotNull(ResourceIcons.Of(type.Key, Colors.Gray));
            });
            Assert.All(ResourceThumbnails.PictureTypes, type => Assert.Contains(ResourceTypes.All, t => t.Key == type));
            Assert.Equal(ResourceIcons.Keys.Count(), ResourceIcons.Keys.Select(ResourceIcons.DataOf).Distinct().Count()); // one icon of its own per type
            Assert.Same(ResourceIcons.Of("sprites", Colors.Gray), ResourceIcons.Of("sprites", Colors.Gray));
            return Task.CompletedTask;
        });
    }
}
