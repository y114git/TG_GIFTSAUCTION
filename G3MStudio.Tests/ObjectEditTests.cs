using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ObjectEditTests
{
    private static GameMakerData WithEvent()
    {
        var data = TestData.Create(3, withCode: true);
        new ObjectEventCommand("obj_0", ObjectEventCommand.Add, (uint)EventType.Step, 0).Apply(data);
        new EditCodeCommand(ObjectEventCommand.CodeName("obj_0", (uint)EventType.Step, 0), "x = 5;").Apply(data);
        return data;
    }

    private static string? CodeOf(GameMakerData data, string obj, EventType type, uint subtype) =>
        data.GameObjects.First(o => o.Name.Content == obj).Events[(int)type].FirstOrDefault(e => e.EventSubtype == subtype)?.Actions.FirstOrDefault()?.CodeId?.Name?.Content;

    [Fact]
    public void CopyToAnotherObjectCarriesTheCodeAndUndoRemovesIt()
    {
        using var data = WithEvent();
        var copy = EditCommand.Create(ObjectEventCopyCommand.Name, new ObjectEventCopyCommand("obj_0", (uint)EventType.Step, 0, "obj_1", 0).Args);
        var history = new EditHistory(data);
        history.Execute(copy);
        var entry = Assert.IsType<string>(CodeOf(data, "obj_1", EventType.Step, 0));
        Assert.Contains("x = 5", CodeText.Decompile(data, entry), StringComparison.Ordinal);
        history.Undo();
        Assert.Null(CodeOf(data, "obj_1", EventType.Step, 0));
        Assert.NotNull(CodeOf(data, "obj_0", EventType.Step, 0));
        history.Redo();
        Assert.NotNull(CodeOf(data, "obj_1", EventType.Step, 0));
    }

    [Fact]
    public void DuplicateTakesTheNextFreeSubtypeAndRefusesWhenAllAreTaken()
    {
        using var data = WithEvent();
        var owner = data.GameObjects.First(o => o.Name.Content == "obj_0");
        var free = ObjectEventCopyCommand.FreeSubtype(data, owner, EventType.Step, 0);
        Assert.Equal(1u, free);
        new ObjectEventCopyCommand("obj_0", (uint)EventType.Step, 0, "obj_0", free!.Value).Apply(data);
        Assert.Equal(2u, ObjectEventCopyCommand.FreeSubtype(data, owner, EventType.Step, 0));
        Assert.Throws<InvalidOperationException>(() => new ObjectEventCopyCommand("obj_0", (uint)EventType.Step, 0, "obj_0", 1).Apply(data));
    }

    [Fact]
    public void ReorderMovesWithinTheTypeAndUndoRestores()
    {
        using var data = WithEvent();
        new ObjectEventCommand("obj_0", ObjectEventCommand.Add, (uint)EventType.Step, 1).Apply(data);
        var list = data.GameObjects.First(o => o.Name.Content == "obj_0").Events[(int)EventType.Step];
        var move = EditCommand.Create(ObjectEventOrderCommand.Name, new ObjectEventOrderCommand("obj_0", (uint)EventType.Step, 1, -1).Args);
        move.Apply(data);
        Assert.Equal([1u, 0u], list.Select(e => e.EventSubtype));
        move.Revert(data);
        Assert.Equal([0u, 1u], list.Select(e => e.EventSubtype));
    }

    [Fact]
    public void ParentSetsClearsAndRefusesALoop()
    {
        using var data = TestData.Create(3);
        var (a, b) = (data.GameObjects[0], data.GameObjects[1]);
        var set = EditCommand.Create(ObjectParentCommand.Name, new ObjectParentCommand("obj_1", "obj_0").Args);
        set.Apply(data);
        Assert.Same(a, b.ParentId);
        Assert.Throws<InvalidOperationException>(() => new ObjectParentCommand("obj_0", "obj_1").Apply(data));
        Assert.Throws<InvalidOperationException>(() => new ObjectParentCommand("obj_0", "obj_0").Apply(data));
        set.Revert(data);
        Assert.Null(b.ParentId);
    }
}
