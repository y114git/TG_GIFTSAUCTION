using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class GeneralChunksTests
{
    [Fact]
    public async Task TheTagsFeatureFlagsAndLanguageChunksAreGeneralEntriesWithPropertyRows()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var seen = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var entry in ResourceTypes.All.Single(t => t.Key == "general").Items(data)!)
            {
                var name = ResourceTypes.NameOf(entry);
                Assert.Same(entry, ResourceTypes.Find(data, ResourceTypes.KeyOf("general", name)));
                Assert.NotEmpty(PropertyModel.Describe(entry));
                seen.Add(name);
            }
        }

        Assert.Contains(ResourceTypes.GeneralInfoName, seen);
    }

    [Fact]
    public void ADictionaryRowShowsItsCountNotItsTypeName()
    {
        using var data = GameMakerData.CreateNew2026LTS();
        var tags = ResourceTypes.Find(data, ResourceTypes.KeyOf("general", ResourceTypes.TagsName))!;
        var row = PropertyModel.Describe(tags).Single(r => r.Name == "AssetTags");
        Assert.Equal("0 items", row.Value);
    }
}
