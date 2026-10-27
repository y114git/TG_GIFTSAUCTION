using System;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;
using Xunit;

namespace G3MLib.Tests;

public sealed class RepackOptionsTests
{
    private static GameMakerData Load()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        return GameMakerIO.Read(input);
    }

    private static MagickImage Solid(int width, int height, byte shade) => new(new MagickColor(shade, shade, shade, 255), (uint)width, (uint)height);

    private static int Sharing(GameMakerData data, GameMakerTexturePageItem item) => data.TexturePageItems.Count(other => other.TexturePage == item.TexturePage);

    private static bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;

    [Fact]
    public void ItemLargerThanMaxItemSizeGetsAPageOfItsOwn()
    {
        using GameMakerData data = Load();
        using MagickImage small = Solid(20, 20, 10);
        using MagickImage big = Solid(300, 200, 20);
        TexturePageRepacker repacker = new(data);
        GameMakerTexturePageItem smallItem = repacker.Add(small);
        GameMakerTexturePageItem bigItem = repacker.Add(big);

        repacker.Apply(repacker.Plan(new RepackOptions(MaxItemSize: 100)));

        Assert.Equal(1, Sharing(data, bigItem));
        Assert.True(Sharing(data, smallItem) > 1);
    }

    [Fact]
    public void KeepAlonePredicateGivesMatchingItemsTheirOwnPage()
    {
        using GameMakerData data = Load();
        using MagickImage first = Solid(20, 20, 10);
        using MagickImage second = Solid(24, 24, 20);
        TexturePageRepacker repacker = new(data);
        GameMakerTexturePageItem alone = repacker.Add(first);
        GameMakerTexturePageItem together = repacker.Add(second);

        repacker.Apply(repacker.Plan(new RepackOptions(KeepAlone: item => ReferenceEquals(item, alone))));

        Assert.Equal(1, Sharing(data, alone));
        Assert.True(Sharing(data, together) > 1);
    }

    [Fact]
    public void ItemsWithDifferentGroupKeysNeverShareAPage()
    {
        using GameMakerData data = Load();
        using MagickImage first = Solid(20, 20, 10);
        using MagickImage second = Solid(24, 24, 20);
        TexturePageRepacker repacker = new(data);
        GameMakerTexturePageItem a = repacker.Add(first);
        GameMakerTexturePageItem b = repacker.Add(second);

        repacker.Apply(repacker.Plan(new RepackOptions(GroupKey: item => ReferenceEquals(item, a) ? "a" : ReferenceEquals(item, b) ? "b" : "rest")));

        Assert.NotSame(a.TexturePage, b.TexturePage);
        Assert.Equal(1, Sharing(data, a));
        Assert.Equal(1, Sharing(data, b));
    }

    [Fact]
    public void PowerOfTwoOffTrimsPagesToTheUsedSizeAndKeepsEveryFrame()
    {
        using GameMakerData data = Load();
        using MagickImage extra = Solid(37, 21, 30);
        TexturePageRepacker repacker = new(data);
        repacker.Add(extra);
        RepackPlan strict = repacker.Plan(new RepackOptions());
        RepackPlan trimmed = repacker.Plan(new RepackOptions(PowerOfTwo: false));
        Assert.True(strict.PageCount.After > 0);

        repacker.Apply(trimmed);

        Assert.Contains(data.EmbeddedTextures, page => !IsPowerOfTwo(page.TextureData.Width) || !IsPowerOfTwo(page.TextureData.Height));
        Assert.All(data.TexturePageItems.Where(item => item.TexturePage is { TextureExternal: false }), item =>
        {
            Assert.True(item.SourceX + item.SourceWidth <= item.TexturePage.TextureData.Width);
            Assert.True(item.SourceY + item.SourceHeight <= item.TexturePage.TextureData.Height);
        });
    }

    [Fact]
    public void DefaultOptionsKeepEveryPagePowerOfTwo()
    {
        using GameMakerData data = Load();
        new TexturePageRepacker(data).Repack();

        Assert.All(data.EmbeddedTextures.Where(page => !page.TextureExternal), page =>
        {
            Assert.True(IsPowerOfTwo(page.TextureData.Width));
            Assert.True(IsPowerOfTwo(page.TextureData.Height));
        });
    }
}
