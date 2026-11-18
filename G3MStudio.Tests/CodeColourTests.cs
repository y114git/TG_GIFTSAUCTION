using Avalonia.Media;
using AvaloniaEdit;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CodeColourTests
{
    [Fact]
    public async Task ANameNothingClassifiesGetsTheVariableColourNotTheOperatorColour()
    {
        await HeadlessApp.Run(() =>
        {
            IBrush variable = new SolidColorBrush(Colors.Plum), op = new SolidColorBrush(Colors.White);
            var colorizer = new GmlColorizer(key => key switch { "Syntax.Variable" => variable, "Syntax.Operator" => op, _ => null });
            colorizer.Tokenize("abcmq = 1;");
            var first = colorizer.Tokens.Select((t, i) => (t, i)).First(x => x.t.Kind == GmlTokenKind.Identifier).i;
            Assert.Same(variable, colorizer.BrushOf(first));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TextShorterThanItsTokensStillDrawsWithoutCrashing()
    {
        await HeadlessApp.Run(() =>
        {
            var editor = GmlColorizer.CreateEditor("a = 1; bbbb = 2; cccc = 3;", false);
            var window = new Avalonia.Controls.Window { Width = 400, Height = 200, Content = editor };
            window.Show();
            try
            {
                window.UpdateLayout();
                editor.Document.Text = "a"; // undo and paste change the text before the tokens are made again
                editor.TextArea.Caret.Offset = 1;
                window.UpdateLayout();
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    // The colour of every character of a line as the view draws it now.
    private static List<(char Char, IBrush? Brush)> Paint(TextEditor editor, int lineNumber)
    {
        var line = editor.Document.GetLineByNumber(lineNumber);
        var visual = editor.TextArea.TextView.GetOrConstructVisualLine(line);
        var painted = new List<(char, IBrush?)>();
        foreach (var element in visual.Elements.Where(e => e.DocumentLength > 0))
            foreach (var c in editor.Document.GetText(visual.FirstDocumentLine.Offset + element.RelativeTextOffset, element.DocumentLength)) painted.Add((c, element.TextRunProperties?.ForegroundBrush));
        return painted;
    }

    [Fact]
    public async Task TypingInTheMiddleOfTheFileNeverChangesTheColoursOfTextTheEditDidNotTouch()
    {
        await HeadlessApp.Run(() =>
        {
            const string text = "var total = 0;\nfoo(1, 2);\nif (total > 3) { total += foo(4); }\nname = \"text\";\n// comment\nrepeat (3) { foo(total); }\nx = total + 1;\ny = foo(x) + total;\nreturn \"done\";\n";
            var editor = GmlColorizer.CreateEditor(text, false, out var colors);
            colors.Classifier = name => name == "foo" ? "Function" : null;
            colors.Tokenize(text);
            var window = new Avalonia.Controls.Window { Width = 600, Height = 400, Content = editor };
            window.Show();
            try
            {
                var view = editor.TextArea.TextView;
                List<(char Char, IBrush? Brush)>[] Snapshot() => [.. Enumerable.Range(1, editor.Document.LineCount).Select(n => Paint(editor, n))];
                window.UpdateLayout();
                var before = Snapshot();
                Assert.True(before.SelectMany(l => l).Select(c => c.Brush).Distinct().Count() > 2, "the test text has several colours");
                var baseline = before.ToDictionary(l => new string([.. l.Select(c => c.Char)]), l => l);

                // Every other line keeps its colours; the edited line keeps those of the text before and after the new characters.
                void Check(int column, int added, string step)
                {
                    view.Redraw(); // every visual line is made again, as a scroll or a resize does between a keystroke and the next tokenize
                    window.UpdateLayout();
                    var now = Snapshot();
                    for (var n = 1; n <= now.Length; n++)
                    {
                        var key = new string([.. now[n - 1].Select(c => c.Char)]);
                        if (n != 7 && baseline.TryGetValue(key, out var expected)) Assert.True(expected.SequenceEqual(now[n - 1]), $"{step}: line {n} {key} changed its colours");
                    }

                    Assert.True(before[6].Take(column).SequenceEqual(now[6].Take(column)), $"{step}: the text before the caret changed its colours");
                    Assert.True(before[6].Skip(column).SequenceEqual(now[6].Skip(column + added)), $"{step}: the text after the caret changed its colours");
                }

                const string typedAfter = "x = total + ";
                var offset = editor.Document.GetLineByNumber(7).Offset + typedAfter.Length;
                editor.Document.Insert(offset, "9");
                Check(typedAfter.Length, 1, "first key");
                editor.Document.Insert(offset + 1, "8");
                Check(typedAfter.Length, 2, "second key");
                colors.Tokenize(editor.Text); // the new tokens arrive: the same colours, swapped in at once
                Check(typedAfter.Length, 2, "retokenized");
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ThousandsOfEditsBeforeTheNextTokenizeStopTheColouringInsteadOfReplayingThemAll()
    {
        await HeadlessApp.Run(() =>
        {
            const string text = "foo(1);\nbar = 2;\n";
            var editor = GmlColorizer.CreateEditor(text, false, out var colors);
            var window = new Avalonia.Controls.Window { Width = 600, Height = 400, Content = editor };
            window.Show();
            try
            {
                int Colours() => Paint(editor, 2).Select(c => c.Brush).Distinct().Count();
                window.UpdateLayout();
                Assert.True(Colours() > 1);
                for (var i = 0; i < 100; i++) editor.Document.Insert(0, "a"); // each one apart from the last: one edit each
                editor.TextArea.TextView.Redraw();
                window.UpdateLayout();
                Assert.True(Colours() > 1, "a few hundred edits still shift the colours");
                for (var i = 0; i < 300; i++) editor.Document.Insert(0, "a");
                editor.TextArea.TextView.Redraw();
                window.UpdateLayout();
                Assert.Equal(1, Colours());
                colors.Tokenize(editor.Text);
                editor.TextArea.TextView.Redraw();
                window.UpdateLayout();
                Assert.True(Colours() > 1);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
