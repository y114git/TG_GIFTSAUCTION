using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Integrity;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// A picture of a new size finds its room on the texture pages by itself: its own place, free space, the page packed again, the page grown, a new page only when
/// nothing else fits. Other pictures on the page keep their pixels (and their coordinates, or move with their item), the edit undoes and redoes as one step,
/// and the file writes, reads back and passes the integrity check.
/// </summary>
public sealed class TextureFitTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255), Blue = new(0, 0, 255, 255);

    private static GameMakerTexturePageItem Frame(GameMakerData data, string sprite) => data.Sprites.First(s => s.Name.Content == sprite).Textures[0].Texture!;

    private static PixelBuffer Solid(int width, int height, Rgba colour)
    {
        var picture = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) picture.Set(x, y, colour);
        return picture;
    }

    // spr_a (32 x 32, on a 64 x 64 page of its own) and spr_b (a blue square) moved onto the same page at the given place.
    private static (GameMakerData Data, EditHistory History) TwoOnOnePage(Box b)
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_b"));
        var (a, item) = (Frame(data, "spr_a"), Frame(data, "spr_b"));
        PagePixels.Write(a.TexturePage, [(b.X, b.Y, Solid(b.Width, b.Height, Blue))]);
        item.TexturePage = a.TexturePage;
        (item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight, item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) =
            ((ushort)b.X, (ushort)b.Y, (ushort)b.Width, (ushort)b.Height, (ushort)b.Width, (ushort)b.Height, (ushort)b.Width, (ushort)b.Height);
        return (data, new EditHistory(data));
    }

    private static TextureFitCommand Fit(string sprite, PixelBuffer picture) => new("sprites", sprite, 0, FramePicture.ToPng(picture));

    private static void AssertSolid(GameMakerData data, string sprite, int width, int height, Rgba colour)
    {
        var picture = FramePicture.Read(data, sprite, 0);
        Assert.Equal((width, height), (picture.Width, picture.Height));
        Assert.Equal(colour, picture.Get(0, 0));
        Assert.Equal(colour, picture.Get(width - 1, height - 1));
    }

    private static void AssertValidFile(GameMakerData data)
    {
        using var back = GameMakerIO.Read(new MemoryStream(TestData.Bytes(data)));
        Assert.Equal(data.EmbeddedTextures.Count, back.EmbeddedTextures.Count);
        foreach (var sprite in data.Sprites.Where(s => s.Textures.Count > 0).Select(s => s.Name.Content!))
            Assert.Equal(FramePicture.Read(data, sprite, 0).Data, FramePicture.Read(back, sprite, 0).Data);
        var check = DataIntegrityService.ValidateOnly(back);
        Assert.True(check.Success, string.Join("; ", check.Errors));
    }

    [Fact]
    public void ASmallerPictureStaysInItsPlace()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 0, 20, 20));
        using (data)
        {
            var a = Frame(data, "spr_a");
            var command = Fit("spr_a", Solid(20, 10, Red));
            history.Execute(command);
            Assert.Equal(FitKind.InPlace, Assert.Single(command.Steps).Kind);
            Assert.Equal((0, 0, 20, 10), (a.SourceX, a.SourceY, a.SourceWidth, a.SourceHeight));
            Assert.Equal((20, 10), (a.BoundingWidth, a.BoundingHeight));
            AssertSolid(data, "spr_a", 20, 10, Red);
            AssertSolid(data, "spr_b", 20, 20, Blue);
            AssertValidFile(data);
        }
    }

    [Fact]
    public void ALargerPictureTakesFreeSpaceAndTheOtherPictureKeepsItsPlace()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            var pages = data.EmbeddedTextures.Count;
            var command = Fit("spr_a", Solid(36, 30, Red));
            history.Execute(command);
            Assert.Equal(FitKind.FreeSpace, Assert.Single(command.Steps).Kind);
            Assert.Equal(pages, data.EmbeddedTextures.Count);
            Assert.Equal((40, 40), (Frame(data, "spr_b").SourceX, Frame(data, "spr_b").SourceY));
            AssertSolid(data, "spr_a", 36, 30, Red);
            AssertSolid(data, "spr_b", 20, 20, Blue);
            AssertValidFile(data);
        }
    }

    [Fact]
    public void ThePageIsPackedAgainWhenNoFreeAreaIsLargeEnoughAndTheMovedItemKeepsItsPixels()
    {
        var (data, history) = TwoOnOnePage(new Box(20, 20, 20, 20));
        using (data)
        {
            var b = Frame(data, "spr_b");
            var command = Fit("spr_a", Solid(60, 30, Red));
            history.Execute(command);
            var step = Assert.Single(command.Steps);
            Assert.Equal(FitKind.Repack, step.Kind);
            Assert.Equal(1, step.Moved);
            Assert.NotEqual((20, 20), (b.SourceX, b.SourceY));
            AssertSolid(data, "spr_a", 60, 30, Red);
            AssertSolid(data, "spr_b", 20, 20, Blue);
            AssertValidFile(data);
        }
    }

    [Fact]
    public void ThePageGrowsToTheNextPowerOfTwoAndUndoAndRedoPutEverythingBack()
    {
        var (data, history) = TwoOnOnePage(new Box(34, 0, 24, 24));
        using (data)
        {
            var a = Frame(data, "spr_a");
            var page = a.TexturePage;
            var before = (page.TextureData.Image, page.TextureWidth, a.SourceWidth, data.EmbeddedTextures.Count);
            var command = Fit("spr_a", Solid(100, 100, Red));
            history.Execute(command);
            var step = Assert.Single(command.Steps);
            Assert.Equal(FitKind.Grow, step.Kind);
            Assert.Equal(((64, 64), (128, 128)), (step.PageBefore, step.PageAfter));
            Assert.Same(page, a.TexturePage);
            Assert.Equal(128, page.TextureWidth);
            Assert.Equal((34, 0), (Frame(data, "spr_b").SourceX, Frame(data, "spr_b").SourceY));
            AssertSolid(data, "spr_a", 100, 100, Red);
            AssertSolid(data, "spr_b", 24, 24, Blue);
            AssertValidFile(data);

            history.Undo();
            Assert.Equal(before, (page.TextureData.Image, page.TextureWidth, a.SourceWidth, data.EmbeddedTextures.Count));
            AssertSolid(data, "spr_a", 32, 32, Rgba.Clear);
            AssertSolid(data, "spr_b", 24, 24, Blue);
            history.Redo();
            AssertSolid(data, "spr_a", 100, 100, Red);
            Assert.Equal(128, page.TextureWidth);
        }
    }

    // A fit whose last step throws, after the page grew and the new item was made.
    private sealed class FailingFit(GameMakerTexturePageItem template) : TextureFitEdit
    {
        public override string Kind => "failingFit";

        public override string Editor => "sprites";

        public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>();

        protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data) =>
            ([new FitRequest(null, 100, 100, template)], [Solid(100, 100, Red)], null, default);

        protected override void Bind(GameMakerData data, int index, GameMakerTexturePageItem item)
        {
            if (data.TexturePageItems.Contains(item) && !ReferenceEquals(item, template)) throw new InvalidOperationException("bind failed");
        }
    }

    [Fact]
    public void AFitThatThrowsHalfwayLeavesTheDataAsItWasAndRecordsNothing()
    {
        var (data, history) = TwoOnOnePage(new Box(34, 0, 24, 24));
        using (data)
        {
            var a = Frame(data, "spr_a");
            var page = a.TexturePage;
            var before = (page.TextureData.Image, page.TextureWidth, page.TextureHeight, a.SourceWidth, data.EmbeddedTextures.Count, data.TexturePageItems.Count);
            Assert.Throws<InvalidOperationException>(() => history.Execute(new FailingFit(a)));
            Assert.Equal(before, (page.TextureData.Image, page.TextureWidth, page.TextureHeight, a.SourceWidth, data.EmbeddedTextures.Count, data.TexturePageItems.Count));
            Assert.Equal(0, history.Position);
            AssertSolid(data, "spr_b", 24, 24, Blue);
        }
    }

    [Fact]
    public void ANewPageInTheSameGroupOnlyWhenNothingElseFitsAndUndoTakesItOut()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var a = Frame(data, "spr_a");
        var pages = data.EmbeddedTextures.Count;
        var command = Fit("spr_a", Solid(2100, 8, Red)); // wider than the largest page may grow (2048)
        history.Execute(command);
        var step = Assert.Single(command.Steps);
        Assert.Equal(FitKind.NewPage, step.Kind);
        Assert.Equal((4096, 4096), step.PageAfter);
        Assert.Equal(pages + 1, data.EmbeddedTextures.Count);
        Assert.Same(data.EmbeddedTextures[^1], a.TexturePage);
        AssertSolid(data, "spr_a", 2100, 8, Red);
        AssertValidFile(data);
        history.Undo();
        Assert.Equal(pages, data.EmbeddedTextures.Count);
        AssertSolid(data, "spr_a", 32, 32, Rgba.Clear);
    }

    [Fact]
    public void AFrameThatSharesItsPictureGetsItsOwnItemAndTheOtherFrameKeepsItsPixels()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        var shared = sprite.Textures[0].Texture;
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = shared });
        history.Execute(new TextureFitCommand("sprites", "spr_a", 1, FramePicture.ToPng(Solid(10, 10, Red))));
        Assert.Same(shared, sprite.Textures[0].Texture);
        Assert.NotSame(shared, sprite.Textures[1].Texture);
        Assert.Equal(Red, FramePicture.Read(data, "spr_a", 1).Get(5, 5));
        Assert.Equal(Rgba.Clear, FramePicture.Read(data, "spr_a", 0).Get(5, 5));
        var own = Assert.IsType<GameMakerTexturePageItem>(sprite.Textures[1].Texture);
        Assert.Equal((32, 32), (own.BoundingWidth, own.BoundingHeight)); // the frame keeps the sprite's size
        history.Undo();
        Assert.Same(shared, sprite.Textures[1].Texture);
    }

    [Fact]
    public void APageCutSmallerMovesThePicturesOutsideItAndAPageMadeLargerKeepsThemWhereTheyAre()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            var page = Frame(data, "spr_a").TexturePage;
            var name = page.Name.Content!;
            history.Execute(new PageResizeCommand(name, 128, 128));
            Assert.Equal((40, 40), (Frame(data, "spr_b").SourceX, Frame(data, "spr_b").SourceY));
            AssertSolid(data, "spr_b", 20, 20, Blue);
            history.Undo();

            var cut = new PageResizeCommand(name, 64, 36);
            history.Execute(cut);
            Assert.Equal(36, page.TextureHeight);
            var b = Frame(data, "spr_b");
            Assert.True(b.TexturePage != page || b.SourceY + b.SourceHeight <= 36);
            AssertSolid(data, "spr_b", 20, 20, Blue);
            AssertSolid(data, "spr_a", 32, 32, Rgba.Clear);
            AssertValidFile(data);
            history.Undo();
            Assert.Equal(64, page.TextureHeight);
            Assert.Equal((40, 40), (Frame(data, "spr_b").SourceX, Frame(data, "spr_b").SourceY));
        }
    }

    [Fact]
    public void APageCutSmallerNeverGrowsBackAndAPictureWithoutRoomGetsANewPageWithAFreeName()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            var page = Frame(data, "spr_a").TexturePage;
