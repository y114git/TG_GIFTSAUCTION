using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // the data model owns the sprites it holds

namespace G3MStudio.Tests;

/// <summary>The shared editor buttons: Add frame in the image editor, Edit Image in the sprite tab, and a disabled control saying why.</summary>
public sealed class EditorButtonTests
{
    internal static async Task<(Workspace Workspace, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            TestData.AddPage(data);
            SpriteTemplate.Create(data, "spr_btn");
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        var service = new WorkspaceService(userData, RecentFiles.Load(userData));
        return (await service.OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken), dir);
    }

    private static Button Named(Control root, string name) => root.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    [Fact]
    public async Task AddFrameInTheImageEditorMakesAnEmptyFrameOfTheSameSizeAndUndoRemovesIt()
    {
        var (workspace, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(() =>
            {
                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_btn");
                var before = sprite.Textures.Count;
                using var session = new WorkspaceImageSession(workspace, "spr_btn");
                using var view = new ImageEditorView(session, 0, new ImageOptions());
                var window = new Window { Content = view, Width = 1200, Height = 700 };
                window.Show();
                try
                {
                    view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "1,1 5,1"));
                    Named(view, "ImageAddFrame").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(before + 1, sprite.Textures.Count);
                    Assert.Equal(1, view.Frame);
                    var first = session.Read(0);
                    var added = session.Read(1);
                    Assert.Equal((first.Width, first.Height), (added.Width, added.Height));
                    Assert.Equal(Rgba.Clear, added.Get(3, 1));
                    Assert.NotEqual(Rgba.Clear, first.Get(3, 1));
                    Assert.NotNull(Named(view, "ImageDuplicateFrame"));
                    workspace.Undo();
                    Assert.Equal(before, sprite.Textures.Count);
                }
                finally
                {
                    window.Close();
                }

                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task TheSpriteTabHasAnEditImageButtonThatNamesTheSelectedFrame()
    {
        var (workspace, dir) = await OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(() =>
            {
                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_btn");
                using var textures = new TextureBitmaps();
                var view = new SpriteFramesView(sprite, textures, workspace.History, workspace);
                var window = new Window { Content = view, Width = 900, Height = 400 };
                window.Show();
                try
                {
                    int? asked = null;
                    view.EditImageRequested += frame => asked = frame;
                    var button = Named(view, "SpriteEditImage");
                    Assert.True(button.IsEnabled);
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(view.SelectedFrame, asked);
                }
                finally
                {
                    window.Close();
                }

                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task ADisabledControlShowsWhyAsItsTooltipAndKeepsItsOwnTipWhenEnabled()
    {
        await HeadlessApp.Run(() =>
        {
            var button = new Button();
            ToolTip.SetTip(button, "own");
            button.GateKey("why.selectGlyph");
            Assert.False(button.IsEnabled);
            Assert.Equal(Loc.Current["why.selectGlyph"], ToolTip.GetTip(button));
            Assert.NotEqual("why.selectGlyph", ToolTip.GetTip(button)); // the key exists in the language file
            button.GateKey(null);
            Assert.True(button.IsEnabled);
            Assert.Equal("own", ToolTip.GetTip(button));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AGatedTipFollowsTheLanguageAndANewerTipSurvivesEnabling()
    {
        await HeadlessApp.Run(() =>
        {
            var button = new Button();
            ToolTip.SetTip(button, "first");
            button.GateKey("why.selectGlyph");
            ToolTip.SetTip(button, "newer"); // a later tip (shortcut hint, relabel) replaces the gate text
            button.GateKey(null);
            Assert.Equal("newer", ToolTip.GetTip(button));

            var language = Loc.Current.Language;
            try
            {
                button.GateKey("why.selectGlyph");
                Loc.Current.SetLanguage(language == "ru" ? "en" : "ru");
                Assert.Equal(Loc.Current["why.selectGlyph"], ToolTip.GetTip(button));
                button.GateKey(null);
                Assert.Equal("newer", ToolTip.GetTip(button));
            }
            finally { Loc.Current.SetLanguage(language); }

            return Task.CompletedTask;
        });
    }

    private static readonly string[] WhyKeys = ["readOnly", "selectPoint", "selectGlyph", "selectInstances", "selectInstance", "maskManual", "nineSlice", "nothingToUndo", "nothingToRedo", "noChanges", "layerNone", "layerKind", "layerEmpty", "pickOne", "noSprite", "noFrames", "selectFile", "selectPicture", "noChannel"];

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("es")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("zh_cn")]
    [InlineData("zh_tw")]
    public async Task EveryWhyKeyExistsInEveryLanguage(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "G3MStudio.Core", "Languages", $"lang_{language}.json");
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var why = json.RootElement.GetProperty("why");
        foreach (var key in WhyKeys) Assert.False(string.IsNullOrWhiteSpace(why.GetProperty(key).GetString()), $"{language} why.{key}");
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("image").GetProperty("duplicateFrame").GetString()));
    }
}
