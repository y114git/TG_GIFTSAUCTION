using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;
using G3MLib.Modding.Resources;
using Xunit;

namespace G3MLib.Tests;

public sealed class RenameBatchTests
{
    private static GameMakerData Load()
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
        return GameMakerIO.Read(input);
    }

    private static byte[] Save(GameMakerData data)
    {
        using MemoryStream output = new();
        GameMakerIO.Write(output, data);
        return output.ToArray();
    }

    private static void EnsureSprites(GameMakerData data, int count)
    {
        while (data.Sprites.Count < count)
        {
            data.Sprites.Add(new GameMakerSprite { Name = data.Strings.MakeString("spr_probe_" + data.Sprites.Count) });
        }
    }

    private static List<GameMakerGameObject> Objects(GameMakerData data, int count)
    {
        while (data.GameObjects.Count < count)
        {
            data.GameObjects.Add(new GameMakerGameObject { Name = data.Strings.MakeString("obj_probe_" + data.GameObjects.Count) });
        }
        return data.GameObjects.Take(count).ToList();
    }

    [Fact]
    public void TwoNamesCanSwap()
    {
        using GameMakerData data = Load();
        List<GameMakerGameObject> objects = Objects(data, 2);
        string a = objects[0].Name.Content, b = objects[1].Name.Content;

        RenamePlan plan = RenameService.PlanRenames(data, [RenameRequest.ForResource(objects[0], b), RenameRequest.ForResource(objects[1], a)]);
        Assert.Empty(plan.Conflicts);
        RenameChangeSet changes = RenameService.Apply(plan);

        Assert.Equal((b, a), (objects[0].Name.Content, objects[1].Name.Content));
        changes.Undo();
        Assert.Equal((a, b), (objects[0].Name.Content, objects[1].Name.Content));
    }

    [Fact]
    public void ThreeNamesCanShiftInACircle()
    {
        using GameMakerData data = Load();
        List<GameMakerGameObject> objects = Objects(data, 3);
        string[] names = objects.Select(o => o.Name.Content).ToArray();

        RenamePlan plan = RenameService.PlanRenames(data, [
            RenameRequest.ForResource(objects[0], names[1]),
            RenameRequest.ForResource(objects[1], names[2]),
            RenameRequest.ForResource(objects[2], names[0])]);

        Assert.Empty(plan.Conflicts);
        RenameService.Apply(plan);
        Assert.Equal([names[1], names[2], names[0]], objects.Select(o => o.Name.Content));
    }

    [Fact]
    public void BatchGivesTheBytesOfTheSingleRenamesInOrder()
    {
        using GameMakerData batch = Load();
        using GameMakerData single = Load();
        (string Sprite, string Object, string Script) names = ("spr_renamed_a", "obj_renamed_a", "scr_renamed_a");

        RenameService.Apply(RenameService.PlanRenames(batch, [
            RenameRequest.ForResource(batch.Sprites[0], names.Sprite),
            RenameRequest.ForResource(batch.GameObjects[0], names.Object),
            RenameRequest.ForResource(batch.Scripts[0], names.Script)]));
        RenameService.RenameResource(single, single.Sprites[0], names.Sprite);
        RenameService.RenameResource(single, single.GameObjects[0], names.Object);
        RenameService.RenameResource(single, single.Scripts[0], names.Script);

        Assert.Equal(Save(single), Save(batch));
    }

    [Fact]
    public void ConflictsAreListedAndApplyRefusesWithoutChangingAnything()
    {
        using GameMakerData data = Load();
        EnsureSprites(data, 2);
        List<GameMakerGameObject> objects = Objects(data, 2);
        byte[] before = Save(data);
        RenameRequest taken = RenameRequest.ForResource(data.Sprites[0], objects[1].Name.Content);
        RenameRequest invalid = RenameRequest.ForResource(data.Sprites[1], "1bad");
        RenameRequest fine = RenameRequest.ForResource(data.Scripts[0], "scr_fine_name");

        RenamePlan plan = RenameService.PlanRenames(data, [taken, invalid, fine]);

        Assert.Equal([(taken, RenameFailure.NameInUse), (invalid, RenameFailure.InvalidName)], plan.Conflicts.Select(c => (c.Request, c.Reason)));
        Assert.Contains(plan.Changes, change => change.NewValue == "scr_fine_name");
        Assert.Throws<InvalidOperationException>(() => RenameService.Apply(plan));
        Assert.Equal(before, Save(data));
    }

    [Fact]
    public void TwoRequestsForTheSameNameConflict()
    {
        using GameMakerData data = Load();
        EnsureSprites(data, 2);

        RenamePlan plan = RenameService.PlanRenames(data, [RenameRequest.ForResource(data.Sprites[0], "same_target"), RenameRequest.ForResource(data.Sprites[1], "same_target")]);

        Assert.Equal(2, plan.Conflicts.Count(c => c.Reason == RenameFailure.NameInUse));
    }

    [Fact]
    public void RenamingOneResourceTwiceIsAnOverlap()
    {
        using GameMakerData data = Load();

        RenamePlan plan = RenameService.PlanRenames(data, [RenameRequest.ForResource(data.Sprites[0], "first_try"), RenameRequest.ForResource(data.Sprites[0], "second_try")]);

        Assert.Contains(plan.Conflicts, c => c.Reason == RenameFailure.Overlap);
    }

    [Fact]
    public void ProgressAndCancellationAreHonoured()
    {
        using GameMakerData data = Load();
        EnsureSprites(data, 2);
        List<BatchProgress> reports = [];
        RenameRequest[] requests = [RenameRequest.ForResource(data.Sprites[0], "progress_a"), RenameRequest.ForResource(data.Sprites[1], "progress_b")];
        RenameService.PlanRenames(data, requests, null, new SyncProgress(reports.Add));
        Assert.Equal([1, 2], reports.Select(r => r.Done));

        using CancellationTokenSource source = new();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => RenameService.PlanRenames(data, requests, null, null, source.Token));
    }

    private sealed class SyncProgress(Action<BatchProgress> onReport) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => onReport(value);
    }
}
