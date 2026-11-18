using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class VariableCountsTests
{
    [Fact]
    public void TheCountersChangeUndoAndReplayFromTheJournal()
    {
        using var data = TestData.Create(1);
        data.VarCount1 = 1;
        data.VarCount2 = 2;
        data.MaxLocalVarCount = 3;
        var history = new EditHistory(data);
        var command = new VariableCountsCommand(7, 9, 11);
        history.Execute(command);
        Assert.Equal((7u, 9u, 11u), (data.VarCount1, data.VarCount2, data.MaxLocalVarCount));
        history.Undo();
        Assert.Equal((1u, 2u, 3u), (data.VarCount1, data.VarCount2, data.MaxLocalVarCount));
        history.Redo();
        history.Undo();
        history.Execute(EditCommand.Create(command.Kind, command.Args));
        Assert.Equal((7u, 9u, 11u), (data.VarCount1, data.VarCount2, data.MaxLocalVarCount));
    }
}
