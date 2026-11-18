using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class EntryScriptsTests
{
    [Fact]
    public async Task ACodeEntryIsAddedToAndRemovedFromTheStartScriptsAndEachStepUndoes()
    {
        var folder = Corpus.Folder;
        GameMakerData? data = null;
        foreach (var file in Corpus.Files(folder))
        {
            data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (data.GlobalInitScripts is { Count: > 0 } && data.Code.Count > data.GlobalInitScripts.Count) break;
            data.Dispose();
            data = null;
        }

        Assert.False(data is null, "no game with start scripts");
        using var _ = data;
        var list = data!.GlobalInitScripts;
        var before = list.Count;
        var listed = list.Select(EntryScriptsCommand.Describe).ToHashSet();
        var code = data.Code.First(c => !listed.Contains(c.Name.Content!)).Name.Content!;

        var add = new EntryScriptsCommand(EntryScriptsCommand.GlobalInit, EntryScriptsCommand.Add, code);
        add.Apply(data);
        Assert.Equal(before + 1, list.Count);
        Assert.Equal(code, EntryScriptsCommand.Describe(list[^1]));
        Assert.Throws<InvalidOperationException>(() => new EntryScriptsCommand(EntryScriptsCommand.GlobalInit, EntryScriptsCommand.Add, code).Apply(data));

        var remove = new EntryScriptsCommand(EntryScriptsCommand.GlobalInit, EntryScriptsCommand.Remove, EntryScriptsCommand.Describe(list[0]));
        var first = EntryScriptsCommand.Describe(list[0]);
        remove.Apply(data);
        Assert.Equal(before, list.Count);
        remove.Revert(data);
        Assert.Equal(first, EntryScriptsCommand.Describe(list[0]));
        add.Revert(data);
        Assert.Equal(before, list.Count);

        var again = (EntryScriptsCommand)EditCommand.Create(EntryScriptsCommand.Name, add.Args);
        Assert.Equal(add.Args, again.Args);
    }
}
