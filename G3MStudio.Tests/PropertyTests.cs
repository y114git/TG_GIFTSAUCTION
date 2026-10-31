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
    public void TheGameGroupHoldsTheGeneralInfoAndTheOptionsAndBothAreEditable()
    {
        using var data = TestData.Create(1);
        var model = new ExplorerModel(key => key);
        model.Load(data);
        Assert.Equal(ResourceTypes.All[0].Key, model.Rows[0].Key);
        Assert.Equal(["General Info", "Game Options"], model.Rows.Where(r => r.IsItem && r.Key == "general").Select(r => r.Text));

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
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
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
