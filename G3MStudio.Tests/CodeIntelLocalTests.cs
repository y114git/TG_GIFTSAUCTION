using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Find Usages and Go to Definition for names of one script, the parameter hint from /// comments, and names other code entries declare.</summary>
public sealed class CodeIntelLocalTests
{
    private const string Text = "enum State { idle, walk }\nvar hp = 1;\nhp += State.walk;\nfunction heal(amount) { return hp + amount; }\nshow_debug_message(hp);\n";

    [Fact]
    public async Task LocalsAndParametersAreFoundInTheirFunctionAndOtherNamesInAllCode()
    {
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = 0;
            // A var outside every function is not seen inside heal.
            Assert.Equal([2, 3, 5], tab.LocalUsages("hp")!.Select(h => h.Line));
            Assert.All(tab.LocalUsages("hp")!, h => Assert.Equal("gml_Script_probe", h.Entry));
            // Enums and their members are seen by all code.
            Assert.Null(tab.LocalUsages("walk"));
            Assert.Null(tab.LocalUsages("State"));
            Assert.Null(tab.LocalUsages("amount"));
            tab.Editor.CaretOffset = Text.IndexOf("return", StringComparison.Ordinal);
            Assert.Equal([4], tab.LocalUsages("amount")!.Select(h => h.Line).Distinct());
            Assert.Null(tab.LocalUsages("show_debug_message"));
            Assert.Null(tab.LocalUsages("idle_not_declared"));
            Assert.Equal(Text.IndexOf("walk", StringComparison.Ordinal), tab.DeclarationOffset("walk"));
            return Task.CompletedTask;
        }, Text);
    }

    [Fact]
    public async Task AVarOfOneFunctionIsNotTheSameNameInAnotherAndStaticsAndMacrosAreSearchedEverywhere()
    {
        const string text = "#macro LIMIT 3\nfunction a() { var i = 0; return i; }\nfunction b() { var i = LIMIT; return i; }\nfunction c() { static n = 0; n++; return n; }\n";
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = text.IndexOf("var i = LIMIT", StringComparison.Ordinal);
            Assert.Equal([3, 3], tab.LocalUsages("i")!.Select(h => h.Line));
            Assert.Null(tab.LocalUsages("LIMIT"));
            tab.Editor.CaretOffset = text.IndexOf("static", StringComparison.Ordinal);
            Assert.Null(tab.LocalUsages("n"));
            return Task.CompletedTask;
        }, text);
    }

    [Fact]
    public async Task TheParameterHintOfAFunctionOfTheScriptShowsItsDocComment()
    {
        const string text = "/// @description Moves the player\n/// @param {real} pace How fast to go\n/// @param {string} label The name shown\nfunction move(pace, label) { return pace; }\nmove(1, ";
        await CodeIntelUiTests.WithTab((tab, _, _) =>
        {
            _ = tab.Doc; // the hint reads the tokens of the current text
            tab.Editor.CaretOffset = text.Length;
            var (signature, argument) = tab.CallAtCaret()!.Value;
            Assert.Equal(1, argument);
            Assert.Equal("move(pace, label)", signature.Label);
            var help = GmlHelp.ActiveParameter(signature, argument);
            Assert.Contains("The name shown", help, StringComparison.Ordinal);
            Assert.Contains("Moves the player", help, StringComparison.Ordinal);
            return Task.CompletedTask;
        }, text);
    }

    [Fact]
    public async Task AMacroOfAnotherEntryIsKnownAndHasAHoverButNotInTheEntryThatDeclaresIt()
    {
        await CodeIntelUiTests.WithTab(async (tab, workspace, _) =>
        {
            workspace.Edit(new CodeEntryCommand("gml_Script_config"));
            workspace.Edit(new EditCodeCommand("gml_Script_config", "#macro TOP_SPEED 12\nenum Team { red, blue }\nvariable_instance_set(id, \"stamina\", 3);\n"));
            await GmlProjectSymbols.ReadyAsync(workspace);
            Assert.Equal("gml_Script_config", GmlProjectSymbols.Find(workspace, "TOP_SPEED")?.Entry);
            Assert.Null(GmlProjectSymbols.Find(workspace, "TOP_SPEED", except: "gml_Script_config"));

            var context = GmlLintSource.For(workspace, "gml_Script_probe");
            var issues = GmlLint.Analyze(GmlDoc.Parse("x = TOP_SPEED + stamina;\n"), context);
            Assert.DoesNotContain(issues, i => i.Diagnostic.Code is GmlLint.UnknownVariable);
            Assert.Contains(GmlLint.Analyze(GmlDoc.Parse("x = TOP_SPEDE;\n"), context), i => i.Diagnostic.Code == GmlLint.UnknownVariable);

            tab.Editor.Text = "x = TOP_SPEED;\n";
            var hover = tab.HoverTextAt(6) ?? "";
            Assert.Contains("#macro TOP_SPEED 12", hover, StringComparison.Ordinal);
            Assert.Contains("gml_Script_config", hover, StringComparison.Ordinal);

            // Undo takes the entry back out: its names leave the index.
            workspace.Undo();
            await GmlProjectSymbols.ReadyAsync(workspace);
            Assert.Null(GmlProjectSymbols.Find(workspace, "TOP_SPEED"));
        });
    }
}
