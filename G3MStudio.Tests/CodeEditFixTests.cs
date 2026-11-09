using Avalonia.Controls;
using Avalonia.Input;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class CodeEditFixTests
{
    private static string Apply(string text, LineEdit? e) => e is null ? text : text.Remove(e.Offset, e.Length).Insert(e.Offset, e.Text);

    private static async Task WithTab(Func<CodeEditorTab, Workspace, Window, Task> body, string text)
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
            tab.Editor.Text = text;
            await body(tab, workspace, window);
            window.Close();
        });
    }

    [Fact]
    public async Task ShrinkAfterAnEditOrAFreshSelectionDoesNothingAndNeverThrows()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = 5;
            tab.ExpandSelection();
            tab.ExpandSelection();
            var grown = (tab.Editor.SelectionStart, tab.Editor.SelectionLength);
            tab.ShrinkSelection();
            Assert.NotEqual(grown, (tab.Editor.SelectionStart, tab.Editor.SelectionLength)); // a real step back
            tab.ExpandSelection();
            tab.Editor.Document.Remove(0, tab.Editor.Document.TextLength - 2); // the text got shorter than the saved ranges
            tab.Editor.Select(0, 1);
            var selection = (tab.Editor.SelectionStart, tab.Editor.SelectionLength);
            tab.ShrinkSelection();
            Assert.Equal(selection, (tab.Editor.SelectionStart, tab.Editor.SelectionLength));
            return Task.CompletedTask;
        }, "foo(alpha, beta);\n");
    }

    [Fact]
    public void LineEditsKeepTheRealBreakOfCrLfAndMixedText()
    {
        Assert.Equal("a b\r\nc", Apply("a\r\nb\r\nc", GmlLineEdits.JoinLines("a\r\nb\r\nc", 0, 0)));
        Assert.Equal("a b", Apply("a\r\nb", GmlLineEdits.JoinLines("a\r\nb", 1, 1)));
        Assert.Equal("a\r\nc\r\nd", Apply("a\r\nb\nc\r\nd", GmlLineEdits.DeleteLines("a\r\nb\nc\r\nd", 3, 3)));
        Assert.Equal("a\nb", Apply("a\nb\r\nc", GmlLineEdits.DeleteLines("a\nb\r\nc", 5, 5)));
        Assert.Equal("a\r\nc\nb", Apply("a\r\nb\nc", GmlLineEdits.MoveLines("a\r\nb\nc", 3, 3, up: false)));
        Assert.Equal("b\r\na\nc", Apply("a\r\nb\nc", GmlLineEdits.MoveLines("a\r\nb\nc", 3, 3, up: true)));
    }

    [Fact]
    public void TrimmingWhitespaceIsOneEditPerLineAndLeavesTheOtherTextAlone()
    {
        var text = "a;  \r\nb\t\r\n  \r\nc";
        var edits = GmlLineEdits.TrimTrailingWhitespace(text, 0, 0);
        Assert.Equal([new TextEdit(2, 2, ""), new TextEdit(7, 1, ""), new TextEdit(10, 2, "")], edits);
        Assert.Equal(3, GmlLineEdits.TrimTrailingWhitespace(text, 0, text.Length).Count);
    }

    [Fact]
    public async Task GoToMatchingBracketJumpsInBothDirections()
    {
        await WithTab((tab, _, _) =>
        {
            tab.Editor.CaretOffset = 3;
            Assert.True(tab.GoToMatchingBracket()); // on the opening bracket: after the closing one
            Assert.Equal(9, tab.Editor.CaretOffset);
            Assert.True(tab.GoToMatchingBracket()); // after the closing bracket: before the opening one
            Assert.Equal(3, tab.Editor.CaretOffset);
            tab.Editor.CaretOffset = 8; // before the closing bracket
            Assert.True(tab.GoToMatchingBracket());
            Assert.Equal(3, tab.Editor.CaretOffset);
            return Task.CompletedTask;
        }, "foo(a, b);\n");
    }

    [Fact]
    public async Task AFailedApplyKeepsItsErrorsWhenALiveCheckWasInFlight()
    {
        await WithTab(async (tab, workspace, _) =>
        {
            tab.Editor.Text = "return (";
            var live = tab.CheckLiveAsync();
            Assert.False(tab.Apply());
            var changes = 0;
            workspace.Problems.Changed += () => changes++;
            await live;
            Assert.Equal(0, changes);
            Assert.False(tab.LivePending);
        }, "return 1;");
    }

    [Fact]
    public async Task ATabShownAgainOwesTheLiveCheckItMissed()
    {
        await WithTab(async (tab, _, window) =>
        {
            await tab.Ready;
            tab.Editor.Text = "if (b {\n}";
            Assert.True(tab.LivePending);
            window.Content = null; // hidden: the pending check is cancelled
            window.UpdateLayout();
            Assert.False(tab.LivePending);
            window.Content = tab;
            Assert.True(tab.LivePending);
        }, "return 1;");
    }

    [Fact]
    public async Task AParserThatThrowsClearsTheProblemsOfTheEntry()
    {
        await WithTab(async (tab, workspace, _) =>
        {
            tab.Editor.Text = "if (b {\n}";
            await tab.CheckLiveAsync();
            Assert.Contains(workspace.Problems.All, p => p.Source == "gml_Script_probe");
            tab.LiveParser = _ => throw new InvalidOperationException("parser failed");
            tab.Editor.Text = "if (b {\n} ";
            await tab.CheckLiveAsync();
            Assert.DoesNotContain(workspace.Problems.All, p => p.Source == "gml_Script_probe");
        }, "return 1;");
    }

    [Fact]
    public void TheRestParameterIsShownPastTheNamedOnes()
    {
        var entry = GmlReference.Default.Entries.First(e => e.Variadic && e.Parameters.Any(p => p.Rest) && e.Parameters.Any(p => !p.Rest));
        var named = entry.Parameters.Where(p => !p.Rest).ToList();
        var signature = Signature.FromReference(entry);
        Assert.StartsWith(named[0].Name + ":", GmlHelp.ActiveParameter(signature, 0), StringComparison.Ordinal);
        Assert.StartsWith(entry.Parameters.First(p => p.Rest).Name + ":", GmlHelp.ActiveParameter(signature, named.Count + 2), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TabStopsEndWhenTheCaretLeavesAnEditHitsOutsideOrShiftTabHasNowhereToGo()
    {
        await HeadlessApp.Run(() =>
        {
            var editor = new AvaloniaEdit.TextEditor();
            var window = new Window { Content = editor, Width = 600, Height = 300 };
            window.Show();
            var completion = new GmlCompletion(editor, () => []);
            var call = CallSnippet.For(Signature.FromCounts("foo", 2, 2));

            void Start()
            {
                editor.Text = "x = " + call.Text + ";\nlast";
                completion.StartStops(call, 4);
            }

            static KeyEventArgs Press(Key key, KeyModifiers modifiers) => new() { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };

            // The editor's own handlers run later and handle Tab too: what counts is the state after the completion's tunnel handler.
            var seen = false;
            editor.TextArea.AddHandler(InputElement.KeyDownEvent, (_, e) => seen = e.Handled, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            Start();
            Assert.True(completion.InParameters);
            var shiftTab = Press(Key.Tab, KeyModifiers.Shift);
            editor.TextArea.RaiseEvent(shiftTab);
            Assert.False(seen); // first stop: nothing before it
            Assert.True(completion.InParameters);
            Assert.Equal("argument0", editor.SelectedText);
            var tab = Press(Key.Tab, KeyModifiers.None);
            editor.TextArea.RaiseEvent(tab);
            Assert.Equal("argument1", editor.SelectedText);

            editor.CaretOffset = editor.Document.TextLength; // a click elsewhere
            Assert.False(completion.InParameters);
            var free = Press(Key.Tab, KeyModifiers.None);
            editor.TextArea.RaiseEvent(free);
            Assert.False(seen);

            Start();
            editor.Document.Insert(0, "z"); // an edit before the call
            Assert.False(completion.InParameters);

            Start();
            editor.Text = "other"; // a load
            Assert.False(completion.InParameters);
            window.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheReferencePanelTakesTheLanguageChangedWhileItWasDetached()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            try
            {
                var panel = new ReferencePanel(GmlReference.Default);
                var window = new Window { Content = panel, Width = 500, Height = 600 };
                window.Show();
                window.Content = null;
                window.UpdateLayout();
                Loc.Current.SetLanguage("ru");
                window.Content = panel;
                panel.Show("string_length");
                Assert.Equal(GmlReference.Default.For("ru").Find("string_length")!.Summary, panel.Selected!.Summary);
                var category = panel.GetVisualDescendantsOfType<ComboBox>().Single(c => c.Name == "ReferenceCategory");
                Assert.Equal(Loc.Current["reference.allCategories"], ((IEnumerable<string>)category.ItemsSource!).First());
                window.Close();
            }
            finally
            {
                Loc.Current.SetLanguage("en");
            }

            return Task.CompletedTask;
        });
    }
}

internal static class VisualQuery
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}
