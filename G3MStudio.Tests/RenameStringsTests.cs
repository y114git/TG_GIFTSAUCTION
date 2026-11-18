using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;
using G3MLib.Analyzer.Decompiler;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A resource rename takes code string literals equal to the old name along, unless it is told not to; undo restores both cases byte for byte.</summary>
public sealed class RenameStringsTests
{
    private static string Decompile(GameMakerData data) =>
        new DecompileContext(new GlobalDecompileContext(data), data.Code.ByName("gml_Script_probe"), data.ToolInfo.DecompilerSettings).DecompileToString();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheLiteralFollowsTheNameOnlyWhenAsked(bool renameStrings)
    {
        using var data = TestData.Create(1, withCode: true);
        var history = new EditHistory(data);
        history.Execute(new EditCodeCommand("gml_Script_probe", "return \"obj_0\";"));
        var original = TestData.Bytes(data);

        history.Execute(new RenameResourceCommand("objects", "obj_0", "obj_a", renameStrings));
        Assert.Equal("obj_a", data.GameObjects[0].Name.Content);
        var code = Decompile(data);
        Assert.Contains(renameStrings ? "\"obj_a\"" : "\"obj_0\"", code, StringComparison.Ordinal);

        history.Undo();
        Assert.Equal(original, TestData.Bytes(data));
        Assert.Contains("\"obj_0\"", Decompile(data), StringComparison.Ordinal);

        history.Redo();
        Assert.Contains(renameStrings ? "\"obj_a\"" : "\"obj_0\"", Decompile(data), StringComparison.Ordinal);
        Assert.Equal(renameStrings, !EditCommand.Create(RenameResourceCommand.Name, history.Items[^1].Args).Args.ContainsKey("strings")); // the journal keeps the choice
    }
}
