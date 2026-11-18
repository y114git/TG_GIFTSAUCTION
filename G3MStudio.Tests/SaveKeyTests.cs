using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class SaveKeyTests
{
    private static KeyEventArgs CtrlS() => new() { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = Shortcuts.Command };

    [Fact]
    public async Task CtrlSReachesTheFileSaveFromTheCodeEditorATextBoxAndTheTree()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("code", "code:gml_Script_probe", false);
                Dispatcher.UIThread.RunJobs();
                var tab = window.GetVisualDescendants().OfType<CodeEditorTab>().Single();
                await tab.Ready;
                var asked = 0;
                window.SaveAsker = _ => { asked++; return Task.FromResult<(bool? Sync, bool Remember)?>((null, false)); };
                var box = window.GetVisualDescendants().OfType<TextBox>().First();
                var sources = new Dictionary<string, InputElement> { ["code editor"] = tab.Editor.TextArea, ["text box"] = box, ["tree"] = window.Explorer };
                foreach (var (name, source) in sources)
                {
                    Assert.False(tab.HasPending);
                    var before = asked;
                    var e = CtrlS();
                    source.RaiseEvent(e);
                    Assert.True(e.Handled, name);
                    await CodeIntelUiTests.Settle(() => asked > before);
                    Assert.True(asked > before, name);
                    await Task.Delay(50, TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                }

                // The same through the window's real input path, with the editor focused.
                tab.Editor.TextArea.Focus();
                Dispatcher.UIThread.RunJobs();
                var count = asked;
                window.KeyPress(Key.S, HeadlessApp.Command, Avalonia.Input.PhysicalKey.S, "s");
                await CodeIntelUiTests.Settle(() => asked > count);
                Assert.True(asked > count, "window key press");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CtrlSSavesATypedShaderSourceInsteadOfAskingToSaveTheFile()
    {
        using var dir = new TempDir();
        var path = dir.File("uty.win");
        File.Copy(Path.Combine(Corpus.Fixtures, "uty", "data.win"), path);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "shaders:shd_u000", false);
                Dispatcher.UIThread.RunJobs();
                var view = window.GetVisualDescendants().OfType<ShaderSourceView>().Single();
                var asked = 0;
                window.SaveAsker = _ => { asked++; return Task.FromResult<(bool? Sync, bool Remember)?>((null, false)); };
                var box = ((StackPanel)((TabItem)view.Items[0]!).Content!).Children.OfType<AvaloniaEdit.TextEditor>().Single();
                box.Text += " // typed";
                var other = ((StackPanel)((TabItem)view.Items[^1]!).Content!).Children.OfType<AvaloniaEdit.TextEditor>().Single(); // a source behind the front one
                other.Text += " // typed too";
                Assert.True(view.Items.Count > 1 && view.Unsaved().Count == 2);
                box.TextArea.Focus();
                Dispatcher.UIThread.RunJobs();
                window.KeyPress(Key.S, HeadlessApp.Command, PhysicalKey.S, "s");
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(view.Unsaved());
                Assert.Equal(0, asked);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
