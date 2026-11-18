using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Panels;
using Avalonia.VisualTree;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>The main editors and panels hosted narrow: no control leaves its window or scroll area, and no two controls touch with no gap.</summary>
public sealed class LayoutAuditTests
{
    public static TheoryData<double> Widths => [260, 400];

    private static Task<List<string>> AuditAsync(double width, bool gaps, bool edges = false) =>
        AuditAsync(width, (name, window) => edges ? (name is "Room toolbar and list" or "Search" or "Image editor" or "Explorer" ? LayoutAudit.TooCloseToEdge(window) : []) : gaps ? LayoutAudit.Touching(window) : LayoutAudit.Overflows(window));

    private static async Task<List<string>> AuditAsync(double width, Func<string, Window, IEnumerable<string>> check)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(Corpus.Files(Corpus.Fixtures).First(), cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var room = data.Rooms.OrderByDescending(r => r.Layers?.Count ?? 0).First();
            var preview = new RoomPreview(room, textures, workspace);
            await preview.Ready;
            var sprite = data.Sprites.First(s => s.Textures.Count > 0);
            var spriteEditor = new SpriteEditorView(sprite, textures, workspace);
            var spriteName = sprite.Name.Content!;
            var imageEditor = new ImageEditorView(new WorkspaceImageSession(workspace, spriteName));
            var cases = new List<(string Name, Control View)>
            {
                ("Search", new SearchPanel()),
                ("Room toolbar and list", preview),
                ("Room inspector", preview.SidePanel is { } side ? new ScrollViewer { Content = side } : new Border()),
                ("Sprite", new PropertyGridTab(workspace, "sprites", spriteName, spriteEditor)),
                ("Image editor", imageEditor),
                ("Object events", new ObjectEventsView(data.GameObjects[0], _ => { })),
            };
            if (data.Fonts.Count > 0) cases.Add(("Font glyphs", new ScrollViewer { Content = new FontGlyphsView(data.Fonts[0], workspace) }));
            var explorer = new G3MStudio.Explorer.ExplorerView();
            explorer.Load("game.win", data);
            cases.Add(("Explorer", explorer));
            if (data.Shaders.FirstOrDefault() is { } shader) cases.Add(("Shader", new ScrollViewer { Content = new ShaderSourceView(shader, workspace) }));
            if (data.Backgrounds.FirstOrDefault(b => b.Texture is not null) is { } tileSet) cases.Add(("Tile set", new ScrollViewer { Content = new TilePickerView(tileSet, textures, workspace) }));
            cases.Add(("Sound", new SoundPlayerView(AudioPlaybackTests.Sound(2), _ => true, () => new G3MStudio.Core.Settings.AudioOptions())));
            foreach (var (name, view) in cases)
            {
                var window = await LayoutAudit.Host(view, width);
                problems.AddRange(check(name, window).Select(p => $"{name} at {width}: {p}"));
                window.Close();
            }
        });
        return problems;
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public async Task NothingIsCutOffInANarrowEditor(double width)
    {
        var problems = await AuditAsync(width, gaps: false);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public async Task NoTwoControlsTouchWithoutAGap(double width)
    {
        var problems = await AuditAsync(width, gaps: true);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    // Toolbars at a wide view and at the layout widths of 100, 125 and 150 % interface scale.
    public static TheoryData<double> RowWidths => [1200, 1200 / 1.25, 1200 / 1.5, 400];

    [Theory]
    [MemberData(nameof(RowWidths))]
    public async Task ControlsSideBySideSitOnOneLevelWithOneHeight(double width)
    {
        var problems = await AuditAsync(width, (_, window) => LayoutAudit.Misaligned(window));
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public async Task NoControlIsPressedAgainstTheEdgeOfItsView(double width)
    {
        var problems = await AuditAsync(width, gaps: false, edges: true);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Theory]
    [InlineData(420)]
    [InlineData(700)]
    public async Task TheSoundHeaderIsNotCappedSoItsLastInfoLineIsNeverHalfHidden(double height)
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(Path.Combine(Corpus.Fixtures, "uty", "data.win"), cancellationToken: TestContext.Current.CancellationToken);
        var sound = workspace.Data.Sounds.First(s => s.AudioFile?.Data is { Length: > 0 });
        await HeadlessApp.Run(async () =>
        {
            var view = new SoundPlayerView(sound, _ => true);
            var window = await LayoutAudit.Host(new PropertyGridTab(workspace, "sounds", sound.Name.Content!, view), 900, height);
            // The header takes its whole height until the splitter is dragged: its own scroller has nothing to scroll, so no line of it is cut by a cap.
            var own = view.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(own.Extent.Height <= own.Viewport.Height + 1, $"the header scrolls inside its part: {own.Extent.Height} in {own.Viewport.Height}");
            Assert.True(view.Bounds.Height >= view.DesiredSize.Height - view.Margin.Top - view.Margin.Bottom - 1);
            Assert.Empty(LayoutAudit.Overflows(window));
            window.Close();
        });
    }

    [Fact]
    public async Task SideColumnsHaveASplitterAndAWiderDragIsRemembered()
    {
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8)));
            var window = await LayoutAudit.Host(view, 1100);
            var splitters = view.GetVisualDescendants().OfType<GridSplitter>().Where(g => g.ResizeDirection == GridResizeDirection.Columns).ToList();
            Assert.Equal(2, splitters.Count);
            var grid = (Grid)splitters[1].Parent!;
            grid.ColumnDefinitions[^1].Width = new GridLength(300);
            await LayoutAudit.Settle();
            splitters[1].RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Avalonia.Controls.Primitives.Thumb.DragCompletedEvent, Vector = default });
            Assert.Equal(300, SideSplit.Remembered["imageSide"], 1);
            window.Close();
        });
    }
}
