using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A search opens the resource types that match, but the data lists (Strings, Variables, ...) only when the setting asks for it.</summary>
public sealed class ExplorerSearchDataTests
{
    private static ExplorerModel Searched(GameMakerDataHolder holder, bool openData)
    {
        var model = new ExplorerModel(key => key) { OpenDataOnSearch = openData };
        model.Load(holder.Data);
        model.SetFilter("obj_0");
        return model;
    }

    private sealed class GameMakerDataHolder : IDisposable
    {
        public G3MLib.DataFile.GameMakerData Data { get; } = TestData.Create(2, true);

        public void Dispose() => Data.Dispose();
    }

    [Fact]
    public void ASearchLeavesTheDataGroupFoldedByDefaultAndKeepsTheResourceTypesOpen()
    {
        using var holder = new GameMakerDataHolder();
        var model = Searched(holder, false);

        var strings = Assert.Single(model.Rows, r => r.Key == "strings" && r.Kind == ExplorerRowKind.Type);
        Assert.False(strings.Expanded);
        Assert.True(strings.Count > 0); // the matches are counted, one click shows them
        Assert.DoesNotContain(model.Rows, r => r.Key == "strings" && r.IsItem);
        Assert.Contains(model.Rows, r => r.IsItem && r.Key == "objects");

        model.Toggle("strings");
        Assert.Contains(model.Rows, r => r.Key == "strings" && r.IsItem);
    }

    [Fact]
    public void ASearchStillOpensCodeByDefault()
    {
        using var holder = new GameMakerDataHolder();
        var model = Searched(holder, false);
        model.SetFilter("probe");

        Assert.True(model.Rows.Single(r => r.Key == "code" && r.Kind == ExplorerRowKind.Type).Expanded);
        Assert.Contains(model.Rows, r => r.IsItem && r.Key == "code");
    }

    [Fact]
    public void TheSettingOpensTheDataGroupOnSearch()
    {
        using var holder = new GameMakerDataHolder();
        var model = Searched(holder, true);

        Assert.True(model.Rows.Single(r => r.Key == "strings" && r.Kind == ExplorerRowKind.Type).Expanded);
        Assert.Contains(model.Rows, r => r.Key == "strings" && r.IsItem);
    }

    [Fact]
    public void TheSettingThatHidesTheDataGroupRemovesItAndDefaultsAreOff()
    {
        using var holder = new GameMakerDataHolder();
        var model = Searched(holder, false);
        model.HideData = true;

        Assert.DoesNotContain(model.Rows, r => r.Key == ExplorerModel.DataGroup);
        Assert.False(new AppSettings().ExplorerOpenDataOnSearch);
        Assert.False(new AppSettings().ExplorerHideData);
    }

    [Fact]
    public void ADataTypeCanBeHiddenByTheFilterLikeAResourceType()
    {
        using var holder = new GameMakerDataHolder();
        var model = Searched(holder, true);
        model.Quick = model.Quick with { Filter = model.Quick.Filter.Toggle("strings") };

        Assert.DoesNotContain(model.Rows, r => r.Key == "strings");
    }
}
