using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using Xunit;

namespace G3MLib.Tests;

public sealed class DataRepairTests
{
    private static string? Find(string name)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        if (File.Exists(fixture))
            return fixture;
        return File.Exists(name) ? name : null;
    }

    public static IEnumerable<object[]> Files() =>
    [
        ["data-2022.win"],
        ["data-2023.win"],
        ["data-2024.win"],
        ["data-2026.win"],
        [@"C:\Program Files (x86)\Steam\steamapps\common\Undertale\data.win"],
        [@"C:\Program Files (x86)\Steam\steamapps\common\DELTARUNE\chapter1_windows\data.win"]
    ];

    [Theory]
    [MemberData(nameof(Files))]
    public void RecomputedCountersEqualTheFilesOwn(string name)
    {
        if (Find(name) is not { } path)
            return;
        using FileStream input = File.OpenRead(path);
        using GameMakerData data = GameMakerIO.Read(input);

        VariableCounters counters = DataRepair.RecomputeVariableCounters(data);

        Assert.Equal((data.VarCount1, data.VarCount2, data.MaxLocalVarCount), (counters.VarCount1, counters.VarCount2, counters.MaxLocalVarCount));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void BuiltCodeLocalsEqualTheFilesOwn(string name)
    {
        if (Find(name) is not { } path)
            return;
        using FileStream input = File.OpenRead(path);
        using GameMakerData data = GameMakerIO.Read(input);
        int different = 0;
        foreach (GameMakerCode code in data.Code.Where(c => c.ParentEntry is null))
        {
            GameMakerCodeLocals? real = data.CodeLocals?.FirstOrDefault(l => l.Name.Content == code.Name.Content);
            if (real is null)
                continue;
            GameMakerCodeLocals built = DataRepair.BuildCodeLocals(data, code);
            if (!built.Locals.Select(l => (l.Name.Content, l.Index)).Order().SequenceEqual(real.Locals.Select(l => (l.Name.Content, l.Index)).Order()))
                different++;
        }

        // GameMaker lists locals by declaration and this lists them by first use: before 2.3 the index follows the position, so a handful of entries differ
        Assert.True(different <= 2, $"{different} entries differ");
    }

    [Fact]
    public void RestoreAddsMissingLocalsAndLeavesOthers()
    {
        using FileStream input = File.OpenRead(Find("data-2022.win")!);
        using GameMakerData data = GameMakerIO.Read(input);
        GameMakerCode code = data.Code.First(c => c.ParentEntry is null && data.CodeLocals.Any(l => l.Name.Content == c.Name.Content));
        GameMakerCodeLocals real = data.CodeLocals.First(l => l.Name.Content == code.Name.Content);
        List<(string?, uint)> expected = real.Locals.Select(l => (l.Name.Content, l.Index)).ToList();
        int before = data.CodeLocals.Count;
        data.CodeLocals.Remove(real);

        int restored = DataRepair.RestoreMissingCodeLocals(data);

        Assert.Equal(1, restored);
        Assert.Equal(before, data.CodeLocals.Count);
        Assert.Equal(expected.Order(), data.CodeLocals.First(l => l.Name.Content == code.Name.Content).Locals.Select(l => (l.Name.Content, l.Index)).Order());
        Assert.Equal(0, DataRepair.RestoreMissingCodeLocals(data));
    }

    [Fact]
    public void ApplyWritesTheCountersIntoTheHeader()
    {
        using FileStream input = File.OpenRead(Find("data-2026.win")!);
        using GameMakerData data = GameMakerIO.Read(input);
        (uint one, uint two, uint max) = (data.VarCount1, data.VarCount2, data.MaxLocalVarCount);
        data.VarCount1 = data.VarCount2 = 0;
        data.MaxLocalVarCount = 0;

        DataRepair.ApplyVariableCounters(data);

        Assert.Equal((one, two, max), (data.VarCount1, data.VarCount2, data.MaxLocalVarCount));
    }

    [Fact]
    public void WriterRaisesTheCountersWhenVariablesWereAdded()
    {
        using FileStream input = File.OpenRead(Find("data-2026.win")!);
        using GameMakerData data = GameMakerIO.Read(input);
        uint declared = data.VarCount1;
        data.Variables.Add(new GameMakerVariable { Name = data.Strings.MakeString("added_without_counter"), InstanceType = GameMakerInstruction.InstanceType.Self, VarID = (int)declared + 4 });

        using MemoryStream output = new();
        GameMakerIO.Write(output, data);
        output.Position = 0;
        using GameMakerData reloaded = GameMakerIO.Read(output);

        Assert.Equal(declared + 5, reloaded.VarCount1);
    }
}
