using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using Xunit;

namespace G3MLib.Tests;

public sealed class DataLookupTests
{
    private static GameMakerSprite AddSprite(GameMakerData data, string name)
    {
        GameMakerSprite sprite = new() { Name = data.Strings.MakeString(name) };
        data.Sprites.Add(sprite);
        return sprite;
    }

    [Fact]
    public void FindStringHitAndMiss()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        GameMakerString existing = data.Strings.MakeString("probe");
        DataLookup lookup = DataLookup.Build(data);
        Assert.Same(existing, lookup.FindString("probe"));
        Assert.Null(lookup.FindString("missing"));
    }

    [Fact]
    public void GetOrAddStringAppendsOnceAndKeepsIndex()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        DataLookup lookup = DataLookup.Build(data);
        int before = data.Strings.Count;
        GameMakerString first = lookup.GetOrAddString("fresh", out int index);
        GameMakerString second = lookup.GetOrAddString("fresh");
        Assert.Same(first, second);
        Assert.Equal(before + 1, data.Strings.Count);
        Assert.Equal(before, index);
        Assert.Same(first, data.Strings[index]);
        Assert.Same(first, lookup.FindString("fresh"));
    }

    [Fact]
    public void TryGetFindsByNameWithIndexAndIsCaseSensitive()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        AddSprite(data, "sprA");
        GameMakerSprite b = AddSprite(data, "sprB");
        DataLookup lookup = DataLookup.Build(data);
        Assert.True(lookup.TryGet("sprB", out GameMakerSprite found, out int index));
        Assert.Same(b, found);
        Assert.Equal(data.Sprites.IndexOf(b), index);
        Assert.False(lookup.TryGet("SPRB", out GameMakerSprite _, out int missIndex));
        Assert.Equal(-1, missIndex);
    }

    [Fact]
    public void AddThenTryGet()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        DataLookup lookup = DataLookup.Build(data);
        Assert.False(lookup.TryGet("late", out GameMakerSprite _, out int _));
        GameMakerSprite sprite = new() { Name = lookup.GetOrAddString("late") };
        lookup.Add(sprite);
        Assert.True(lookup.TryGet("late", out GameMakerSprite found, out int index));
        Assert.Same(sprite, found);
        Assert.Same(sprite, data.Sprites[index]);
    }

    [Fact]
    public void ListChangesOutsideTheLookupRebuildTheIndex()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        GameMakerSprite a = AddSprite(data, "sprA");
        DataLookup lookup = DataLookup.Build(data);
        Assert.True(lookup.TryGet("sprA", out GameMakerSprite _, out int _));

        GameMakerSprite b = AddSprite(data, "sprB");
        Assert.True(lookup.TryGet("sprB", out GameMakerSprite foundB, out int indexB));
        Assert.Same(b, foundB);
        Assert.Equal(data.Sprites.IndexOf(b), indexB);

        data.Sprites.Remove(a);
        Assert.False(lookup.TryGet("sprA", out GameMakerSprite _, out int _));
        Assert.True(lookup.TryGet("sprB", out GameMakerSprite _, out int shifted));
        Assert.Equal(0, shifted);

        data.Strings.Add(new GameMakerString("outside"));
        Assert.NotNull(lookup.FindString("outside"));
    }

    [Fact]
    public void InPlaceRenameIsRejectedAndRebuilt()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        GameMakerSprite a = AddSprite(data, "old");
        DataLookup lookup = DataLookup.Build(data);
        Assert.True(lookup.TryGet("old", out GameMakerSprite _, out int _));
        a.Name.Content = "new";
        Assert.False(lookup.TryGet("old", out GameMakerSprite _, out int _));
        Assert.True(lookup.TryGet("new", out GameMakerSprite found, out int _));
        Assert.Same(a, found);
    }

    [Fact]
    public void BuildHandlesTenThousandStrings()
    {
        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        for (int i = 0; i < 10000; i++)
        {
            data.Strings.Add(new GameMakerString("s" + i));
        }
        DataLookup lookup = DataLookup.Build(data);
        for (int i = 0; i < 10000; i++)
        {
            GameMakerString found = lookup.FindString("s" + i);
            Assert.NotNull(found);
            Assert.Equal("s" + i, found.Content);
        }
        Assert.Null(lookup.FindString("s10000"));
    }
}
