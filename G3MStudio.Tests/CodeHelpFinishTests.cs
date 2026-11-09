using Avalonia.Controls;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CodeHelpFinishTests
{
    private static readonly string[] Languages = ["ru", "es", "ja", "ko", "zh_cn", "zh_tw"];

    private static readonly GmlMemberContext Player = new(name => name == "obj_player" ? ["hp", "speed_boost"] : null, "obj_player");

    // The names offered after the last dot of the text that follows the marker.
    private static List<string> Members(string text, string marker, GmlMemberContext? context = null)
    {
        var dot = text.LastIndexOf(marker, StringComparison.Ordinal) + marker.Length - 1;
        return [.. GmlMembers.After(GmlDoc.Parse(text), dot, ["x"], context).Select(m => m.Key)];
    }

    [Fact]
    public void LigaturesAndLiveChecksHaveTheirDefaults()
    {
        var settings = new AppSettings();
        Assert.False(settings.CodeLigatures);
        Assert.True(settings.CodeLiveCheck);
        Assert.False(EditorOptions.Ligatures);
    }

    [Fact]
    public void SymbolRunsOfTwoOrMoreAreFoundForTheLigatureSwitch()
    {
        Assert.Equal((1, 2), NoLigatureGenerator.RunIn("a->b", 0, 4));
        Assert.Equal((6, 2), NoLigatureGenerator.RunIn("a = b == c", 0, 10));
        Assert.Null(NoLigatureGenerator.RunIn("a + b", 0, 5));
        Assert.Equal((6, 3), NoLigatureGenerator.RunIn("x = 1 <=> 2", 0, 11));
    }

    [Fact]
    public void ParserProblemsComeFromTheBackgroundCheck()
    {
        Assert.NotEmpty(LiveCheck.Parse("a = 1;\nif (b {\n}"));
        Assert.Empty(LiveCheck.Parse("if (b) { a = 1; }"));
    }

    [Fact]
    public async Task LiveCheckShowsAndClearsSquigglesAndObeysTheSwitch()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe");
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            await tab.Ready;
            tab.Editor.Text = "if (b {\n}";
            await tab.CheckLiveAsync();
            Assert.Contains(workspace.Problems.All, p => p.Source == "gml_Script_probe");
            tab.Editor.Text = "if (b) {\n}";
            await tab.CheckLiveAsync();
            Assert.DoesNotContain(workspace.Problems.All, p => p.Source == "gml_Script_probe");
            EditorOptions.SetLayout(4, true, false, liveCheck: false);
            try
            {
                tab.Editor.Text = "if (b {\n}";
                await tab.CheckLiveAsync();
                Assert.DoesNotContain(workspace.Problems.All, p => p.Source == "gml_Script_probe");
            }
            finally
            {
                EditorOptions.SetLayout(4, true);
            }

            window.Close();
        });
    }

    [Fact]
    public void ObjectMembersComeFromTheObjectNameACreateCallAndSelf()
    {
        string[] expected = ["hp", "speed_boost", "x"];
        Assert.Equal(expected, Members("obj_player.", "obj_player.", Player));
        Assert.Equal(expected, Members("var p = instance_create_layer(0, 0, \"Instances\", obj_player);\np.", "\np.", Player));
        Assert.Equal(expected, Members("global.hero = instance_create_depth(0, 0, 0, obj_player);\nglobal.hero.", "global.hero.", Player));
        Assert.Equal(expected, Members("with (obj_player) { self. }", "self.", Player));
    }

    [Fact]
    public void SelfInTheCodeOfAnObjectListsItsOwnAssignments()
    {
        var found = Members("energy = 3;\nself.", "self.", Player);
        Assert.Contains("energy", found);
        Assert.Contains("hp", found);
        Assert.Equal("obj_player", GmlNames.OwnerObject("gml_Object_obj_player_Create_0", n => n == "obj_player" ? [] : null));
        Assert.Equal("obj_player", GmlNames.OwnerObject("gml_Object_obj_player_Collision_obj_wall_0", n => n == "obj_player" ? [] : null));
        Assert.Null(GmlNames.OwnerObject("gml_Script_probe", _ => []));
    }

    [Fact]
    public void StructLiteralKeysAreOffered()
    {
        Assert.Equal(["a", "b"], Members("var s = { a: 1, b: foo(2, 3) };\ns.", "s."));
    }

    [Fact]
    public void TranslationFilesOnlyHoldKnownKeysAndNoEmptyTexts()
    {
        var english = GmlReference.Default.Entries.ToDictionary(GmlReference.KeyOf);
        foreach (var language in Languages)
        {
            var table = GmlReference.LoadTranslation(language);
            Assert.NotNull(table);
            Assert.NotEmpty(table);
            foreach (var (key, translation) in table)
            {
                Assert.True(english.TryGetValue(key, out var entry), $"{language}: unknown key {key}");
                Assert.False(string.IsNullOrWhiteSpace(translation.Summary), $"{language}: empty description of {key}");
                foreach (var (name, meaning) in translation.Parameters)
                {
                    Assert.Contains(entry.Parameters, p => p.Name == name);
                    Assert.False(string.IsNullOrWhiteSpace(meaning), $"{language}: empty note {key}.{name}");
                }
            }
        }
    }

    [Fact]
    public void TranslatedDescriptionsReplaceEnglishAndFallBackPerEntry()
    {
        var english = GmlReference.Default;
        Assert.Same(english, english.For("en"));
        Assert.Same(english, english.For("xx"));
        foreach (var language in Languages)
        {
            var localized = english.For(language);
            Assert.Equal(english.Entries.Count, localized.Entries.Count);
            Assert.NotEqual(english.Find("abs")!.Summary, localized.Find("abs")!.Summary);
            Assert.Equal(english.Find("abs")!.Signature, localized.Find("abs")!.Signature);
        }
    }

    [Fact]
    public void ConstantGroupsLinkAPageWhereTheSchemeAllows()
    {
        var reference = GmlReference.Default;
        Assert.EndsWith("/Drawing/Colour_And_Alpha/draw_set_colour.htm", reference.ManualLink(reference.Find("c_red")!), StringComparison.Ordinal);
        Assert.EndsWith("GML_Reference.htm", reference.ManualLink(reference.Find("os_windows")!), StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionsThatHadOnlyDataFileHintsAreDocumented()
    {
        foreach (var name in new[] { "physics_apply_force", "ds_grid_value_x", "path_rotate", "audio_emitter_gain", "date_week_span", "part_emitter_burst" })
            Assert.False(string.IsNullOrEmpty(GmlReference.Default.Find(name)?.Summary), name);
    }
}