#pragma warning disable CA2000 // the data file owns its pages
            Page(data, $"Texture {data.EmbeddedTextures.Count + 1}", 64, null); // the name a new page would take by count
            // every other page is full, so spr_a has room only on a new page
            foreach (var other in data.EmbeddedTextures.Where(t => t != page).ToList())
                data.TexturePageItems.Add(new GameMakerTexturePageItem { TexturePage = other, SourceWidth = (ushort)other.TextureWidth, SourceHeight = (ushort)other.TextureHeight });
#pragma warning restore CA2000
            var cut = new PageResizeCommand(page.Name.Content!, 64, 24); // spr_a (32 x 32) no longer fits on the page at all
            history.Execute(cut);
            Assert.Equal((64, 24), (page.TextureWidth, page.TextureHeight));
            Assert.DoesNotContain(cut.Steps, s => s.Kind == FitKind.Grow);
            Assert.Contains(cut.Steps, s => s.Kind == FitKind.NewPage);
            Assert.Equal(data.EmbeddedTextures.Count, data.EmbeddedTextures.Select(t => t.Name.Content).Distinct().Count());
            Assert.NotSame(page, Frame(data, "spr_a").TexturePage);
            AssertSolid(data, "spr_a", 32, 32, Rgba.Clear);
            AssertSolid(data, "spr_b", 20, 20, Blue);
            AssertValidFile(data);
        }
    }

    // A sprite whose frame sits on the page at the given place, in a solid colour.
    private static GameMakerTexturePageItem On(GameMakerData data, EditHistory history, string sprite, GameMakerEmbeddedTexture page, Box at, Rgba colour)
    {
        history.Execute(ResourceTemplates.New(data, "sprites", sprite));
        var item = Frame(data, sprite);
        PagePixels.Write(page, [(at.X, at.Y, Solid(at.Width, at.Height, colour))]);
        item.TexturePage = page;
        (item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight, item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) =
            ((ushort)at.X, (ushort)at.Y, (ushort)at.Width, (ushort)at.Height, (ushort)at.Width, (ushort)at.Height, (ushort)at.Width, (ushort)at.Height);
        return item;
    }

    [Fact]
    public void PicturesCutOffAPageKeepTheirPixelsWhenALaterOneRepacksThePageAndSharedItemsMoveTogether()
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
#pragma warning disable CA2000 // the data file owns its pages
        var page = Page(data, "Texture cut", 128, null);
