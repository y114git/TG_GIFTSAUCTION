using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class SearchFocusTests
{
    private static TextBox? Focused(Window window) => window.FocusManager?.GetFocusedElement() as TextBox;

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task FindFocusesAndSelectsTheSearchBoxAndEscapeGivesTheEditorTheFocusBack()
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
            try
            {
                await tab.Ready;
                tab.Editor.TextArea.Focus();
                // The menu command and the shortcut inside the editor both land in the search box with its text selected, on the first open and again after a close.
                for (var round = 0; round < 3; round++)
                {
                    if (round == 1) window.KeyPressQwerty(PhysicalKey.F, HeadlessApp.Command);
                    else if (round == 2) window.KeyPressQwerty(PhysicalKey.H, HeadlessApp.Command);
                    else tab.ShowFind();
                    Settle(window);
                    var box = Focused(window);
                    Assert.NotNull(box);
                    Assert.NotNull(box.FindAncestorOfType<AvaloniaEdit.Search.SearchPanel>());
                    box.Text = "return";
                    window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                    Settle(window);
                    Assert.True(tab.Editor.TextArea.IsFocused, "round " + round);
                }

                // Reopened with the old text in the box: all of it is selected so typing replaces it.
                tab.ShowFind();
                Settle(window);
                var again = Focused(window)!;
                Assert.Equal(0, Math.Min(again.SelectionStart, again.SelectionEnd));
                Assert.Equal(again.Text!.Length, Math.Max(again.SelectionStart, again.SelectionEnd));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AFilterBoxTakesTheFocusWithItsOldTextSelected()
    {
        await HeadlessApp.Run(() =>
        {
            var box = new TextBox { Text = "old query" };
            var window = new Window { Content = new StackPanel { Children = { new TextBox(), box } } };
            window.Show();
            try
            {
                G3MStudio.Theme.TokenExtensions.FocusSelected(box);
                Settle(window);
                Assert.Same(box, Focused(window));
                Assert.Equal("old query", box.SelectedText);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheHoverCardIsDueAfterTheShortDelayAndScrollingOrTypingCancelsIt()
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
            try
            {
                await tab.Ready;
                Assert.Equal(400, EditorOptions.HoverDelayMs);
                tab.Editor.Text = string.Join('\n', Enumerable.Range(0, 300).Select(i => "var line" + i + " = " + i + ";"));
                Settle(window);
                window.MouseMove(Avalonia.VisualExtensions.TranslatePoint(tab.Editor, new Avalonia.Point(100, 20), window)!.Value);
                Assert.True(tab.HoverPending);
                tab.Editor.ScrollToLine(250);
                Settle(window);
                Assert.False(tab.HoverPending);
                window.MouseMove(Avalonia.VisualExtensions.TranslatePoint(tab.Editor, new Avalonia.Point(110, 30), window)!.Value);
                Assert.True(tab.HoverPending);
                tab.Editor.TextArea.Focus();
                window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
                Assert.False(tab.HoverPending);
            }
            finally { window.Close(); }
        });
    }
}
