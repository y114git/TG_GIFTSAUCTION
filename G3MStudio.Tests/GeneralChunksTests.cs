using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class GeneralChunksTests
{
    [Fact]
    public async Task TheTagsFeatureFlagsAndLanguageChunksAreGeneralEntriesWithPropertyRows()
    {
        var folder = Corpus.Folder;
        var seen = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var entry in ResourceTypes.All.Single(t => t.Key == "general").Items(data)!)
            {
                var name = ResourceTypes.NameOf(entry);
                Assert.Same(entry, ResourceTypes.Find(data, ResourceTypes.KeyOf("general", name)));
                if (entry is not IncludedFilesEntry) Assert.NotEmpty(PropertyModel.Describe(entry)); // that entry has its own view, no property rows
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
        Assert.Equal(("0", 0), (row.Value, row.Count));
    }
}
