using G3MLib.DataFile;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ModPackTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly UserData _userData;

    public ModPackTests() => _userData = new UserData(_dir.File("user"));

    public void Dispose() => _dir.Dispose();

    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    /// <summary>The base game: an object, a sprite, a script with code. <paramref name="code"/> replaces the script's code.</summary>
    private static GameMakerData Base(string code = "return 7;")
    {
        var data = CopyResourcesTests.Source();
        new EditHistory(data).Execute(new EditCodeCommand("gml_Script_scr_use", code));
        return data;
    }

    /// <summary>The base game after a mod: a new sprite, a changed sprite, another script code, a removed script.</summary>
    private static GameMakerData Modded(string code = "return 8;")
    {
        var data = Base();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_new"));
        history.Execute(new SetPropertyCommand("sprites", "spr_a", "OriginX", "4"));
        history.Execute(new EditCodeCommand("gml_Script_scr_use", code));
        history.Execute(new ResourceListCommand("scripts", ResourceListCommand.Delete, "scr_0"));
        return data;
    }

    private async Task<Workspace> Open(string name, GameMakerData data)
    {
        using (data) TestData.Write(_dir.File(name), data);
        return await new WorkspaceService(_userData, RecentFiles.Load(_userData)).OpenAsync(_dir.File(name), cancellationToken: Token);
    }

    private static Side Of(Workspace w) => new(w.Data, w.Path);

    private static DiffResult Diff(Workspace a, Workspace b) => DataDiff.Compare(a.Data, b.Data, Token);

    private static ModPackContent Pack(Workspace baseFile, Workspace modded) =>
        ModPackBuilder.Build(Of(baseFile), Of(modded), Diff(baseFile, modded).Items, new PackInfo("Test mod", "1.2.0", ["me"]), Token).Pack;

    [Fact]
    public async Task TheDiffListsAddedRemovedAndChangedResourcesOnly()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        var diff = Diff(left, right);
        Assert.Equal(["sprites:spr_a", "sprites:spr_new", "scripts:scr_0"], diff.Items.Where(i => i.Type != "code").Select(i => i.Key));
        Assert.Equal([ChangeKind.Changed, ChangeKind.Added, ChangeKind.Removed], diff.Items.Where(i => i.Type != "code").Select(i => i.Kind));
        Assert.Contains(diff.Items, i => i is { Type: "code", Name: "gml_Script_scr_use", Kind: ChangeKind.Changed });
        Assert.Empty(Diff(left, left).Items);
    }

    [Fact]
    public async Task PropertyDiffNamesTheChangedProperty()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        var change = Assert.Single(DataDiff.Properties(left.Data, right.Data, "sprites", "spr_a"));
        Assert.Equal(new PropertyChange("OriginX", "0", "4"), change);
    }

    [Fact]
    public async Task FiltersNarrowByTypeKindAndText()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        var diff = Diff(left, right);
        Assert.Equal(["sprites:spr_new"], diff.Where(new DiffFilter(new HashSet<string> { "sprites" }, new HashSet<ChangeKind> { ChangeKind.Added })).Select(i => i.Key));
        Assert.Equal(["sprites:spr_a"], diff.Where(new DiffFilter(new HashSet<string>(), new HashSet<ChangeKind>(), "_a")).Select(i => i.Key));
        Assert.Equal(1, diff.Count(ChangeKind.Removed));
    }

    [Fact]
    public async Task APackSurvivesWritingAndReading()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        var pack = Pack(left, right);
        ModPackFile.Write(_dir.File("m.g3mpack"), pack);
        var read = ModPackFile.Read(_dir.File("m.g3mpack"));
        Assert.Equal(("Test mod", "1.2.0", ModPackBuilder.HashFile(left.Path)), (read.Manifest.Name, read.Manifest.Version, read.Manifest.TargetHash));
        Assert.Equal(pack.Entries.Select(e => (e.Change.Key, e.Change.Op, e.Change.BaseHash, e.Change.NewHash)), read.Entries.Select(e => (e.Change.Key, e.Change.Op, e.Change.BaseHash, e.Change.NewHash)));
        Assert.Equal(pack.Entries.Select(e => e.Payload?.Length), read.Entries.Select(e => e.Payload?.Length));
        Assert.Equal("return 8;", read.Entries.Single(e => e.Change.Op == "code").NewCode?.Trim());
    }

    [Fact]
    public void AFileThatIsNotAPackIsRefused()
    {
        System.IO.Compression.ZipFile.Open(_dir.File("x.g3mpack"), System.IO.Compression.ZipArchiveMode.Create).Dispose();
        Assert.Throws<InvalidDataException>(() => ModPackFile.Read(_dir.File("x.g3mpack")));
    }

    [Fact]
    public async Task ReapplyingOnTheBaseIsOneUndoStepThatMakesTheFileLikeTheMod()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        using var target = await Open("t.win", Base());
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        Assert.Equal(0, plan.Count(PackState.Conflict));
        Assert.Equal(plan.Items.Count, plan.Count(PackState.Apply));
        var before = target.History.Position;

        target.Edit(ModPackApplier.Build(plan, target.Data) ?? throw new InvalidOperationException("nothing to apply"));

        Assert.Equal(before + 1, target.History.Position);
        Assert.Empty(Diff(right, target).Items);
        target.Undo();
        Assert.Empty(Diff(left, target).Items);
    }

    [Fact]
    public async Task ADryRunChangesNothingAndReportsEveryItem()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        using var target = await Open("t.win", Base());
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        Assert.Empty(Diff(left, target).Items);
        Assert.Equal(plan.Items.Count + 1, plan.Report().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("scr_0", plan.Report(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReapplyingOnTheModdedFileFindsEverythingAlreadyDone()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        using var target = await Open("t.win", Modded());
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        Assert.Equal(plan.Items.Count, plan.Count(PackState.AlreadyApplied));
        Assert.Null(ModPackApplier.Build(plan, target.Data));
    }

    [Fact]
    public async Task ACodeChangedOnBothSidesIsAConflictAndKeepMineLeavesIt()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        using var target = await Open("t.win", Base("return 9;"));
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal("code:gml_Script_scr_use", conflict.Key);
        Assert.False(conflict.Merge?.Clean);

        target.Edit(ModPackApplier.Build(plan, target.Data) ?? throw new InvalidOperationException("nothing to apply"));
        Assert.Equal("return 9;", CodeText.Decompile(target.Data, "gml_Script_scr_use").Trim());
        Assert.NotNull(target.Data.Sprites.FirstOrDefault(s => s.Name.Content == "spr_new"));
    }

    [Fact]
    public async Task KeepTheirsTakesThePackCodeAndUndoBringsMineBack()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        using var target = await Open("t.win", Base("return 9;"));
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        var choices = new Dictionary<string, Resolution> { ["code:gml_Script_scr_use"] = Resolution.KeepTheirs };

        target.Edit(ModPackApplier.Build(plan, target.Data, choices) ?? throw new InvalidOperationException("nothing to apply"));
        Assert.Equal("return 8;", CodeText.Decompile(target.Data, "gml_Script_scr_use").Trim());
        target.Undo();
        Assert.Equal("return 9;", CodeText.Decompile(target.Data, "gml_Script_scr_use").Trim());
    }

    [Fact]
    public async Task MergeCodeCombinesChangesThatDoNotTouchTheSameLines()
    {
        const string original = "var a = 1;\nvar b = 2;\nvar c = 3;\nreturn a + b + c;";
        using var left = await Open("a.win", Base(original));
        using var right = await Open("b.win", Modded(original.Replace("c = 3", "c = 30", StringComparison.Ordinal)));
        using var target = await Open("t.win", Base(original.Replace("a = 1", "a = 10", StringComparison.Ordinal)));
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.True(conflict.Merge?.Clean);

        var choices = new Dictionary<string, Resolution> { [conflict.Key] = Resolution.MergeCode };
        target.Edit(ModPackApplier.Build(plan, target.Data, choices) ?? throw new InvalidOperationException("nothing to apply"));
        var merged = CodeText.Decompile(target.Data, "gml_Script_scr_use");
        Assert.Contains("10", merged, StringComparison.Ordinal);
        Assert.Contains("30", merged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AResourceChangedOnBothSidesIsAConflictKeptByDefault()
    {
        using var left = await Open("a.win", Base());
        using var right = await Open("b.win", Modded());
        var other = Base();
        new EditHistory(other).Execute(new SetPropertyCommand("sprites", "spr_a", "OriginX", "9"));
        using var target = await Open("t.win", other);
        var plan = ModPackApplier.Plan(Pack(left, right), target.Data, Token);
        Assert.Contains(plan.Conflicts, c => c.Key == "sprites:spr_a");

        target.Edit(ModPackApplier.Build(plan, target.Data) ?? throw new InvalidOperationException("nothing to apply"));
        Assert.Equal(9, target.Data.Sprites.Single(s => s.Name.Content == "spr_a").OriginX);
        target.Undo();
        var theirs = new Dictionary<string, Resolution> { ["sprites:spr_a"] = Resolution.KeepTheirs };
        target.Edit(ModPackApplier.Build(plan, target.Data, theirs) ?? throw new InvalidOperationException("nothing to apply"));
        Assert.Equal(4, target.Data.Sprites.Single(s => s.Name.Content == "spr_a").OriginX);
    }

    [Fact]
    public async Task ApplyingOneItemAtATimeMakesMineLikeTheOtherAndUndoes()
    {
        using var mine = await Open("a.win", Base());
        using var other = await Open("b.win", Modded());
        foreach (var item in Diff(mine, other).Items)
        {
            var command = ModPackApplier.ForItem(Of(mine), Of(other), item, null, false, out _);
            if (command is not null) mine.Edit(command);
        }

        Assert.Empty(Diff(mine, other).Items);
        while (mine.History.Position > 0) mine.Undo();
        Assert.Equal(4, Diff(mine, other).Items.Count);
    }

    [Fact]
    public async Task ApplyingOnePropertyChangesOnlyThatProperty()
    {
        using var mine = await Open("a.win", Base());
        using var other = await Open("b.win", Modded());
        var item = Diff(mine, other).Items.Single(i => i.Key == "sprites:spr_a");
        var change = Assert.Single(DataDiff.Properties(mine.Data, other.Data, item.Type, item.Name));
        mine.Edit(ModPackApplier.ForItem(Of(mine), Of(other), item, change, false, out _) ?? throw new InvalidOperationException("no edit"));
        Assert.Equal(4, mine.Data.Sprites.Single(s => s.Name.Content == "spr_a").OriginX);
    }

    [Fact]
    public async Task RoomInstancesAreComparedById()
    {
        var left = Base();
        var right = Base();
        foreach (var data in new[] { left, right })
        {
            var history = new EditHistory(data);
            history.Execute(ResourceTemplates.New(data, "rooms", "room_a"));
            history.Execute(new RoomInstanceCommand("room_a", RoomInstanceCommand.Add, 0, 10, 10, "obj_a"));
        }

        new EditHistory(right).Execute(new RoomInstanceCommand("room_a", RoomInstanceCommand.Add, 0, 50, 60, "obj_a"));
        using var a = await Open("a.win", left);
        using var b = await Open("b.win", right);
        var change = Assert.Single(DataDiff.Instances(a.Data, b.Data, "room_a"));
        Assert.Equal(ChangeKind.Added, change.Kind);
        Assert.Contains(Diff(a, b).Items, i => i is { Key: "rooms:room_a", Kind: ChangeKind.Changed });
    }

    [Fact]
    public void CodeViewPairsChangedLinesAndCutsUnifiedContext()
    {
        var lines = CodeView.Lines("a\nb\nc\nd\ne\nf\ng", "a\nB\nc\nd\ne\nf\ng", false);
        var rows = CodeView.SideBySide(lines);
        Assert.Equal(("b", "B"), (rows[1].Left?.Text, rows[1].Right?.Text));
        Assert.Equal("  a\n- b\n+ B\n  c\n...\n", CodeView.Unified(lines, 1));
        Assert.All(CodeView.Lines("x  y", "x y", true), l => Assert.Equal(DiffKind.Same, l.Kind));
    }

    [Fact]
    public void ThreeWayMergeKeepsBothEditsAndMarksClashes()
    {
        var clean = TextMerge.Merge("a\nb\nc\nd", "a\nB\nc\nd", "a\nb\nc\nD");
        Assert.Equal(("a\nB\nc\nD", 0), (clean.Text, clean.Conflicts));
        var clash = TextMerge.Merge("a\nb\nc", "a\nX\nc", "a\nY\nc");
        Assert.Equal(1, clash.Conflicts);
        Assert.Contains("<<<<<<< mine\nX\n=======\nY\n>>>>>>> theirs", clash.Text, StringComparison.Ordinal);
        Assert.Equal("a\nZ\nc", TextMerge.Merge("a\nb\nc", "a\nZ\nc", "a\nZ\nc").Text);
    }

    [Fact]
    public void PictureDifferenceMarksOnlyThePixelsThatDiffer()
    {
        var a = new PixelBuffer(2, 1, [10, 10, 10, 255, 20, 20, 20, 255]);
        var b = new PixelBuffer(2, 1, [10, 10, 10, 255, 200, 20, 20, 255]);
        var diff = PictureDiff.Difference(a, b, out var count);
        Assert.Equal(1, count);
        Assert.Equal(255, diff.Data[4]);
        Assert.Equal(new[] { (byte)10, (byte)10, (byte)10, (byte)255, (byte)200, (byte)20, (byte)20, (byte)255 }, PictureDiff.Swipe(a, b, 1).Data);
    }
}