#pragma warning restore CA2000
        Rgba green = new(0, 255, 0, 255), yellow = new(255, 255, 0, 255);
        var kept = On(data, history, "spr_k", page, new Box(40, 0, 40, 20), green);
        var first = On(data, history, "spr_e1", page, new Box(0, 60, 30, 20), Red);
        var twin = On(data, history, "spr_e1b", page, new Box(0, 60, 30, 20), Red); // another item on the same rectangle
        On(data, history, "spr_e2", page, new Box(0, 90, 50, 20), yellow);

        // spr_e1 takes free space at the left; spr_e2 then fits only with the page packed again, which moves spr_k and the place spr_e1 just took.
        var cut = new PageResizeCommand("Texture cut", 128, 30);
        history.Execute(cut);
        Assert.Equal([FitKind.FreeSpace, FitKind.Repack], cut.Steps.Select(s => s.Kind));
        Assert.Equal(1, cut.Steps[1].Moved);
        Assert.Same(page, first.TexturePage);
        Assert.Equal((first.TexturePage, first.SourceX, first.SourceY), (twin.TexturePage, twin.SourceX, twin.SourceY));
        Assert.True(first.SourceY + first.SourceHeight <= 30 && kept.SourceY + kept.SourceHeight <= 30);
        AssertSolid(data, "spr_k", 40, 20, green);
        AssertSolid(data, "spr_e1", 30, 20, Red);
        AssertSolid(data, "spr_e1b", 30, 20, Red);
        AssertSolid(data, "spr_e2", 50, 20, yellow);
        AssertValidFile(data);
    }

    private static GameMakerEmbeddedTexture Page(GameMakerData data, string name, int side, GameMakerTextureGroupInfo? group)
    {
        var page = new GameMakerEmbeddedTexture
        {
            TextureData = new GameMakerEmbeddedTexture.TexData { Image = new G3MLib.DataFile.Util.GMImage(side, side) },
            TextureWidth = side,
            TextureHeight = side,
            Name = new GameMakerString(name),
        };
        data.EmbeddedTextures.Add(page);
        if (group is null) return page;
        page.TextureInfo = group;
        group.TexturePages.Add(new(page));
        return page;
    }

    // spr_a sits under a 2048 x 2000 block on a full 2048 x 2048 page of group "tg_home", so a 100 x 100 picture neither fits there, nor after packing, nor by
    // growing (2048 is the largest page). "tg_other" has an empty 256 x 256 page; with sameGroupRoom "tg_home" has one too.
    private static GameMakerData Groups(bool sameGroupRoom)
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_b"));
        var (home, other) = (new GameMakerTextureGroupInfo { Name = data.Strings.MakeString("tg_home") }, new GameMakerTextureGroupInfo { Name = data.Strings.MakeString("tg_other") });
        data.TextureGroupInfo.Add(home);
        data.TextureGroupInfo.Add(other);
