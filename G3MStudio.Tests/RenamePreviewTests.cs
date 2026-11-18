using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RenamePreviewTests
{
    [Fact]
    public void ThePreviewListsTheChangedNamesWithoutChangingThem()
    {
        using var data = TestData.Create();
        var preview = RenamePreview.Of(data, "objects", "obj_0", "obj_hero", null);
        Assert.Null(preview.Refusal);
        Assert.Contains("obj_0 > obj_hero", preview.Changes);
        Assert.Equal("obj_0", data.GameObjects[0].Name.Content);
    }

    [Fact]
    public void ATakenOrInvalidNameIsRefusedInThePreview()
    {
        using var data = TestData.Create();
        Assert.NotNull(RenamePreview.Of(data, "objects", "obj_0", "obj_1", null).Refusal);
        Assert.NotNull(RenamePreview.Of(data, "objects", "obj_0", "1 bad", null).Refusal);
        Assert.NotNull(RenamePreview.Of(data, "objects", "missing", "x", null).Refusal);
    }
}
