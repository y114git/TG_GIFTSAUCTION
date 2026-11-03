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
