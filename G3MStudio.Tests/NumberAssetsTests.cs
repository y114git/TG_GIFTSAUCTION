using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class NumberAssetsTests
{
    [Theory]
    [InlineData("x = 12;", 4, 12)]
    [InlineData("x = 12;", 5, 12)]
    [InlineData("x = 12;", 6, 12)]
    [InlineData("sprite2", 6, null)]
    [InlineData("x = 1.5", 4, null)]
    [InlineData("x = 1.5", 6, null)]
    [InlineData("a_1 = 3", 2, null)]
    [InlineData("x = ;", 4, null)]
    public void ANumberIsFoundOnlyWhenItStandsAlone(string text, int offset, int? expected) => Assert.Equal(expected, NumberAssets.NumberAt(text, offset));

    [Theory]
    [InlineData("c = $0000FF;", 6, 255, true, 6)]
    [InlineData("c = 0xFFFFFF;", 8, 16777215, true, 6)]
    [InlineData("c = 16777215;", 6, 16777215, false, 0)]
    [InlineData("c = $FF;", 5, 255, true, 2)]
    [InlineData("c = $7FFFFFFFFFFFFFFF;", 8, long.MaxValue, true, 16)]
    public void HexAndDecimalLiteralsAreRead(string text, int offset, long value, bool hex, int digits) => Assert.Equal(new NumberLiteral(value, hex, digits), NumberAssets.LiteralAt(text, offset));

    [Theory]
    [InlineData("c = 0x;", 5)]
    [InlineData("c = $;", 5)]
    [InlineData("c = abc;", 5)]
    [InlineData("c = obj_1;", 8)]
    [InlineData("c = $FFFFFFFFFFFFFFFF;", 8)]
    [InlineData("c = 0xFFFFFFFFFFFFFFFF;", 10)]
    [InlineData("c = 99999999999999999999;", 8)]
    public void WordsEmptyHexAndTooLargeNumbersAreNotLiterals(string text, int offset) => Assert.Null(NumberAssets.LiteralAt(text, offset));

    [Fact]
    public async Task TheAssetsAtAnIndexComeFromEveryTypeThatHasOne()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2, withCode: false);
        using var data = await G3MLib.DataFile.GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var found = NumberAssets.Find(data, 0);
        Assert.Contains(found, a => a.Type == "objects");
        Assert.Empty(NumberAssets.Find(data, 5000));
        Assert.Empty(NumberAssets.Find(data, -1));
    }
}
