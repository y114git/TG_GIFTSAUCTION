using G3MLib.Analyzer.Decompiler;
using G3MStudio.Core.Workspace;
using Avalonia.Threading;

namespace G3MStudio.Tests;

public sealed class DecompilerOptionsTests
{
    [Fact]
    public void TheFieldsAreTheLibrarysSwitchesAndTextsAndSavedValuesAreAppliedWithBadOnesIgnored()
    {
        var names = DecompilerOptions.Fields.Select(f => f.Name).ToList();
        foreach (var expected in new[] { "IndentString", "UseSemicolon", "CleanupTry", "CreateEnumDeclarations", "UnknownArgumentNamePattern", "AllowLeftoverDataOnStack", "UnknownEnumName" }) Assert.Contains(expected, names);
        Assert.DoesNotContain("SinglePartPredefinedDoubles", names);

        var target = new DecompileSettings();
        DecompilerOptions.Apply(target, new Dictionary<string, string>
        {
            ["UseSemicolon"] = "false",
            ["IndentString"] = "\t",
            ["UnknownArgumentNamePattern"] = "no placeholder", // dropped: it lacks {0}
            ["UnknownEnumName"] = "9bad", // dropped: not a GML name
            ["CleanupTry"] = "maybe", // dropped: not true or false
            ["NoSuchSetting"] = "x",
        });
        Assert.False(target.UseSemicolon);
        Assert.Equal("\t", target.IndentString);
        Assert.Equal("arg{0}", target.UnknownArgumentNamePattern);
        Assert.Equal("UnknownEnum", target.UnknownEnumName);
        Assert.True(target.CleanupTry);

        DecompilerOptions.Apply(target, new Dictionary<string, string>()); // nothing saved: back to the defaults
        Assert.True(target.UseSemicolon);
        Assert.Equal("    ", target.IndentString);
        Assert.Equal("Open block brace on same line", DecompilerOptions.Label("OpenBlockBraceOnSameLine"));
    }

    [Fact]
    public async Task ASettingChangeReachesTheOpenFileAndTheDecompiledText()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var data = window.Workspaces.Current!.Data;
                var target = (DecompileSettings)data.ToolInfo.DecompilerSettings;
                Assert.True(target.UseSemicolon);
                window.Workspaces.Settings.Update(x => x with { Decompiler = new Dictionary<string, string> { ["UseSemicolon"] = "false", ["IndentString"] = "	" } });
                Dispatcher.UIThread.RunJobs();
                Assert.False(target.UseSemicolon);
                Assert.Equal("	", target.IndentString);
                window.Workspaces.Settings.Update(x => x with { Decompiler = new Dictionary<string, string>() });
                Dispatcher.UIThread.RunJobs();
                Assert.True(target.UseSemicolon);
            }
            finally { window.Close(); }
        });
    }
}
