using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class PropertyTests
{
    [Fact]
    public void DescribeListsEditablePropertiesAndKeepsTheNameReadOnly()
    {
        using var data = TestData.Create(1);
        var rows = PropertyModel.Describe(data.GameObjects[0]);
        Assert.Contains(rows, r => r.Name == "Name" && r.ReadOnly && r.Value == "obj_0");
        Assert.Contains(rows, r => r.Kind == PropertyKind.Bool && !r.ReadOnly);
        Assert.Contains(rows, r => r.Kind == PropertyKind.Whole && !r.ReadOnly);
        Assert.Equal(rows.Count, rows.Select(r => r.Path).Distinct().Count());
    }

    [Fact]
    public void ASetPropertyEditUndoesRedoesAndReplaysFromTheJournal()
    {
        using var data = TestData.Create(1);
        var history = new EditHistory(data);
        var original = TestData.Bytes(data);
        var row = PropertyModel.Describe(data.GameObjects[0]).First(r => r.Kind == PropertyKind.Whole && !r.ReadOnly);
        var command = new SetPropertyCommand("objects", "obj_0", row.Path, "7");

        history.Execute(command);
        Assert.Equal("7", PropertyModel.Read(data.GameObjects[0], row.Path));
        Assert.Contains(command.Touches!, t => t.Type == "GameObjects" && t.Name == "obj_0");
        Assert.Equal("property", command.Kind);

        history.Undo();
        Assert.Equal(original, TestData.Bytes(data));
        history.Redo();
        Assert.Equal("7", PropertyModel.Read(data.GameObjects[0], row.Path));

        // The journal rebuilds the same edit from its arguments.
        var replay = EditCommand.Create(command.Kind, command.Args);
        history.Undo();
        history.Execute(replay);
        Assert.Equal("7", PropertyModel.Read(data.GameObjects[0], row.Path));
    }

    [Fact]
    public void AListOfStringsTakesNewEntriesAndEditsAndRemovesThemWithUndo()
    {
        using var data = TestData.Create(0);
        var history = new EditHistory(data);
        var flags = ResourceTypes.FeatureFlagsName;
        var root = PropertyModel.FindRoot(data, "general", flags)!;
        var list = PropertyModel.Describe(root).Single(r => r.Kind == PropertyKind.List);
        Assert.True(list.Addable);

        history.Execute(new ListEntryCommand("general", flags, list.Path, ListEntryCommand.Add, -1));
        var entry = PropertyModel.Describe(root).Single(r => r.Removable);
        history.Execute(new SetPropertyCommand("general", flags, entry.Path, "fast_path"));
        Assert.Equal("fast_path", PropertyModel.Describe(root).Single(r => r.Removable).Value);

        var remove = new ListEntryCommand("general", flags, list.Path, ListEntryCommand.Remove, 0);
        history.Execute(remove);
        Assert.DoesNotContain(PropertyModel.Describe(root), r => r.Removable);
        history.Undo();
        Assert.Equal("fast_path", PropertyModel.Describe(root).Single(r => r.Removable).Value);
        history.Undo();
        history.Undo();
        Assert.Empty(data.FeatureFlags.List);
        Assert.Equal(remove.Args, EditCommand.Create(remove.Kind, remove.Args).Args);
    }

    [Fact]
    public void AnyPlainListTakesAddMoveAndRemoveWithUndoWhileOwnedListsAreRefused()
    {
        using var data = TestData.Create(1);
        var history = new EditHistory(data);
        var options = ResourceTypes.GameOptionsName;
        var root = PropertyModel.FindRoot(data, "general", options)!;
        var constants = PropertyModel.Describe(root).Single(r => r.Name == "Constants");
        Assert.True(constants.Addable);
        var original = TestData.Bytes(data);
        var before = ((System.Collections.IList)PropertyModel.ReadRaw(root, constants.Path)!).Count;

        history.Execute(new ListEntryCommand("general", options, constants.Path, ListEntryCommand.Add, -1));
        history.Execute(new ListEntryCommand("general", options, constants.Path, ListEntryCommand.Add, -1));
        var rows = PropertyModel.Describe(root).Where(r => r.Removable && r.Path.StartsWith(constants.Path + "[", StringComparison.Ordinal)).ToList();
        Assert.Equal(before + 2, rows.Count);
        Assert.True(rows[0].CanMoveDown && !rows[0].CanMoveUp);

        var first = PropertyModel.ReadRaw(root, rows[0].Path);
        history.Execute(new ListEntryCommand("general", options, constants.Path, ListEntryCommand.Down, 0));
        Assert.Same(first, PropertyModel.ReadRaw(root, rows[1].Path));
        history.Undo();
        Assert.Same(first, PropertyModel.ReadRaw(root, rows[0].Path));
        history.Execute(new ListEntryCommand("general", options, constants.Path, ListEntryCommand.Remove, 0));
        Assert.Equal(before + 1, PropertyModel.Describe(root).Count(r => r.Removable && r.Path.StartsWith(constants.Path + "[", StringComparison.Ordinal)));
        history.Undo();
        history.Undo();
        history.Undo();
        Assert.Equal(original, TestData.Bytes(data));

        // A list with its own editor and a move past the end are refused.
        Assert.Throws<InvalidOperationException>(() => history.Execute(new ListEntryCommand("objects", ResourceTypes.NameOf(data.GameObjects[0]), "Events", ListEntryCommand.Remove, 0)));
        Assert.Throws<InvalidOperationException>(() => history.Execute(new ListEntryCommand("general", options, constants.Path, ListEntryCommand.Up, 0)));
    }

    [Fact]
    public void AReferenceFieldIsSetByExplorerKeyAndClearedWithUndoAndRefusesAWrongType()
    {
        using var data = TestData.Create(1);
#pragma warning disable CA2000 // the data model owns what is added to it
        data.Sprites.Add(new G3MLib.DataFile.Models.GameMakerSprite { Name = data.Strings.MakeString("spr_a") });
#pragma warning restore CA2000
        var history = new EditHistory(data);
        var obj = data.GameObjects[0];
        var row = PropertyModel.Describe(obj).Single(r => r.Name == "Sprite");
        Assert.True(row.Reference);

        history.Execute(new SetPropertyCommand("objects", "obj_0", "Sprite", "sprites:spr_a"));
        Assert.Same(data.Sprites[0], obj.Sprite);
        Assert.Equal("sprites:spr_a", PropertyModel.Describe(obj).Single(r => r.Name == "Sprite").Target);
        history.Execute(new SetPropertyCommand("objects", "obj_0", "Sprite", ""));
        Assert.Null(obj.Sprite);
        history.Undo();
        Assert.Same(data.Sprites[0], obj.Sprite);
        history.Undo();
        Assert.Null(obj.Sprite);

        Assert.Throws<InvalidOperationException>(() => history.Execute(new SetPropertyCommand("objects", "obj_0", "Sprite", "scripts:scr_0")));
    }

    [Fact]
    public void NullEntriesAreListedOnlyWhenAskedAndAreNotCounted()
    {
        using var data = TestData.Create(2);
        data.GameObjects.Insert(1, null!);
        var model = new ExplorerModel(key => key);
        model.Load(data);
        Assert.DoesNotContain(model.Rows, r => r.Kind == ExplorerRowKind.Null);

        model.ShowNulls = true;
        var objects = model.Rows.Where(r => r.Key == "objects" && r.Kind != ExplorerRowKind.Type).ToList();
        Assert.Equal([ExplorerRowKind.Item, ExplorerRowKind.Null, ExplorerRowKind.Item], objects.Select(r => r.Kind));
        Assert.Equal("[1] null", objects[1].Text);
        Assert.Equal(2, model.Rows.Single(r => r.Kind == ExplorerRowKind.Type && r.Key == "objects").Count);
        Assert.False(objects[1].IsHeader);

        model.SetFilter("obj");
        Assert.DoesNotContain(model.Rows, r => r.Kind == ExplorerRowKind.Null);
    }

    [Fact]
    public void TheGameGroupHoldsTheGeneralInfoAndTheOptionsAndBothAreEditable()
    {
        using var data = TestData.Create(1);
        var model = new ExplorerModel(key => key);
        model.Load(data);
        Assert.Equal(ResourceTypes.All[0].Key, model.Rows.First(r => r.Kind == ExplorerRowKind.Type && !r.Key.StartsWith("quick", StringComparison.Ordinal)).Key);
        Assert.Equal(["General Info", "Game Options"], model.Rows.Where(r => r.IsItem && r.Key == "general").Select(r => r.Text).Take(2));

        var history = new EditHistory(data);
        var options = PropertyModel.FindRoot(data, "general", ResourceTypes.GameOptionsName)!;
        var flag = PropertyModel.Describe(options).First(r => r.Kind == PropertyKind.Bool && !r.ReadOnly);
        var before = PropertyModel.Read(options, flag.Path);
        history.Execute(new SetPropertyCommand("general", ResourceTypes.GameOptionsName, flag.Path, before == "True" ? "False" : "True"));
        Assert.NotEqual(before, PropertyModel.Read(options, flag.Path));
        history.Undo();
        Assert.Equal(before, PropertyModel.Read(options, flag.Path));
    }

    [Fact]
    public void TextThatDoesNotFitIsRefusedAndChangesNothing()
    {
        using var data = TestData.Create(1);
        var history = new EditHistory(data);
        var before = TestData.Bytes(data);
        var row = PropertyModel.Describe(data.GameObjects[0]).First(r => r.Kind == PropertyKind.Whole && !r.ReadOnly);
        Assert.Throws<InvalidOperationException>(() => history.Execute(new SetPropertyCommand("objects", "obj_0", row.Path, "not a number")));
        Assert.Empty(history.Items);
        Assert.Equal(before, TestData.Bytes(data));
    }

    [Fact]
    public void AStringPropertyPicksATableEntryAndUndoPutsTheOldOneBack()
    {
        using var data = TestData.Create(1);
        var history = new EditHistory(data);
        var row = PropertyModel.Describe(data.GeneralInfo).First(r => r.Kind == PropertyKind.GmString && !r.ReadOnly && r.Name == "DisplayName");
        var old = PropertyModel.ReadRaw(data.GeneralInfo, row.Path);

        history.Execute(new SetPropertyCommand("general", ResourceTypes.GeneralInfoName, row.Path, "My Game"));
        Assert.Equal("My Game", data.GeneralInfo.DisplayName.Content);
        Assert.Contains(data.Strings, s => s.Content == "My Game");
        history.Undo();
        Assert.Same(old, data.GeneralInfo.DisplayName);
    }
}

