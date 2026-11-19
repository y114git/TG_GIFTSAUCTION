using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class MacroAcrossEntriesTests
{
    private const string Declare = "#macro SPEED 5\n#macro LIMIT (SPEED * 2)\nreturn LIMIT;";
    private const string Use = "x = LIMIT + SPEED;";

    private static (GameMakerData Data, EditHistory History) Open()
    {
        var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "scripts", "scr_decl"));
        history.Execute(ResourceTemplates.New(data, "scripts", "scr_user"));
        return (data, history);
    }

    [Fact]
    public void AMacroOneEntryDeclaresIsExpandedWhenAnotherEntryIsCompiledAndUndoForgetsIt()
    {
        var (data, history) = Open();
        history.Execute(new EditCodeCommand("gml_Script_scr_decl", Declare));
        history.Execute(new EditCodeCommand("gml_Script_scr_user", Use));
        var text = CodeText.Decompile(data, "gml_Script_scr_user");
        Assert.DoesNotContain("LIMIT", text, StringComparison.Ordinal); // compiled as the macro, not as a variable of that name
        Assert.DoesNotContain("SPEED", text, StringComparison.Ordinal);

        history.Undo();
        history.Undo();
        Assert.Empty(MacroRegistry.Of(data).Table());
        history.Redo();
        history.Redo();
        Assert.Equal(["LIMIT", "SPEED"], MacroRegistry.Of(data).Table().Keys.Order());
        Assert.Empty(MacroRegistry.Of(data).Table("gml_Script_scr_decl"));

        history.Undo();
        history.Undo();
        history.Execute(new EditCodeCommand("gml_Script_scr_user", Use));
        Assert.Contains("LIMIT", CodeText.Decompile(data, "gml_Script_scr_user"), StringComparison.Ordinal); // no macro known: a plain variable
    }

    [Fact]
    public void TheLiveCheckTakesTheTableAndStillReportsRealErrors()
    {
        var (data, history) = Open();
        history.Execute(new EditCodeCommand("gml_Script_scr_decl", Declare));
        var macros = MacroRegistry.Of(data).Table("gml_Script_scr_user");
        Assert.Empty(LiveCheck.Parse(Use, macros));
        Assert.NotEmpty(LiveCheck.Parse("x = LIMIT + ;", macros));
    }
}
