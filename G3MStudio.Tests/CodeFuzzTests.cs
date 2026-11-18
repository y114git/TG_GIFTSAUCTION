#nullable disable
#pragma warning disable CA2000, CA5394, CA1849, IDE0305
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>
/// Random edits, undo, redo, paste, folds, searches and look-ups on one code tab while the timers (colours, live check, folds) run between the steps:
/// every helper that keeps offsets, tokens or lines of an older text must survive the text changing under it.
/// </summary>
public sealed class CodeFuzzTests(ITestOutputHelper output)
{
    private static readonly string[] Snippets =
    [
        "{", "}", "(", ")", "[", "]", "\"", "'", "/*", "*/", "//", "#region x\n", "#endregion\n", "function f(a, b) {\n", "var x = 1;\n", "enum E { A, B }\n", "#macro M 5\n",
        "\n\n\n", "\t", "if (a) {\n} else {\n}\n", "show_debug_message(\"hi\");", "struct = { a: 1, b: [1, 2] };", "0xFF", "$ff00ff", "@\"x\"", "漢字", "",
    ];

    private static string Script()
    {
        var text = new StringBuilder("#macro LIMIT 10\nenum Mode { A, B, C }\n#region setup\n");
        for (var i = 0; i < 40; i++)
        {
            text.Append("function fn_").Append(i).Append("(a, b = 2) {\n    var total = a + b * LIMIT;\n    // note ").Append(i).Append("\n    if (total > 3) {\n        show_debug_message(\"v\" + string(total));\n    }\n");
            text.Append("    var s = { x: 1, y: [1, 2, 3], z: function() { return Mode.A; } };\n    /* block\n       comment */\n    return fn_").Append((i + 1) % 40).Append("(total, s.x);\n}\n\n");
        }

        return text.Append("#endregion\n").ToString();
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public async Task ACodeTabSurvivesRandomEditsUndoPasteAndLookups(int seed)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var random = new Random(seed);
        var path = TestData.WriteSample(dir, withCode: true);
        var log = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                var tab = window.Docking.OpenTab("code", "code:gml_Script_probe", false);
                await CommandSweep.Pump(5);
                var code = window.GetVisualDescendants().OfType<CodeEditorTab>().Single();
                await code.Ready;
                code.Editor.IsReadOnly = false;
                code.Editor.Document.Text = Script();
                window.UpdateLayout();
                code.Editor.TextArea.Focus();
                var commands = window.Registry.All.Where(c => c.Id.StartsWith("code.", StringComparison.Ordinal) && !CommandSweep.Special.Contains(c.Id)).ToList();
                for (var step = 0; step < 400; step++)
                {
                    var what = Act(window, probe, code, commands, random);
                    log.Add(what);
                    probe.Where = $"seed {seed} step {step} {what}";
                    if (step % 10 == 9) await Task.Delay(random.Next(5) == 0 ? 450 : 5); // lets the debounced timers fire between edits
                    await CommandSweep.Pump();
                    await CommandSweep.CloseDialogs(window, probe);
                    if (window.GetVisualDescendants().OfType<CodeEditorTab>().FirstOrDefault() != code) break;
                }

                output.WriteLine($"seed {seed}: {log.Count} steps, {code.Editor.Document.TextLength} chars at the end");
            }
            finally
            {
                window.Close();
            }
        });
        SweepProbe.Flush();
        Assert.True(probe.Errors.Count == 0, $"seed {seed}, last steps: {string.Join(" | ", log.TakeLast(6))}\n{string.Join("\n", probe.Errors.Take(5))}");
    }

    private static string Act(MainWindow window, SweepProbe probe, CodeEditorTab code, List<G3MStudio.Core.Commands.Command> commands, Random random)
    {
        var editor = code.Editor;
        var roll = random.Next(100);
        var length = editor.Document.TextLength;
        string what;
        Action action;
        if (roll < 25)
        {
            var text = Snippets[random.Next(Snippets.Length)];
            what = $"type '{text.Replace("\n", "\\n", StringComparison.Ordinal)}'";
            action = () => editor.TextArea.PerformTextInput(text);
        }
        else if (roll < 45)
        {
            var (key, mods) = (new[] { Key.Z, Key.Z, Key.Y, Key.X, Key.C, Key.A, Key.Delete, Key.Back, Key.Enter, Key.Tab, Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.End, Key.Escape, Key.F3, Key.F, Key.H }[random.Next(20)], new[] { RawInputModifiers.None, RawInputModifiers.Control, RawInputModifiers.Control, RawInputModifiers.Shift, RawInputModifiers.Control | RawInputModifiers.Shift }[random.Next(5)]);
            what = $"key {mods}+{key}";
            action = () => window.KeyPress(key, mods, PhysicalKey.None, null);
        }
        else if (roll < 55)
        {
            var text = Snippets[random.Next(Snippets.Length)] + (random.Next(3) == 0 ? Script()[..random.Next(200, 2000)] : "");
            what = $"paste {text.Length} chars";
            action = () =>
            {
                var start = random.Next(length + 1);
                editor.Select(start, random.Next(Math.Min(60, length - start + 1)));
                _ = TopLevel.GetTopLevel(editor).Clipboard.SetTextAsync(text);
                Dispatcher.UIThread.RunJobs();
                editor.Paste();
            };
        }
        else if (roll < 65)
        {
            var at = random.Next(length + 1);
            what = $"hover at {at}";
            action = () => code.ShowHoverAt(at);
        }
        else if (roll < 80)
        {
            var command = commands[random.Next(commands.Count)];
            what = "command " + command.Id;
            editor.CaretOffset = Math.Min(editor.CaretOffset, editor.Document.TextLength);
            action = () => window.Registry.Execute(command.Id);
        }
        else if (roll < 88)
        {
            var (start, end) = (random.Next(length + 1), random.Next(length + 1));
            what = $"select {start}..{end} and replace";
            action = () =>
            {
                editor.Select(Math.Min(start, end), Math.Abs(end - start));
                editor.SelectedText = random.Next(2) == 0 ? "" : Snippets[random.Next(Snippets.Length)];
            };
        }
        else
        {
            var at = random.Next(length + 1);
            what = $"caret to {at}";
            action = () =>
            {
                editor.CaretOffset = at;
                _ = G3MStudio.Editor.GmlStructure.MatchingBrackets(code.Doc, at);
                _ = code.HoverTextAt(at);
            };
        }

        probe.Where = what;
        CommandSweep.Step(probe, what, action);
        return what;
    }
}