/// <summary>
/// Round trip on a real game: writing every editable number, flag, enum and text back with its own text changes nothing.
/// String table properties are left out: choosing a table entry by content may pick an equal duplicate, which is the same text but another index.
/// </summary>
public sealed class PropertyCorpusTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SettingEveryPropertyToItsOwnTextLeavesTheDataFileUnchanged()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var stream = File.OpenRead(file);
        using var data = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var before = TestData.Bytes(data);

        int rows = 0, failed = 0;
        foreach (var type in ResourceTypes.All.Where(t => !t.IsData))
        {
            foreach (var item in (type.Items(data) ?? []).Take(40))
            {
                var name = ResourceTypes.NameOf(item);
                var snapshot = PropertyModel.Describe(item);
                foreach (var row in snapshot.Where(r => !r.ReadOnly && r.Kind is not (PropertyKind.Group or PropertyKind.List or PropertyKind.Info or PropertyKind.GmString)))
                {
                    rows++;
                    try
                    {
                        PropertyModel.Set(data, item, row.Path, row.Value);
                        if (PropertyModel.Read(item, row.Path) != row.Value) { failed++; output.WriteLine($"{type.Key}:{name} {row.Path}: '{row.Value}' read back as '{PropertyModel.Read(item, row.Path)}'"); }
                    }
                    catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or InvalidCastException)
                    {
                        failed++;
                        output.WriteLine($"{type.Key}:{name} {row.Path} = '{row.Value}': {e.GetType().Name}");
                    }
                }
            }
        }

        output.WriteLine($"{Path.GetFileName(Path.GetDirectoryName(file))}: {rows} properties, {failed} failed");
        Assert.Equal(0, failed);
        Assert.Equal(before, TestData.Bytes(data));
    }
}
