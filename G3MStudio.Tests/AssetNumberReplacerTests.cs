using G3MLib.DataFile;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class AssetNumberReplacerTests
{
    private static async Task<(GameMakerData Data, AssetNames Names, AssetParameters Parameters)> OpenAsync()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: false);
        var data = await GameMakerIO.ReadAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        return (data, new AssetNames(data), AssetParameters.Builtin(GmlReference.Default));
    }

    private static string Replace(string text, AssetNames names, AssetParameters parameters, Func<string, bool>? allowed = null) =>
        AssetNumberReplacer.Apply(text, AssetNumberReplacer.Find(text, names, parameters, allowed));

    [Fact]
    public async Task ANumberThatIsAWholeAssetArgumentBecomesTheName()
    {
        var (data, names, parameters) = await OpenAsync();
        using (data)
        {
            Assert.Equal("instance_create_layer(0, 0, \"Instances\", obj_1);", Replace("instance_create_layer(0, 0, \"Instances\", 1);", names, parameters));
            Assert.Equal("if (object_index == obj_2) { n = 5; }", Replace("if (object_index == 2) { n = 5; }", names, parameters));
        }
    }

    [Theory]
    [InlineData("instance_create_layer(0, 0, \"Instances\", 1 + 1);")]
    [InlineData("instance_create_layer(0, 0, \"Instances\", -1);")]
    [InlineData("instance_create_layer(0, 0, \"Instances\", 99);")]
    [InlineData("instance_create_layer(0, 0, \"Instances\", 1.5);")]
    [InlineData("instance_create_layer(0, 0, \"Instances\", 0x1);")]
    [InlineData("show_debug_message(1);")]
    [InlineData("x = [1, 2]; y = (1);")]
    public async Task OtherNumbersAreLeftAlone(string text)
    {
        var (data, names, parameters) = await OpenAsync();
        using (data) Assert.Equal(text, Replace(text, names, parameters));
    }

    [Fact]
    public async Task FunctionsOfTheGameAreLearnedFromCallsThatPassNames()
    {
        var (data, names, parameters) = await OpenAsync();
        using (data)
        {
            Assert.Equal("spawn(2);", Replace("spawn(2);", names, parameters));
            parameters.Learn("spawn(obj_0); spawn(obj_1);", names);
            parameters.Seal();
            Assert.Equal("spawn(obj_2);", Replace("spawn(2);", names, parameters));
        }
    }

    [Fact]
    public async Task AFunctionThatTakesTwoAssetTypesIsNotLearned()
    {
        var (data, names, parameters) = await OpenAsync();
        using (data)
        {
            parameters.Learn("mix(obj_0); mix(scr_1);", names);
            parameters.Seal();
            Assert.Equal("mix(2);", Replace("mix(2);", names, parameters));
        }
    }

    [Fact]
    public async Task OnlyTheAllowedTypesAreReplaced()
    {
        var (data, names, parameters) = await OpenAsync();
        using (data) Assert.Equal("instance_create_layer(0, 0, \"Instances\", 1);", Replace("instance_create_layer(0, 0, \"Instances\", 1);", names, parameters, type => type != "objects"));
    }
}
