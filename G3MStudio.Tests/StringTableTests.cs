using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class StringTableTests
{
    [Fact]
    public void CsvKeepsQuotesCommasAndLineBreaks()
    {
        (int, string)[] rows = [(0, "plain"), (1, "a, \"b\""), (7, "two\r\nlines"), (8, "")];
        Assert.Equal(rows, StringTable.ParseCsv(StringTable.ToCsv(rows)));
        Assert.Throws<InvalidDataException>(() => StringTable.ParseCsv("index,text\r\nabc,\"x\"\r\n"));
        Assert.Throws<InvalidDataException>(() => StringTable.ParseCsv("0,\"open"));
    }

    [Fact]
    public async Task AddDeleteReplaceAndImportAreOneUndoStepEach()
    {
        using var data = await GameMakerIO.ReadAsync(Corpus.Files(Corpus.Folder).First(), cancellationToken: TestContext.Current.CancellationToken);
        var count = data.Strings.Count;
        var add = new StringAddCommand("zz_probe_string");
        add.Apply(data);
        Assert.Equal(count + 1, data.Strings.Count);
        Assert.Throws<InvalidOperationException>(() => new StringAddCommand("zz_probe_string").Apply(data));
        var delete = new StringDeleteCommand(count);
        delete.Apply(data);
        Assert.Equal(count, data.Strings.Count);
        delete.Revert(data);
        Assert.Equal("zz_probe_string", data.Strings[count].Content);

        var replace = StringTable.ReplaceEdits(data, [count], new Regex("probe", RegexOptions.None, TimeSpan.FromSeconds(1)), "swap")!;
        replace.Apply(data);
        Assert.Equal("zz_swap_string", data.Strings[count].Content);
        replace.Revert(data);
        Assert.Equal("zz_probe_string", data.Strings[count].Content);

        var import = StringTable.ImportEdits(data, [(count, "zz_csv"), (count + 50, "outside"), (0, data.Strings[0].Content ?? "")], out var skipped)!;
        Assert.Equal(1, skipped);
        import.Apply(data);
        Assert.Equal("zz_csv", data.Strings[count].Content);
        import.Revert(data);
        Assert.Equal("zz_probe_string", data.Strings[count].Content);
        add.Revert(data);
        Assert.Equal(count, data.Strings.Count);
    }
}
