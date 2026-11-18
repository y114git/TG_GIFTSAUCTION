using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlLintUiTests
{
    [Fact]
    public async Task TheGameContextKnowsTheResourcesBuiltInsAndTheirRules()
    {
        await CodeIntelUiTests.WithTab((_, workspace, _) =>
        {
            var context = GmlLintSource.For(workspace);
            Assert.True(context.IsKnown("scr_0"));
            Assert.True(context.IsKnown("obj_1"));
            Assert.True(context.IsKnown("draw_text"));
            Assert.True(context.IsKnown("probe"));
            Assert.False(context.IsKnown("zz_not_anywhere"));
            Assert.True(context.IsReadOnly("fps"));
            Assert.True(context.IsReadOnly("c_red"));
            Assert.False(context.IsReadOnly("x"));
            Assert.Equal((3, 3), context.ArgumentRange("draw_text"));
            Assert.Equal(int.MaxValue, context.ArgumentRange("string")?.Max);
            Assert.True(context.IsDeprecated("draw_background"));
            Assert.True(context.Escapes);
            Assert.Same(context, GmlLintSource.For(workspace));
            Assert.False(GmlLintSource.For(workspace, "gml_Script_probe").InstanceCode);
            Assert.True(GmlLintSource.For(workspace, "gml_Object_obj_1_Step_0").InstanceCode);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnEditMakesANewContextButKeepsTheBuiltInTables()
    {
        await CodeIntelUiTests.WithTab((_, workspace, _) =>
        {
            var before = GmlLintSource.For(workspace);
            workspace.Edit(new Core.Workspace.CodeEntryCommand("gml_Script_more"));
            var after = GmlLintSource.For(workspace);
            Assert.NotSame(before, after);
            Assert.Same(before.IsReadOnly.Target, after.IsReadOnly.Target);
            Assert.Same(before.IsBuiltinVariable.Target, after.IsBuiltinVariable.Target);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheLiveCheckShowsFindingsWithTheirLengthAndOffersTheirFixes()
    {
        const string text = "if (x = 1) y = undefined_thing;\ndraw_text(1);\n";
        await CodeIntelUiTests.WithTab(async (tab, workspace, _) =>
        {
            await GmlProjectSymbols.ReadyAsync(workspace); // before it, any name may come from another entry
            await tab.CheckLiveAsync();
            var problems = workspace.Problems.All.Where(p => p.Source == "gml_Script_probe").ToList();
            Assert.Contains(problems, p => p is { Code: GmlLint.AssignInCondition, Severity: "Info", Line: 1, Column: 7, Length: 1 });
            Assert.Contains(problems, p => p is { Code: GmlLint.UnknownVariable, Severity: "Warning", Line: 1, Column: 16, Length: 15 });
            Assert.Contains(problems, p => p is { Code: GmlLint.ArgumentCount, Line: 2, Length: 12 });
            Assert.Contains("undefined_thing", tab.HoverTextAt(text.IndexOf("thing", StringComparison.Ordinal)) ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain(GmlLint.UnknownVariable, tab.HoverTextAt(text.IndexOf(';', StringComparison.Ordinal)) ?? "", StringComparison.Ordinal); // the column after a finding is not part of it
            tab.Editor.CaretOffset = text.IndexOf('=', StringComparison.Ordinal);
            Assert.Contains(tab.QuickFixesAtCaret(), f => f.Key == "code.fix.useEquals");
        }, text);
    }

    [Fact]
    public async Task TextThatDoesNotParseGetsOnlyTheParserErrors()
    {
        await CodeIntelUiTests.WithTab(async (tab, workspace, _) =>
        {
            await tab.CheckLiveAsync();
            Assert.All(workspace.Problems.All, p => Assert.StartsWith("GML", p.Code, StringComparison.Ordinal));
            Assert.NotEmpty(workspace.Problems.All);
        }, "x = undefined_thing +;\n");
    }
}
