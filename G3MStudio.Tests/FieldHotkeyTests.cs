using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

#pragma warning disable CA2000 // windows made for a key test only

namespace G3MStudio.Tests;

/// <summary>In a text field the standard keys edit the text; the window never turns them into file-wide commands (undo, select all, and so on).</summary>
public sealed class FieldHotkeyTests
{
    private static void Press(Window window, Key key, RawInputModifiers extra = default, bool codeEditor = false)
    {
        // The text boxes of Avalonia answer to the command key of the platform settings, which the headless platform keeps at Ctrl on every system
        // The code editor (AvaloniaEdit) binds its own keys by the operating system instead, so it takes the command key of the real platform
        var modifiers = (codeEditor ? HeadlessApp.Command : (RawInputModifiers)Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers) | extra;
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Focus(Control control)
    {
        control.Focus();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task CtrlZYXCVAActOnTheTextOfABoxANumberFieldAndTheCodeEditorNotOnTheFile()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var userData = new UserData(dir.File("user"));
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
            window.Show();
            try
            {
                await window.OpenAsync(path);
                for (var i = 0; i < 300 && window.Workspaces.Current is null; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }

                var executed = new List<string>();
                window.Registry.Executed += executed.Add;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var filter = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "FilterBox");
                var number = new NumericUpDown { Minimum = 0, Maximum = 99999, Value = 123 };
                var editor = new TextEditor { Text = "abc", Height = 40 };
                var plain = new TextBox { Text = "abc" };
                var host = window.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "WorkArea");
                host.Children.Add(new StackPanel { Children = { number, editor, plain }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top });
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var inner = number.GetVisualDescendants().OfType<TextBox>().First();

                foreach (var box in new[] { filter, plain, inner })
                {
                    box.Text = "abc";
                    Focus(box);
                    Press(window, Key.A);
                    Assert.True(3 == Math.Abs(box.SelectionEnd - box.SelectionStart), $"select all on {box.Name ?? box.GetType().Name}: selection {box.SelectionStart}-{box.SelectionEnd}, focused {window.FocusManager?.GetFocusedElement()?.GetType().Name}, command {Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers}, select-all gesture {Application.Current!.PlatformSettings!.HotkeyConfiguration.SelectAll.FirstOrDefault()}");
                    Press(window, Key.C);
                    Press(window, Key.X);
                    Assert.Equal("", box.Text);
                    Press(window, Key.Z);
                    Assert.Equal("abc", box.Text);
                    Press(window, Key.Y);
                    Assert.Equal("", box.Text);
                    Press(window, Key.V);
                    Assert.Equal("abc", box.Text);
                }

                Focus(editor.TextArea);
                Press(window, Key.A, codeEditor: true);
                Assert.Equal(3, editor.SelectionLength);
                Press(window, Key.X, codeEditor: true);
                Assert.Equal("", editor.Text);
                Press(window, Key.Z, codeEditor: true);
                Assert.Equal("abc", editor.Text);
                Press(window, Key.Y, codeEditor: true);
                Assert.Equal("", editor.Text);
                Press(window, Key.V, codeEditor: true);
                Assert.Equal("abc", editor.Text);

                Assert.DoesNotContain(executed, id => id.StartsWith("edit.", StringComparison.Ordinal));
            }
            finally { window.Close(); }
        });
    }
}