#pragma warning disable CA2000 // the data file owns its pages
        var full = Page(data, "Texture full", 2048, home);
        Page(data, "Texture other", 256, other);
        if (sameGroupRoom) Page(data, "Texture spare", 256, home);
#pragma warning restore CA2000
        var (a, block) = (Frame(data, "spr_a"), Frame(data, "spr_b"));
        (block.TexturePage, block.SourceX, block.SourceY, block.SourceWidth, block.SourceHeight) = (full, 0, 0, 2048, 2000);
        (a.TexturePage, a.SourceX, a.SourceY) = (full, 0, 2004);
        return data;
    }

    [Fact]
    public void ABlockedPictureTakesFreeSpaceOnAnotherPageOfItsOwnGroupAndNeverOfAnother()
    {
        using var data = Groups(true);
        var command = Fit("spr_a", Solid(100, 100, Red));
        new EditHistory(data).Execute(command);
        var step = Assert.Single(command.Steps);
        Assert.Equal((FitKind.FreeSpace, "Texture spare"), (step.Kind, step.Page));
        var page = Frame(data, "spr_a").TexturePage;
        Assert.Equal("tg_home", page.TextureInfo.Name.Content);
        AssertSolid(data, "spr_a", 100, 100, Red);
    }

    [Fact]
    public void ANewPageForABlockedPictureJoinsItsOwnGroupAndUndoTakesItOutOfTheGroup()
    {
        using var data = Groups(false);
        var home = data.TextureGroupInfo.First(g => g.Name.Content == "tg_home");
        var other = data.TextureGroupInfo.First(g => g.Name.Content == "tg_other");
        var (pages, homePages, otherPages) = (data.EmbeddedTextures.Count, home.TexturePages.Count, other.TexturePages.Count);
        var history = new EditHistory(data);
        var command = Fit("spr_a", Solid(100, 100, Red));
        history.Execute(command);
        Assert.Equal(FitKind.NewPage, Assert.Single(command.Steps).Kind); // the free 256 x 256 page of tg_other is not used
        var page = Frame(data, "spr_a").TexturePage;
        Assert.Same(home, page.TextureInfo);
        Assert.Equal((pages + 1, homePages + 1, otherPages), (data.EmbeddedTextures.Count, home.TexturePages.Count, other.TexturePages.Count));
        Assert.Contains(home.TexturePages, r => ReferenceEquals(r.Resource, page));
        AssertSolid(data, "spr_a", 100, 100, Red);
        history.Undo();
        Assert.Equal((pages, homePages), (data.EmbeddedTextures.Count, home.TexturePages.Count));
        Assert.DoesNotContain(home.TexturePages, r => ReferenceEquals(r.Resource, page));
    }

    [Fact]
    public void TheEditReplaysFromTheJournalToTheSamePlaces()
    {
        var (data, history) = TwoOnOnePage(new Box(34, 0, 24, 24));
        using (data)
        {
            var command = Fit("spr_a", Solid(100, 100, Red));
            var plan = TextureFit.Plan(data, [new FitRequest(Frame(data, "spr_a"), 100, 100)]);
            Assert.Equal(FitKind.Grow, Assert.Single(plan).Kind); // the dialog's preview says what the edit then does
            history.Execute(command);
            var again = (TextureFitCommand)EditCommand.Create(command.Kind, command.Args);
            var (fresh, freshHistory) = TwoOnOnePage(new Box(34, 0, 24, 24));
            using (fresh)
            {
                freshHistory.Execute(again);
                Assert.Equal(command.Steps, again.Steps);
                Assert.Equal(FramePicture.Read(data, "spr_a", 0).Data, FramePicture.Read(fresh, "spr_a", 0).Data);
            }
        }
    }

    [Fact]
    public void TheEditOfAFontPictureTouchesTheFont()
    {
        var command = new TextureFitCommand("fonts", "fnt_a", 0, FramePicture.ToPng(Solid(4, 4, Red)));
        Assert.Equal(new ProjectTouch("Fonts", "fnt_a"), command.Touches?[0]);
    }

    [Fact]
    public void TwoPackingsInOneEditMoveEveryPictureToItsLastPlace()
    {
        // A full 64 x 64 page of sixteen pictures; the first and third new sizes each need the page packed again. The second packing puts pictures where others
        // were before it, and a move recorded by the first packing must follow its own picture only, not the one whose old place it lands on.
        using var data = CopyResourcesTests.Source();
#pragma warning disable CA2000 // the data file owns its pages and items
        var page = Page(data, "Texture full", 64, null);
        Box[] rects =
        [
            new(0, 48, 12, 12), new(0, 0, 10, 10), new(48, 48, 12, 12), new(48, 0, 12, 12), new(16, 32, 10, 10), new(16, 48, 12, 12), new(0, 32, 10, 10), new(32, 0, 10, 10),
            new(16, 16, 10, 10), new(48, 16, 10, 10), new(32, 32, 10, 10), new(0, 16, 12, 12), new(32, 48, 10, 10), new(32, 16, 10, 10), new(48, 32, 10, 10), new(16, 0, 10, 10),
        ];
        var items = rects.Select(r => new GameMakerTexturePageItem { TexturePage = page, SourceX = (ushort)r.X, SourceY = (ushort)r.Y, SourceWidth = (ushort)r.Width, SourceHeight = (ushort)r.Height }).ToList();
#pragma warning restore CA2000
        foreach (var item in items) data.TexturePageItems.Add(item);

        var (layout, placements, _) = TextureFit.Run(data, [new FitRequest(items[11], 18, 13), new FitRequest(items[0], 21, 13), new FitRequest(items[6], 19, 12)], null, default);

        Assert.Equal([FitKind.Repack, FitKind.FreeSpace, FitKind.Repack], placements.Select(p => p.Step.Kind));
        var final = layout.Spaces.SelectMany(s => s.Used).SelectMany(u => u.Items.Select(i => (Item: i, u.Rect))).ToDictionary(u => u.Item, u => u.Rect);
        var moves = placements.SelectMany(p => p.Moves).ToList();
        Assert.NotEmpty(moves);
        Assert.All(moves, m => Assert.Equal(final[m.Item], m.To));
        // A picture that gets its own new size later in the edit is placed by that request only: no earlier packing still moves it.
        Assert.DoesNotContain(moves, m => placements.Any(p => p.Items.Contains(m.Item)));
    }

    // Places pictures on bare page items, as the dialogs do for several frames at once.
    private sealed class BareFit(IReadOnlyList<FitRequest> requests, IReadOnlyList<PixelBuffer?> pictures) : TextureFitEdit
    {
        public override string Kind => "bareFit";

        public override string Editor => "texturePageItems";

        public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>();

        protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data) =>
            (requests, pictures, null, default);
    }

    [Fact]
    public void APictureMovedByAPackingAndResizedLaterInTheSameEditIsWrittenOnceAtItsLastPlace()
    {
        // As above: items[0] and items[6] are moved by the first packing and then get their own size. The old pixels the first move kept for them must not land
        // anywhere: every picture reads back (through a written and read file) as its own colour only.
        using var data = CopyResourcesTests.Source();
#pragma warning disable CA2000 // the data file owns its pages and items
        var page = Page(data, "Texture full", 64, null);
        Box[] rects =
        [
            new(0, 48, 12, 12), new(0, 0, 10, 10), new(48, 48, 12, 12), new(48, 0, 12, 12), new(16, 32, 10, 10), new(16, 48, 12, 12), new(0, 32, 10, 10), new(32, 0, 10, 10),
            new(16, 16, 10, 10), new(48, 16, 10, 10), new(32, 32, 10, 10), new(0, 16, 12, 12), new(32, 48, 10, 10), new(32, 16, 10, 10), new(48, 32, 10, 10), new(16, 0, 10, 10),
        ];
        var items = rects.Select(r => new GameMakerTexturePageItem { TexturePage = page, SourceX = (ushort)r.X, SourceY = (ushort)r.Y, SourceWidth = (ushort)r.Width, SourceHeight = (ushort)r.Height }).ToList();
#pragma warning restore CA2000
        static Rgba Colour(int i) => new((byte)(10 + i * 9), (byte)(250 - i * 7), (byte)(i * 13), 255);
        foreach (var (i, item) in items.Index())
        {
            (item.TargetWidth, item.TargetHeight, item.BoundingWidth, item.BoundingHeight) = (item.SourceWidth, item.SourceHeight, item.SourceWidth, item.SourceHeight);
            data.TexturePageItems.Add(item);
            PagePixels.Write(page, [(rects[i].X, rects[i].Y, Solid(rects[i].Width, rects[i].Height, Colour(i)))]);
        }

        (int Index, int W, int H)[] sizes = [(11, 18, 13), (0, 21, 13), (6, 19, 12)];
        var history = new EditHistory(data);
        history.Execute(new BareFit([.. sizes.Select(s => new FitRequest(items[s.Index], s.W, s.H))], [.. sizes.Select(s => Solid(s.W, s.H, Colour(20 + s.Index)))]));

        var first = data.TexturePageItems.IndexOf(items[0]);
        using var back = GameMakerIO.Read(new MemoryStream(TestData.Bytes(data)));
        foreach (var (i, item) in items.Index())
        {
            var read = back.TexturePageItems[first + i];
            var size = sizes.FirstOrDefault(s => s.Index == i);
            var (w, h, colour) = size.W > 0 ? (size.W, size.H, Colour(20 + i)) : (rects[i].Width, rects[i].Height, Colour(i));
            Assert.Equal((w, h), (read.SourceWidth, read.SourceHeight));
            var pixels = PagePixels.Read(read.TexturePage, read.SourceX, read.SourceY, w, h);
            for (var p = 0; p < w * h; p++) Assert.Equal(colour, pixels.Get(p % w, p / w));
        }

        // Nothing else is drawn on the page: every pixel outside the items is clear.
        var all = PagePixels.Read(page, 0, 0, 64, 64);
        for (var p = 0; p < 64 * 64; p++)
        {
            var (x, y) = (p % 64, p / 64);
            if (!items.Any(i => x >= i.SourceX && x < i.SourceX + i.SourceWidth && y >= i.SourceY && y < i.SourceY + i.SourceHeight)) Assert.Equal(Rgba.Clear, all.Get(x, y));
        }
    }

    [Fact]
    public void EveryFrameResizedTogetherKeepsSharedFramesSharedAndLeavesAPictureAnotherSpriteShows()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            // spr_a: frames 0 and 1 share one picture; frame 2 shows the picture of spr_b
            var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
            var (own, other) = (Frame(data, "spr_a"), Frame(data, "spr_b"));
            PagePixels.Write(own.TexturePage, [(own.SourceX, own.SourceY, Solid(32, 32, Red))]);
            sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = own });
            sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = other });
            var op = new ResizeSpec(false, 40, 36, 1, 1).Op;
            var plan = TextureFit.Plan(data, FramesFitCommand.Requests(data, "spr_a", [0, 1, 2], 40, 36).Requests);
            var command = new FramesFitCommand("spr_a", [0, 1, 2], op);
            history.Execute(command);

            Assert.Equal(plan, command.Steps); // the dialog's line says what the edit does
            Assert.Equal(2, command.Steps.Count); // one placement for the shared picture, one for the picture of spr_b
            Assert.Same(sprite.Textures[0].Texture, sprite.Textures[1].Texture);
            Assert.Same(own, sprite.Textures[0].Texture);
            Assert.NotSame(other, sprite.Textures[2].Texture);
            Assert.Same(other, Frame(data, "spr_b"));
            AssertSolid(data, "spr_b", 20, 20, Blue);
            for (var f = 0; f < 3; f++)
            {
                var picture = FramePicture.Read(data, "spr_a", f);
                Assert.Equal((40, 36), (picture.Width, picture.Height));
                Assert.Equal(f < 2 ? Red : Blue, picture.Get(20, 18));
                Assert.Equal(Rgba.Clear, picture.Get(0, 0)); // the canvas grew around the middle
            }

            AssertValidFile(data);
            var replayed = (FramesFitCommand)EditCommand.Create(command.Kind, command.Args);
            Assert.Equal(command.Args, replayed.Args);
            history.Undo();
            Assert.Same(other, sprite.Textures[2].Texture);
            AssertSolid(data, "spr_a", 32, 32, Red);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-4, 10)]
    [InlineData(10, 0)]
    [InlineData(8193, 10)]
    [InlineData(10, 70000)]
    public void ASizeNoPageCanHoldIsRefusedWithItsReasonAndChangesNothing(int width, int height)
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            var a = Frame(data, "spr_a");
            var page = a.TexturePage;
            var before = (page.TextureData.Image, a.SourceX, a.SourceWidth, data.EmbeddedTextures.Count);
            var plan = Assert.Throws<InvalidDataException>(() => TextureFit.Plan(data, [new FitRequest(a, width, height)]));
            Assert.Contains("1 to 8192", plan.Message, StringComparison.Ordinal);
            var resize = Assert.Throws<InvalidDataException>(() => history.Execute(new PageResizeCommand(page.Name.Content!, width, height)));
            Assert.Contains("1 to 8192", resize.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => SpriteResize.Build(data, "spr_a", 0, new ResizeSpec(false, width, height), true, null));
            Assert.Equal(before, (page.TextureData.Image, a.SourceX, a.SourceWidth, data.EmbeddedTextures.Count));
            Assert.Equal(0, history.Position);
        }
    }

    [Fact]
    public void APictureOnAnExternalPageAndTheExternalPageItselfAreNeverEdited()
    {
        var (data, history) = TwoOnOnePage(new Box(40, 40, 20, 20));
        using (data)
        {
            var a = Frame(data, "spr_a");
            var page = a.TexturePage;
            page.TextureExternal = true;
            var before = (page.TextureData.Image, page.TextureWidth, a.SourceX, a.SourceWidth, data.EmbeddedTextures.Count, data.TexturePageItems.Count);
            foreach (var edit in new EditCommand[] { Fit("spr_a", Solid(50, 50, Red)), Fit("spr_a", Solid(10, 10, Red)), new PageResizeCommand(page.Name.Content!, 32, 32), new PageResizeCommand(page.Name.Content!, 128, 128) })
            {
                var refused = Assert.Throws<InvalidOperationException>(() => history.Execute(edit));
                Assert.Contains("stored outside the data file", refused.Message, StringComparison.Ordinal);
            }

            Assert.Equal(before, (page.TextureData.Image, page.TextureWidth, a.SourceX, a.SourceWidth, data.EmbeddedTextures.Count, data.TexturePageItems.Count));
            Assert.Equal(0, history.Position);
        }
    }

    [Fact]
    public void APageListedByTwoGroupsKeepsBothListsWhenCutAndANewPageJoinsOnlyItsOwnGroup()
    {
        using var data = Groups(false);
        var (home, other) = (data.TextureGroupInfo.First(g => g.Name.Content == "tg_home"), data.TextureGroupInfo.First(g => g.Name.Content == "tg_other"));
        var full = PageResizeCommand.Find(data, "Texture full");
        other.TexturePages.Add(new(full)); // the page is listed by both groups; its own group is tg_home
        var lists = (Home: home.TexturePages.Count, Other: other.TexturePages.Count);
        var (history, pages) = (new EditHistory(data), data.EmbeddedTextures.Count);
        var cut = new PageResizeCommand("Texture full", 2048, 1024); // spr_a (at y 2004) and the 2048 x 2000 block no longer fit
        history.Execute(cut);
        var made = data.EmbeddedTextures.Count - pages;
        Assert.True(made > 0 && cut.Steps.Any(s => s.Kind == FitKind.NewPage));
        Assert.Equal((lists.Home + made, lists.Other), (home.TexturePages.Count, other.TexturePages.Count));
        Assert.Contains(other.TexturePages, r => ReferenceEquals(r.Resource, full));
        Assert.All(data.TexturePageItems.Where(i => i.TexturePage is not null), i => Assert.True(i.SourceX + i.SourceWidth <= i.TexturePage.TextureWidth && i.SourceY + i.SourceHeight <= i.TexturePage.TextureHeight));
        Assert.Same(home, Frame(data, "spr_a").TexturePage.TextureInfo);
        history.Undo();
        Assert.Equal(lists, (home.TexturePages.Count, other.TexturePages.Count));
        Assert.Equal(2048, full.TextureHeight);
    }

    [Fact]
    public void TheAdvancedChoicesPickThePageTurnOffGrowingAndNewPagesAndCapThePageSize()
    {
        var (data, history) = TwoOnOnePage(new Box(34, 0, 24, 24));
        using (data)
        {
            var a = Frame(data, "spr_a");
            IReadOnlyList<FitStep> Plan(int w, int h, FitOptions options) => TextureFit.Plan(data, [new FitRequest(a, w, h)], options: options);
            Assert.Equal(FitKind.Grow, Assert.Single(Plan(100, 100, FitOptions.Default)).Kind);
            var noGrow = Assert.Single(Plan(100, 100, new FitOptions(Grow: false)));
            Assert.Equal((FitKind.FreeSpace, "Texture 0"), (noGrow.Kind, noGrow.Page)); // the empty 128 x 128 page of the same (no) group
            var chosen = Assert.Single(Plan(20, 40, new FitOptions(Page: "Texture 0")));
            Assert.Equal((FitKind.FreeSpace, "Texture 0"), (chosen.Kind, chosen.Page)); // free space on its own page too, but the chosen page comes first
            var capped = Assert.Single(Plan(2100, 8, new FitOptions(MaxSide: 4096)));
            Assert.Equal(FitKind.Grow, capped.Kind);
            Assert.True(capped.PageAfter.Width <= 4096 && capped.PageAfter.Height <= 4096);
            Assert.Contains("1024x1024", Assert.Throws<InvalidDataException>(() => Plan(2000, 8, new FitOptions(MaxSide: 1024))).Message, StringComparison.Ordinal);
            Assert.Contains("a new page is not allowed", Assert.Throws<InvalidDataException>(() => Plan(2100, 8, new FitOptions(NewPage: false))).Message, StringComparison.Ordinal);

            // The choices are part of the edit, so the journal replays to the same place.
            var options = new FitOptions(Page: "Texture 0", MaxSide: 1024);
            var command = new TextureFitCommand("sprites", "spr_a", 0, FramePicture.ToPng(Solid(20, 40, Red)), options);
            history.Execute(command);
            Assert.Equal("Texture 0", a.TexturePage.Name.Content);
            AssertSolid(data, "spr_a", 20, 40, Red);
            var again = (TextureFitCommand)EditCommand.Create(command.Kind, command.Args);
            Assert.Equal(options, FitOptions.Parse(again.Args["fit"]));
            Assert.False(new TextureFitCommand("sprites", "spr_a", 0, [], FitOptions.Default).Args.ContainsKey("fit")); // older journals stay as they were
        }
    }
}
