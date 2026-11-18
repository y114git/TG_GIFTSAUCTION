using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Rooms;

#pragma warning disable CA2000 // windows and views made for a check only

namespace G3MStudio.Tests;

/// <summary>Each recent user action: one undo step puts the data back (or, for Studio settings, the choice is stored and not part of the history).</summary>
public sealed class UndoActionTests
{
    private static void Click(Control root, string name)
    {
        Dispatcher.UIThread.RunJobs();
        root.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task AddFrameFromTheSpriteTabAddsOneFrameAndUndoRemovesIt()
    {
        var (workspace, dir) = await EditorButtonTests.OpenAsync();
        using (dir)
        using (workspace)
        {
            await HeadlessApp.Run(() =>
            {
                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_btn");
                var before = sprite.Textures.Count;
                using var textures = new TextureBitmaps();
                var view = new SpriteFramesView(sprite, textures, workspace.History, workspace);
                var window = new Window { Content = view, Width = 900, Height = 400 };
                window.Show();
                try
                {
                    Click(view, "SpriteFrameAdd");
                    Assert.Equal(before + 1, sprite.Textures.Count);
                    workspace.Undo();
                    Assert.Equal(before, sprite.Textures.Count);
                    workspace.Redo();
                    Assert.Equal(before + 1, sprite.Textures.Count);
                }
                finally { window.Close(); }

                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task AFontGlyphAddedByTypedCharacterPresetOrTextIsOneUndoStep()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var workspace = ThumbnailTests.Generate();
            var font = workspace.Data.Fonts[0];
            var view = new FontGlyphsView(font, workspace);
            var window = new Window { Content = view, Width = 900, Height = 700 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var start = font.Glyphs.Count;
                view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "GlyphNew").Text = "Ж";
                Click(view, "GlyphAdd");
                Assert.Equal(start + 1, font.Glyphs.Count);
                workspace.Undo();
                Assert.Equal(start, font.Glyphs.Count);

                Click(view, "GlyphPresetDigits");
                Assert.Equal(start + 10, font.Glyphs.Count);
                workspace.Undo();
                Assert.Equal(start, font.Glyphs.Count); // the whole preset is one step

                view.GetVisualDescendants().OfType<ToggleButton>().Single(x => x.Name == "GlyphFromTextToggle").IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "GlyphFromText").Text = "あいあ";
                Click(view, "GlyphFromTextAdd");
                Assert.Equal(start + 2, font.Glyphs.Count);
                workspace.Undo();
                Assert.Equal(start, font.Glyphs.Count);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RoomLayerButtonsAddDuplicateRenameMoveAndDeleteEachWithOneUndoStep()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var workspace = ThumbnailTests.Generate();
            var room = workspace.Data.Rooms[0];
            room.Layers ??= [];
            var panel = new RoomLayersPanel(room, workspace);
            var window = new Window { Content = panel, Width = 500, Height = 700 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                string[] Names() => [.. room.Layers.Select(l => $"{l.LayerName.Content}:{l.LayerDepth}")];
                var list = panel.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "RoomLayers");

                panel.AddLayer(0);
                panel.AddLayer(2);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, room.Layers.Count);
                workspace.Undo();
                Assert.Single(room.Layers);
                workspace.Redo();
                var two = Names();

                void Step(Action act)
                {
                    act();
                    Dispatcher.UIThread.RunJobs();
                    Assert.NotEqual(two, Names());
                    workspace.Undo();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(two, Names());
                }

                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                Step(() => Click(panel, "DuplicateLayer"));
                Step(() => Click(panel, "DeleteLayer"));
                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                panel.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "LayerName").Text = "renamed";
                Step(() => Click(panel, "RenameLayer"));
                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                Step(() => Click(panel, "LayerDown"));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheReadOnlyCheckboxLocksTheResourceStoresItAndLeavesTheHistoryAlone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using (var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken))
        {
            await HeadlessApp.Run(async () =>
            {
                Loc.Current.SetLanguage("en");
                var panel = new InspectorPanel(() => { });
                var window = new Window { Content = panel, Width = 400, Height = 500 };
                window.Show();
                try
                {
                    await panel.ShowAsync(workspace, "scripts:scr_0");
                    Dispatcher.UIThread.RunJobs();
                    var box = panel.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "InspectorLock");
                    var steps = workspace.History.Items.Count;
                    box.IsChecked = true;
                    Assert.True(workspace.Locks.IsLocked("scripts:scr_0"));
                    Assert.Equal(steps, workspace.History.Items.Count); // a Studio setting, not an edit: nothing to undo
                }
                finally { window.Close(); }
            });
        }

        using var again = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        Assert.True(again.Locks.IsLocked("scripts:scr_0")); // stored with the user data
    }

    [Fact]
    public async Task TogglingAGameFunctionFavouriteIsStoredInTheSettingsAndComesBackOnTheNextStart()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var before = EditorOptions.Favourites.ToList();
        try
        {
            await HeadlessApp.Run(() =>
            {
                EditorOptions.SetFavourites([]);
                var first = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
                first.Show();
                EditorOptions.ToggleFavourite("draw_text");
                Assert.Contains("draw_text", first.Workspaces.Settings.Current.FavouriteFunctions);
                first.Close();

                EditorOptions.SetFavourites([]);
                var second = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
                second.Show();
                Assert.True(EditorOptions.IsFavourite("draw_text"));
                EditorOptions.ToggleFavourite("draw_text");
                Assert.DoesNotContain("draw_text", second.Workspaces.Settings.Current.FavouriteFunctions);
                second.Close();
            });
        }
        finally { EditorOptions.SetFavourites(before); }
    }
}
