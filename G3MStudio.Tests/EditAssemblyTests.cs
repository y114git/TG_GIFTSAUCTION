using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class EditAssemblyTests
{
    [Fact]
    public async Task ReassemblingTheDisassemblyKeepsTheEntryAndAnEditChangesItAndUndoes()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var code = data.Code.Where(c => c.ParentEntry is null && c.Instructions.Count is > 2 and < 40).OrderBy(c => c.Instructions.Count).First();
        var name = code.Name.Content!;
        var text = CodeText.Disassemble(data, name);
        var length = code.Length;

        var same = new EditAssemblyCommand(name, text);
        same.Apply(data);
        Assert.Equal(text, CodeText.Disassemble(data, name));
        Assert.Equal(length, code.Length);

        var broken = new EditAssemblyCommand(name, text + "\nnot_an_instruction 1 2 3");
        Assert.Throws<InvalidOperationException>(() => broken.Apply(data));
        Assert.Equal(text, CodeText.Disassemble(data, name));

        var again = (EditAssemblyCommand)EditCommand.Create(EditAssemblyCommand.Name, same.Args);
        Assert.Equal(same.Args, again.Args);
        same.Revert(data);
        Assert.Equal(text, CodeText.Disassemble(data, name));
    }
}
