using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class OrganizerTests
{
    private static string Key(string type, string name) => ResourceTypes.KeyOf(type, name);

    private static ExplorerModel Model(Organizer organizer, int objects = 6)
    {
        var model = new ExplorerModel(key => key);
#pragma warning disable CA2000 // the model reads the data only while loading; a test model is not disposed
        model.Load(TestData.Create(objects));
#pragma warning restore CA2000
        model.Organizer = organizer;
        return model;
    }

    [Fact]
    public void FoldersNestMoveAndDeleteWithoutLosingMembers()
    {
        var (o, a) = Organizer.Empty.AddFolder("Heroes", null);
        var (o2, b) = o.AddFolder("Bosses", a);
        o = o2.MoveTo([Key("objects", "obj_0"), Key("objects", "obj_1")], b).MoveTo([Key("objects", "obj_2")], a);
        Assert.Equal([b!], o.ChildrenOf(a).Select(f => f.Id));
        Assert.Equal(3, o.KeysUnder(a!).Count);
        Assert.Equal(["Heroes", "Bosses"], o.PathOf(b!).Select(f => f.Name));

        // a name is unique among siblings; a folder cannot move into its own subtree
        Assert.Null(o.AddFolder("bosses", a).Id);
        Assert.Same(o, o.MoveFolder(a!, b));

        o = o.DeleteFolder(a!);
        Assert.Equal([b!], o.ChildrenOf(null).Select(f => f.Id));
        Assert.Equal(2, o.KeysUnder(b!).Count);
        Assert.Null(o.FolderOf.GetValueOrDefault(Key("objects", "obj_2"))); // a member of a top folder returns to the top

        // a resource sits in one folder
        o = o.MoveTo([Key("objects", "obj_0")], null);
        Assert.Equal([Key("objects", "obj_1")], o.KeysUnder(b!));
    }

    [Fact]
    public void CollectionsHoldAResourceMoreThanOnceAndFollowRenames()
    {
        var k = Key("objects", "obj_0");
        var o = Organizer.Empty.AddToCollection("Enemies", [k]).AddToCollection("enemies", [Key("objects", "obj_1")]).AddToCollection("Boss fight", [k]);
        Assert.Equal(["Enemies", "Boss fight"], o.CollectionsOf(k));
        Assert.Equal(2, o.Collection("Enemies")!.Keys.Count);

        o = o.Rekey(k, Key("objects", "obj_z"));
        Assert.Contains(Key("objects", "obj_z"), o.Collection("Boss fight")!.Keys);
        o = o.Rekey(Key("objects", "obj_z"), null);
        Assert.Empty(o.Collection("Boss fight")!.Keys);
        Assert.Empty(o.RemoveFromCollection("Enemies", [Key("objects", "obj_1")]).Collection("Enemies")!.Keys);
    }

    [Fact]
    public void RestoreBringsADeletedResourceBackToItsFolderAndSurvivingCollections()
    {
        var k = Key("objects", "obj_0");
        var (o, folder) = Organizer.Empty.AddFolder("Bosses", null);
        o = o.MoveTo([k], folder).AddToCollection("Enemies", [k]).AddToCollection("Gone", [k]);
        var (inFolder, inSets) = (o.FolderOf[k], o.CollectionsOf(k));
        o = o.Rekey(k, null).DeleteCollection("Gone");
        Assert.False(o.FolderOf.ContainsKey(k));
        o = o.Restore(k, inFolder, inSets);
        Assert.Equal(folder, o.FolderOf[k]);
        Assert.Equal(["Enemies"], o.CollectionsOf(k));

        var q = QuickAccess.Empty.AddTag(k, "boss").ToggleFavourite(k);
        var (tags, fav) = (q.TagsOf(k), q.IsFavourite(k));
        q = q.Rekey(k, null);
        Assert.Empty(q.TagsOf(k));
        q = q.Restore(k, tags, fav);
        Assert.Equal(["boss"], q.TagsOf(k));
        Assert.True(q.IsFavourite(k));
    }

    [Fact]
    public void StoreRoundTripsAndSurvivesADamagedOrHandEditedFile()
    {
        using var dir = new TempDir();
        var store = new OrganizerStore(new UserData(dir.File("user")));
        var (o, id) = Organizer.Empty.AddFolder("A", null);
        o = o.MoveTo([Key("objects", "obj_0")], id).AddToCollection("C", [Key("objects", "obj_0")]).ColourTag("hard", "#FF0000") with { FolderView = true };
        store.Save("data.win", o);
        var back = store.Load("data.win");
        Assert.Equal(id, back.FolderOf[Key("objects", "obj_0")]);
        Assert.Equal("#FF0000", back.TagColours["hard"]);
        Assert.True(back.FolderView);

        Assert.Equal(Organizer.Empty, store.Load("other.win")); // none saved
        var bad = new Organizer { Folders = [new VirtualFolder("x", "X", "x")], FolderOf = new Dictionary<string, string> { ["k"] = "gone" } }.Sanitized();
        Assert.Null(bad.Folders[0].Parent);
        Assert.Empty(bad.FolderOf);
    }

    [Fact]
    public void ACycleOfFoldersIsCutInsteadOfHanging()
    {
        var cyclic = new Organizer { Folders = [new VirtualFolder("a", "A", "b"), new VirtualFolder("b", "B", "a"), new VirtualFolder("c", "C", "b")] };
        Assert.Equal(3, cyclic.Subtree("a").Count);
        var clean = cyclic.Sanitized();
        Assert.Null(clean.Folder("a")!.Parent);
        Assert.Equal(["a", "b", "c"], clean.Subtree("a"));
        Assert.Equal(3, clean.Folders.Count);
    }

    [Fact]
    public void FolderViewListsFoldersThenWhatNoFolderHolds()
    {
        var (o, id) = Organizer.Empty.AddFolder("Heroes", null);
        o = o.MoveTo([Key("objects", "obj_0"), Key("objects", "obj_1")], id) with { FolderView = true };
        var model = Model(o);
        var rows = model.Rows;
        var folder = rows.Single(r => r.Kind == ExplorerRowKind.Folder && r.Depth == 1);
        Assert.Equal(("Heroes", 2), (folder.Text, folder.Count));
        Assert.Contains(rows, r => r is { IsItem: true, Text: "obj_0", Depth: 2 });
        // objects heading counts only the unfiled four
        Assert.Equal(4, rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" }).Count);
        Assert.DoesNotContain(rows, r => r is { IsItem: true, Text: "obj_0", Depth: 3 });

        model.Toggle(folder.Key); // folders start open; a click closes
        Assert.DoesNotContain(model.Rows, r => r is { IsItem: true, Text: "obj_0" } && r.Depth == 2);
        Assert.False(model.Rows.Single(r => r.Key == folder.Key).Expanded);

        model.Organizer = o with { FolderView = false };
        Assert.Equal(6, model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" }).Count);
        Assert.Contains(model.Rows, r => r.Kind == ExplorerRowKind.Folder && r.Depth == 3); // under Quick Access
    }

    [Fact]
    public void CollectionsAndTagColoursShowUnderQuickAccessWithCounts()
    {
        var o = Organizer.Empty.AddToCollection("Enemies", [Key("objects", "obj_0"), Key("objects", "obj_1"), Key("objects", "gone")]).ColourTag("hard", "#00FF00");
        var model = Model(o);
        model.Quick = QuickAccess.Empty.AddTag(Key("objects", "obj_0"), "hard").AddTag(Key("objects", "gone"), "hard");
        model.Toggle(ExplorerModel.QuickTags);
        Assert.Equal("#00FF00", model.Rows.Single(r => r.Key == ExplorerModel.TagPrefix + "hard").Colour);
        Assert.Equal(1, model.Rows.Single(r => r.Key == ExplorerModel.TagPrefix + "hard").Count); // a deleted resource keeps its tag for an undo but is not counted
        var collection = model.Rows.Single(r => r.Kind == ExplorerRowKind.Collection);
        Assert.Equal(2, collection.Count); // the entry that is gone is not counted
        Assert.False(collection.Expanded);
        model.Toggle(collection.Key);
        Assert.Equal(2, model.Rows.Count(r => r is { IsItem: true, Depth: 4 }));

        model.ShowCounts = false;
        Assert.All(model.Rows, r => Assert.False(r.HasCount));
    }

    [Fact]
    public void ScopeNarrowsToAFolderWithABreadcrumbAndFilters()
    {
        var (o, a) = Organizer.Empty.AddFolder("A", null);
        var (o2, b) = o.AddFolder("B", a);
        o = o2.MoveTo([Key("objects", "obj_0")], a).MoveTo([Key("objects", "obj_1"), Key("scripts", "scr_1")], b);
        var model = Model(o);
        model.Scope = ExplorerModel.FolderPrefix + b;
        Assert.Equal(["objects", "scripts"], model.Rows.Where(r => r.IsItem).Select(r => r.Key).Order());
        Assert.Equal(["breadcrumb-root", "A", "B"], model.Breadcrumb.Select(p => p.Text));
        Assert.Null(model.Breadcrumb[0].Scope);

        model.Scope = ExplorerModel.FolderPrefix + a;
        Assert.Equal(["B", "obj_1", "scr_1", "obj_0"], model.Rows.Select(r => r.Text)); // folders start open
        model.SetFilter("scr:");
        Assert.Equal(["scr_1"], model.Rows.Select(r => r.Text)); // the filter looks below the folder too
        Assert.Equal(1, model.Matches);

        model.SetFilter("");
        model.Organizer = o.DeleteFolder(a!).DeleteFolder(b!);
        Assert.Null(model.Scope); // gone: back to the whole tree
        Assert.Contains(model.Rows, r => r.Key == "objects");
    }

    [Fact]
    public void KeysOfRowsSupportSelectByTypeAndAllInFolder()
    {
        var (o, a) = Organizer.Empty.AddFolder("A", null);
        o = o.MoveTo([Key("objects", "obj_0"), Key("scripts", "scr_0"), Key("objects", "missing")], a);
        var model = Model(o);
        var folder = model.Rows.First(r => r.Kind == ExplorerRowKind.Folder);
        Assert.Equal([Key("objects", "obj_0"), Key("scripts", "scr_0")], model.KeysOf(folder).Order());
        Assert.Equal(6, model.KeysOf(model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" })).Count);
        Assert.Equal(6, model.KeysOfType("scripts").Count);
    }

    [Fact]
    public void TypeAheadFindsTheNextMatchAndWraps()
    {
        var model = Model(Organizer.Empty, 3);
        model.SetFilter("obj");
        var rows = model.Rows;
        var first = ExplorerModel.TypeAhead(rows, -1, "obj_0");
        Assert.Equal("obj_0", rows[first].Text);
        Assert.Equal("obj_1", rows[ExplorerModel.TypeAhead(rows, first, "o")].Text);
        Assert.Equal("obj_2", rows[ExplorerModel.TypeAhead(rows, first, "obj_2")].Text);
        Assert.Equal(first, ExplorerModel.TypeAhead(rows, first, "obj_0")); // a longer text may stay
        Assert.Equal(-1, ExplorerModel.TypeAhead(rows, 0, "zzz"));
    }

    [Fact]
    public void BulkRenamePlansPrefixSuffixReplaceAndRegexWithProblems()
    {
        (string, string)[] items = [("objects", "obj_a"), ("objects", "obj_b"), ("objects", "enemy")];
        static bool Exists(string t, string n) => n == "taken_b";

        Assert.Equal(["x_obj_a", "x_obj_b", "x_enemy"], BulkRename.Plan(items, new(RenameMode.Prefix, "", "x_"), Exists).Select(s => s.New));
        Assert.Equal(["obj_a_1", "obj_b_1", "enemy_1"], BulkRename.Plan(items, new(RenameMode.Suffix, "", "_1"), Exists).Select(s => s.New));
        Assert.Equal(["o_a", "o_b", "enemy"], BulkRename.Plan(items, new(RenameMode.Replace, "bj", ""), Exists).Select(s => s.New));
        var rx = BulkRename.Plan(items, new(RenameMode.Regex, @"^obj_(\w)$", "$1_obj"), Exists);
        Assert.Equal(["a_obj", "b_obj", "enemy"], rx.Select(s => s.New));
        Assert.False(rx[2].Changes);

        Assert.All(BulkRename.Plan(items, new(RenameMode.Regex, "(", "x"), Exists), s => Assert.Equal(BulkRename.BadPattern, s.Problem));
        var same = BulkRename.Plan(items, new(RenameMode.Regex, @"obj_.", "same"), Exists);
        Assert.Equal(BulkRename.Duplicate, same[0].Problem);
        Assert.Equal(BulkRename.Taken, BulkRename.Plan(items, new(RenameMode.Replace, "obj_b", "taken_b"), Exists)[1].Problem);
        Assert.Equal(BulkRename.Empty, BulkRename.Plan(items, new(RenameMode.Replace, "enemy", ""), Exists)[2].Problem);
        Assert.Equal(BulkRename.Invalid, BulkRename.Plan(items, new(RenameMode.Prefix, "", "a b"), Exists)[0].Problem);
    }

    [Fact]
    public void BulkRenameRunsAsOneUndoStep()
    {
        using var data = TestData.Create(3);
        var history = new EditHistory(data);
        var original = TestData.Bytes(data);
        var steps = BulkRename.Plan([("objects", "obj_0"), ("objects", "obj_1"), ("objects", "obj_2")], new(RenameMode.Prefix, "", "boss_"), (t, n) => CopyPlanner.Exists(data, t, n));
        history.Execute(BulkRename.Command(steps)!);
        Assert.Equal(["boss_obj_0", "boss_obj_1", "boss_obj_2"], data.GameObjects.Select(o => o.Name.Content));
        Assert.Single(history.Items);
        history.MoveTo(0);
        Assert.Equal(original, TestData.Bytes(data));
        Assert.Null(BulkRename.Command(BulkRename.Plan([("objects", "obj_0")], new(RenameMode.Prefix, "", ""), (_, _) => false)));
    }
}
