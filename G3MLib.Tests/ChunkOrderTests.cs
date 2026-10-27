using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using Xunit;

namespace G3MLib.Tests;

public sealed class ChunkOrderTests
{
    private static GameMakerData Load()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        return GameMakerIO.Read(input);
    }

    private static List<string> WrittenOrder(GameMakerData data)
    {
        using MemoryStream output = new();
        GameMakerIO.Write(output, data);
        output.Position = 0;
        using GameMakerData reread = GameMakerIO.Read(output);
        return [.. reread.FORM.ChunkOrder];
    }

    [Fact]
    public void RemoveThenAddPutsTheChunkBackInCanonicalPlace()
    {
        using GameMakerData data = Load();
        List<string> before = [.. data.FORM.ChunkOrder];
        GameMakerChunk tags = data.FORM.Chunks["TAGS"];

        Assert.True(data.FORM.RemoveChunk("TAGS"));
        Assert.DoesNotContain("TAGS", data.FORM.ChunkOrder);
        Assert.False(data.FORM.ChunksTypeDict.ContainsKey(tags.GetType()));
        data.FORM.AddChunk(tags);

        Assert.Equal(before, data.FORM.ChunkOrder);
        Assert.Same(tags, data.FORM.ChunksTypeDict[tags.GetType()]);
        Assert.Equal(before, WrittenOrder(data));
    }

    [Fact]
    public void AddingSeveralChunksAfterRemovalsKeepsTheCanonicalOrder()
    {
        using GameMakerData data = Load();
        List<string> before = [.. data.FORM.ChunkOrder];
        Dictionary<string, GameMakerChunk> removed = new();
        foreach (string name in new[] { "ACRV", "SEQN", "STRG", "GEN8" })
        {
            removed[name] = data.FORM.Chunks[name];
            data.FORM.RemoveChunk(name);
        }

        foreach (string name in new[] { "STRG", "GEN8", "SEQN", "ACRV" })
            data.FORM.AddChunk(removed[name]);

        Assert.Equal(before, data.FORM.ChunkOrder);
    }

    [Fact]
    public void UnknownChunkKeepsItsPlaceWhileKnownOnesMove()
    {
        using GameMakerData data = Load();
        data.FORM.AddChunk(new GameMakerRawChunk("ZZZZ") { RawData = new byte[16] });
        data.FORM.SetChunkOrder(["GEN8", "ZZZZ", .. data.FORM.ChunkOrder.Where(n => n is not ("GEN8" or "ZZZZ"))]);
        GameMakerChunk room = data.FORM.Chunks["ROOM"];
        data.FORM.RemoveChunk("ROOM");
        data.FORM.AddChunk(room);

        List<string> order = [.. data.FORM.ChunkOrder];
        Assert.Equal(["GEN8", "ZZZZ", "OPTN"], order.GetRange(0, 3));
        Assert.Equal("ROOM", order[order.IndexOf("TAGS") + 1]);
    }

    [Fact]
    public void SetChunkOrderRefusesNamesThatAreNotLoaded()
    {
        using GameMakerData data = Load();

        Assert.Throws<ArgumentException>(() => data.FORM.SetChunkOrder(["GEN8", "NOPE"]));
        Assert.Equal("GEN8", data.FORM.ChunkOrder[0]);
    }

    [Fact]
    public void CanonicalOrderMatchesTheOrderOfTheFile()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        using GameMakerData data = GameMakerIO.Read(input);

        Assert.Equal(data.FORM.ChunkOrder, GameMakerChunkFORM.CanonicalOrder.Where(n => data.FORM.Chunks.ContainsKey(n)));
    }
}
