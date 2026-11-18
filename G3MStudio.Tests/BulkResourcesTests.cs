using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class BulkResourcesTests
{
    [Fact]
    public void SeveralObjectsDeleteAsOneGroupAndUndoTogether()
    {
        using var data = TestData.Create(4);
        var keys = new[] { "objects:obj_0", "objects:obj_2", "objects:obj_0" };
        var items = BulkResources.Split(keys);
        Assert.Equal([("objects", "obj_0"), ("objects", "obj_2")], items);

        var delete = BulkResources.Delete([.. items.Select(i => (i.Type, i.Name, false))]);
        delete.Apply(data);
        Assert.Equal(["obj_1", "obj_3"], data.GameObjects.Select(o => o.Name.Content));
        delete.Revert(data);
        Assert.Equal(4, data.GameObjects.Count);
    }

    [Fact]
    public void CopiesGetFreeNamesAndOnlyCopyableTypesAreCopied()
    {
        using var data = TestData.Create(2);
        var taken = new HashSet<string>();
        Assert.Equal("obj_0_copy", BulkResources.CopyName(data, "obj_0", taken));
        Assert.Equal("obj_0_copy2", BulkResources.CopyName(data, "obj_0", taken));

        var copy = BulkResources.Duplicate(data, [("objects", "obj_0"), ("objects", "obj_1"), ("rooms", "r")], out var keys);
        Assert.Equal(["objects:obj_0_copy", "objects:obj_1_copy"], keys);
        copy.Apply(data);
        Assert.Equal(4, data.GameObjects.Count);
        copy.Revert(data);
        Assert.Equal(2, data.GameObjects.Count);
    }
}
