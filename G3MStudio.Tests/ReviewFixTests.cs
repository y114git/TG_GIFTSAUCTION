using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Theme;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>Regression checks for the review fixes: settings apply that must not repeat work, caches, glyph bulk edits, drags, motion and the Save command.</summary>
public sealed class ReviewFixTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle");
    }

    [Fact]
    public async Task TheAnimationsBoxStartsOnSystemDefaultAndPickingMediumStoresIt()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var box = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "Animations");
                Assert.Equal(0, box.SelectedIndex);
                Assert.Equal("", window.Workspaces.Settings.Current.Animations);
                box.SelectedIndex = 2; // System default, Full, Medium
                Assert.Equal(MotionPolicy.Medium, window.Workspaces.Settings.Current.Animations);
                box.SelectedIndex = 0;
                Assert.Equal("", window.Workspaces.Settings.Current.Animations);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ApplyingTheSameSettingsAgainChangesNothing()
    {
        var before = EditorOptions.Favourites.ToList();
        await HeadlessApp.Run(() =>
        {
            var styles = Application.Current!.Styles.Count;
            TooltipDelay.Apply(400);
            Assert.Equal(styles + 1, Application.Current.Styles.Count);
            var applied = Application.Current.Styles[^1];
            TooltipDelay.Apply(400);
            Assert.Same(applied, Application.Current.Styles[^1]);

            var changes = 0;
            void Count() => changes++;
            Motion.Changed += Count;
            try
            {
                Motion.SetLevel(MotionPolicy.Off);
                Motion.SetLevel(MotionPolicy.Off);
                Assert.True(changes <= 1);
            }
            finally
            {
                Motion.Changed -= Count;
                Motion.SetLevel(MotionPolicy.Medium);
            }

            var raised = new List<IReadOnlyList<string>>();
            void Saved(IReadOnlyList<string> names) => raised.Add(names);
            EditorOptions.FavouritesChanged += Saved;
            try
            {
                EditorOptions.SetFavourites(["b", "a", "c"]);
                EditorOptions.SetFavourites(["c", "a", "b"]);
                EditorOptions.ToggleFavourite("a");
                EditorOptions.ToggleFavourite("z");
                Assert.Equal(3, raised.Count);
                Assert.Equal(["b", "c", "z"], raised[^1]);
            }
            finally
            {
                EditorOptions.FavouritesChanged -= Saved;
                EditorOptions.SetFavourites(before);
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AGameFunctionAnswersAtOnceAndAnnouncesWhenItsScriptIsRead()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir, objects: 2, withCode: true));
                var workspace = window.Workspaces.Current!;
                var ready = 0;
                void Count(Workspace w) { if (ReferenceEquals(w, workspace)) ready++; }
                GameFunctions.Ready += Count;
                try
                {
                    Assert.NotNull(GameFunctions.Find(workspace, "scr_0"));
                    for (var i = 0; i < 500 && ready == 0; i++)
                    {
                        Dispatcher.UIThread.RunJobs();
                        await Task.Delay(10);
                    }

                    Assert.Equal(1, ready);
                }
                finally { GameFunctions.Ready -= Count; }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AGameFunctionIsReadAgainOnlyWhenItsOwnCodeChanged()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir, objects: 2, withCode: true));
                var workspace = window.Workspaces.Current!;
                RefEntry? Read()
                {
                    // Find answers at once with a fresh name-only entry until the worker is done; the read entry is the same object on every call.
                    var until = DateTime.UtcNow.AddSeconds(10);
                    var last = GameFunctions.Find(workspace, "scr_0");
                    while (last is not null && DateTime.UtcNow < until)
                    {
                        Thread.Sleep(10);
                        var next = GameFunctions.Find(workspace, "scr_0");
                        if (ReferenceEquals(next, last)) return next;
                        last = next;
                    }

                    return last;
                }
                var first = Read();
                Assert.NotNull(first);
                Assert.Same(first, Read());
                workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                Assert.Same(first, Read()); // another script's code
                workspace.Edit(new EntryScriptsCommand(EntryScriptsCommand.GlobalInit, EntryScriptsCommand.Add, "gml_Script_probe"));
                Assert.NotSame(first, Read()); // an edit that names no code entry drops the cache
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task GlyphsAreAddedSortedAndMissingTextWorksPerCodePoint()
    {
        await HeadlessApp.Run(() =>
        {
            var workspace = ThumbnailTests.Generate();
            var font = workspace.Data.Fonts[0];
            var view = new FontGlyphsView(font, workspace);
            view.AddCodes([0x3044, 0x3042, 0x3044, 0x3043]);
            var added = font.Glyphs.Where(g => g.Character is >= 0x3042 and <= 0x3044).Select(g => (int)g.Character).ToList();
            Assert.Equal([0x3042, 0x3043, 0x3044], added);
            Assert.Equal(font.Glyphs.OrderBy(g => g.Character).Select(g => g.Character), font.Glyphs.Select(g => g.Character));
            Loc.Current.SetLanguage("en");
            var missing = view.MissingText("あ😀😀\n");
            Assert.Contains("😀", missing, StringComparison.Ordinal);
            Assert.DoesNotContain("あ", missing, StringComparison.Ordinal);
            Assert.Equal("", view.MissingText(new string('あ', 20000)));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AGlyphListThatWasDetachedShowsTheChangesMadeMeanwhileInTheLanguageInUse()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("ja");
            try
            {
                var workspace = ThumbnailTests.Generate();
                var font = workspace.Data.Fonts[0];
                var view = new FontGlyphsView(font, workspace);
                var host = new Border { Child = view };
                var window = new Window { Width = 600, Height = 600, Content = host };
                window.Show();
                host.Child = null; // the tab is hidden
                view.AddCodes([0x3042, 0x3043, 0x3044]);
                host.Child = view;
                Dispatcher.UIThread.RunJobs();
                var list = view.GetVisualDescendants().OfType<ListBox>().First();
                Assert.Equal(font.Glyphs.Count, view.Shown);
                Assert.Contains(list.ItemsSource!.Cast<string>(), t => t.Contains("U+3042", StringComparison.Ordinal) && t.Contains("送り幅", StringComparison.Ordinal));
                window.Close();
            }
            finally { Loc.Current.SetLanguage("en"); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ShaderTextNotSavedYetIsPutBackIntoARebuiltView()
    {
        await HeadlessApp.Run(() =>
        {
            var shader = new G3MLib.DataFile.Models.GameMakerShader { Name = new G3MLib.DataFile.Models.GameMakerString("sh_test"), GLSL_Vertex = new G3MLib.DataFile.Models.GameMakerString("void main() {}"), GLSL_Fragment = new G3MLib.DataFile.Models.GameMakerString("void main() {}") };
            static AvaloniaEdit.TextEditor Box(ShaderSourceView view, int tab) => ((StackPanel)((TabItem)view.Items[tab]!).Content!).Children.OfType<AvaloniaEdit.TextEditor>().Single();
            var view = new ShaderSourceView(shader);
            Assert.Empty(view.Unsaved());
            Box(view, 1).Text = "void main() { typed(); }";
            var unsaved = view.Unsaved();
            Assert.Equal(["GLSL_Fragment"], unsaved.Keys);

            var rebuilt = new ShaderSourceView(shader);
            rebuilt.Restore(unsaved, 1);
            Assert.Equal("void main() { typed(); }", Box(rebuilt, 1).Text);
            Assert.Equal(1, rebuilt.SelectedIndex);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASelectedHeadingIsNotCountedAsAnItem()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 320, Height = 400, Content = view };
            window.Show();
            using var data = ExplorerModelTests.Generate(20);
            view.Load("game.win", data);
            Dispatcher.UIThread.RunJobs();
            try
            {
                var list = view.GetVisualDescendants().OfType<ListBox>().Single(c => c.Name == "List");
                var heading = view.Model.Rows.First(r => r.Kind == ExplorerRowKind.Type);
                var item = view.Model.Rows.First(r => r.IsItem);
                list.SelectedItems!.Add(heading);
                list.SelectedItems.Add(item);
                Dispatcher.UIThread.RunJobs();
                var shown = view.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "SelectedCount").Text ?? "";
                Assert.Contains("1", shown, StringComparison.Ordinal);
                Assert.DoesNotContain("2", shown, StringComparison.Ordinal);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASideGrowsBackWhenTheWindowWidensAgain()
    {
        await HeadlessApp.Run(() =>
        {
            var side = new Border { Name = "side" };
            var grid = SideSplit.Around(new Border(), new SideColumn(side, "test.regrow", 200, 50));
            var window = new Window { Width = 800, Height = 200, Content = grid };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal(200, side.Bounds.Width, 1);
                window.Width = 260;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(side.Bounds.Width < 200);
                window.Width = 800;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(200, side.Bounds.Width, 1);

                side.IsVisible = false;
                window.UpdateLayout();
                Assert.Equal(0, grid.ColumnDefinitions[2].Width.Value);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ADragEndsWhenTheButtonIsNoLongerHeld()
    {
        await HeadlessApp.Run(() =>
        {
            var target = new Border { Height = 100 };
            var bar = DragHandle.For(target, "test.drag");
            var window = new Window { Width = 300, Height = 400, Content = new StackPanel { Children = { target, bar } } };
            window.Show();
            try
            {
                window.UpdateLayout();
                var at = bar.TranslatePoint(new Point(100, 4), window)!.Value;
                window.MouseDown(at, MouseButton.Left);
                window.MouseMove(at + new Point(0, 30), RawInputModifiers.LeftMouseButton);
                Assert.Equal(130, target.Height, 1);
                window.MouseMove(at + new Point(0, 60), RawInputModifiers.None);
                window.MouseMove(at + new Point(0, 90), RawInputModifiers.LeftMouseButton);
                Assert.Equal(130, target.Height, 1);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ALeavingControlKeepsItsFadeWhenSlideTouchesIt()
    {
        await HeadlessApp.Run(async () =>
        {
            var tab = new Border { Width = 50, Height = 20 };
            var window = new Window { Content = tab };
            window.Show();
            try
            {
                Motion.TimedInHeadless = true;
                Motion.SetLevel("Full");
                var closed = 0;
                Motion.Exit(tab, () => closed++);
                Motion.Slide(tab, 20, 0);
                Assert.Equal(0, closed);
                Assert.False(tab.IsHitTestVisible);
                await Settle(() => closed == 1);
                Assert.True(tab.IsHitTestVisible);
            }
            finally
            {
                Motion.TimedInHeadless = false;
                Motion.SetLevel(MotionPolicy.Medium);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AFailingSaveQuestionIsReportedNotLost()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(TestData.WriteSample(dir, objects: 2, withCode: true));
                window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                window.SaveAsker = _ => throw new InvalidOperationException("dialog broke");
                window.Save();
                await Settle(() => window.Notifications.Active.Any(n => n.Level == NotificationLevel.Error && n.Text.Contains("dialog broke", StringComparison.Ordinal)));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheFunctionStarHasANameAndWorksFromTheKeyboard()
    {
        var before = EditorOptions.Favourites.ToList();
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var game = new RefEntry("scr_hit", RefKind.Function, GameFunctions.Category, "", [new RefParam("", "", "", false, true)], "") { Game = true, Where = "scripts:scr_hit" };
            var panel = new ReferencePanel(GmlReference.Default) { GameSource = () => [game], GameDetail = _ => game };
            var window = new Window { Content = panel, Width = 600, Height = 600 };
            window.Show();
            try
            {
                EditorOptions.SetFavourites([]);
                panel.GetVisualDescendants().OfType<ComboBox>().First(c => c.Name == "ReferenceCategory").SelectedIndex = 2;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var star = panel.GetVisualDescendants().OfType<TextBlock>().First(c => c.Name == "RowStar");
                Assert.Equal("Favourite: scr_hit", Avalonia.Automation.AutomationProperties.GetName(star));
                Assert.True(star.Focusable);
                star.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                Assert.True(EditorOptions.IsFavourite("scr_hit"));
            }
            finally
            {
                EditorOptions.SetFavourites(before);
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
