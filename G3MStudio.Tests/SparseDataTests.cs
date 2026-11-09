using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Files with no entries, and the older fixtures: every view of the model must cope without exceptions.</summary>
public sealed class SparseDataTests
{
    private static void Walk(GameMakerData data)
    {
        var model = new ExplorerModel(key => key);
        model.Load(data);
        model.ShowNulls = true;
        model.Reload();
        _ = ResourceTypes.Counts(data);
        foreach (var type in ResourceTypes.All)
            foreach (var item in type.Items(data) ?? [])
            {
                if (item is null) continue;
                var rows = PropertyModel.Describe(item);
                Assert.NotNull(rows);
                Assert.True(ResourceTypes.Find(data, ResourceTypes.KeyOf(type.Key, ResourceTypes.NameOf(item))) is not null || ResourceTypes.NameOf(item).Length == 0 || type.Key == "general");
            }
    }

    [Fact]
    public void AnEmptyDataFileCanBeBrowsed()
    {
        using var data = TestData.Create(0);
        Walk(data);
    }

    [Theory]
    [InlineData("pizza")]
    [InlineData("uty")]
    [InlineData("deltarune")]
    public async Task EveryEntryOfAFixtureCanBeDescribed(string name)
    {
        var path = Path.Combine(Corpus.Fixtures, name, "data.win");
        using var data = await GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        Walk(data);
    }

    [Theory]
    [InlineData("pizza")]
    [InlineData("uty")]
    [InlineData("deltarune")]
    public async Task CodeOfAFixtureDecompilesDisassemblesAndRecompilesWithUndo(string name)
    {
        var path = Path.Combine(Corpus.Fixtures, name, "data.win");
        using var data = await GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var history = new EditHistory(data);
        var tried = 0;
        foreach (var entry in data.Code.Where(c => c.ParentEntry is null).Take(8).ToList())
        {
            var entryName = entry.Name.Content!;
            var text = CodeText.Decompile(data, entryName);
            Assert.NotEmpty(CodeText.Disassemble(data, entryName));
            var before = entry.Instructions.Count;
            history.Execute(new EditCodeCommand(entryName, text));
            history.Undo();
            Assert.Equal(before, data.Code.ByName(entryName)!.Instructions.Count);
            tried++;
        }

        Assert.False(history.IsDirty);
        Assert.True(tried > 0 || data.Code.Count == 0);
    }

    /// <summary>Real games of older GameMaker versions (GMS1 era, 2.x): G3MSTUDIO_OLD_DATA may list other data files, separated by semicolons; without it the corpus fixtures are used.</summary>
    [Fact]
    public async Task OlderGamesCanBeBrowsedAndEditedWithUndo()
    {
        var list = Environment.GetEnvironmentVariable("G3MSTUDIO_OLD_DATA");
        var files = string.IsNullOrEmpty(list) ? Directory.EnumerateFiles(Corpus.Folder, "data.win", SearchOption.AllDirectories) : list.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists);
        foreach (var file in files)
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            Walk(data);
            var history = new EditHistory(data);
            var script = data.Scripts.FirstOrDefault(x => x is not null);
            if (script is not null)
            {
                var name = script.Name.Content!;
                history.Execute(new RenameResourceCommand("scripts", name, name + "_x"));
                history.Undo();
                Assert.Equal(name, script.Name.Content);
            }

            var entry = data.Code.FirstOrDefault(c => c is { ParentEntry: null });
            if (entry is not null)
            {
                var text = CodeText.Decompile(data, entry.Name.Content!);
                history.Execute(new EditCodeCommand(entry.Name.Content!, text));
                history.Undo();
            }
        }
    }
}
