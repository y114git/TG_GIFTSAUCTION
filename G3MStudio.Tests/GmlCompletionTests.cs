using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlCompletionTests
{
    [Fact]
    public void NamesThatStartWithTheTextComeFirstShortestFirstAndNothingElseShows()
    {
        KeyValuePair<string, string>[] names = [new("draw_text", "Function"), new("draw_text_ext", "Function"), new("Draw_Sprite", "Function"), new("obj_draw", "Asset"), new("draw", "Constant")];
        Assert.Equal(["draw_text", "draw_text_ext", "Draw_Sprite"], GmlCompletion.Match(names, "draw_").Select(n => n.Key));
        Assert.Equal(["Draw_Sprite", "draw_text", "draw_text_ext"], GmlCompletion.Match(names, "Draw").Select(n => n.Key));
        Assert.Empty(GmlCompletion.Match(names, "zzz"));
        Assert.DoesNotContain(GmlCompletion.Match(names, "draw"), n => n.Key == "draw"); // the whole word is already typed
    }

    [Fact]
    public void TheWordUnderTheCaretIsFoundAtItsEdgesAndNotInsideSpacesOrNumbers()
    {
        const string text = "x = scr_move(12, obj_player);";
        Assert.Equal("scr_move", GmlNames.WordAt(text, 6));
        Assert.Equal("scr_move", GmlNames.WordAt(text, 4)); // the start of the word
        Assert.Equal("scr_move", GmlNames.WordAt(text, 12)); // the end of the word
        Assert.Equal("obj_player", GmlNames.WordAt(text, 20));
        Assert.Null(GmlNames.WordAt(text, 15)); // between the number and the comma it is the number, which is no name
        Assert.Null(GmlNames.WordAt(text, -1));
    }

    [Fact]
    public async Task AScriptLeadsToItsCodeAndAnObjectToItself()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var data = window.Workspaces.Current!.Data;
                var definitions = GmlNames.Definitions(window.Workspaces.Current!);
                var script = data.Scripts.First(x => x.Code?.Name?.Content is { Length: > 0 });
                Assert.Equal("code:" + script.Code!.Name.Content, definitions(script.Name.Content!));
                var obj = data.GameObjects[0].Name.Content!;
                Assert.Equal("objects:" + obj, definitions(obj));
                Assert.Null(definitions("draw_text")); // a built-in has no definition in the file
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheFindPanelOfACodeTabSpeaksTheLanguageOfTheProgram()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                G3MStudio.Localization.Loc.Current.SetLanguage("ru");
                var tab = new CodeEditorTab(workspace, workspace.Data.Code.First(c => c.ParentEntry is null && c.Name.Content is { Length: > 0 }).Name.Content!);
                var host = new Avalonia.Controls.Window { Content = tab, Width = 800, Height = 600 };
                host.Show();
                await tab.Ready;
                tab.ShowFind();
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                var controls = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(tab).OfType<Avalonia.Controls.Control>().ToList();
                Assert.Contains(controls, c => Avalonia.Controls.ToolTip.GetTip(c) as string == "Учитывать регистр");
                Assert.Contains(controls, c => c is Avalonia.Controls.TextBox { PlaceholderText: "Найти..." });
                host.Close();
            }
            finally
            {
                G3MStudio.Localization.Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheNameTableFollowsARenameAnUndoAndADifferentRenameAtTheSamePlace()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var definitions = GmlNames.Definitions(workspace);
                var name = workspace.Data.GameObjects[0].Name.Content!;
                workspace.Edit(new Core.Workspace.RenameResourceCommand("objects", name, "obj_zz_b"));
                Assert.Equal("objects:obj_zz_b", definitions("obj_zz_b"));
                workspace.History.Undo();
                Assert.Null(definitions("obj_zz_b"));
                workspace.Edit(new Core.Workspace.RenameResourceCommand("objects", name, "obj_zz_c")); // the same place in the history as the first rename
                Assert.Equal("objects:obj_zz_c", definitions("obj_zz_c"));
                Assert.Null(definitions("obj_zz_b"));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
