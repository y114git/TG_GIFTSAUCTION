using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class TileCsvTests
{
    [Fact]
    public void ARoundTripChangesNothingAndAnEditChangesOnlyThatCell()
    {
        uint[][] rows = [[0, 5, 6], [7, 0, 0x10000008]];
        var csv = TileCsv.Write(rows);
        Assert.Empty(TileCsv.Changes(rows, csv));
        Assert.Equal([(2, 0, 9u)], TileCsv.Changes(rows, csv.Replace("5,6", "5,9", StringComparison.Ordinal)));
    }

    [Fact]
    public void ASizeMismatchOrABadValueIsRefused()
    {
        uint[][] rows = [[0, 1], [2, 3]];
        Assert.Throws<InvalidDataException>(() => TileCsv.Changes(rows, "0,1"));
        Assert.Throws<InvalidDataException>(() => TileCsv.Changes(rows, "0,1,2\n3,4,5"));
        Assert.Throws<InvalidDataException>(() => TileCsv.Changes(rows, "0,x\n1,2"));
    }
}
