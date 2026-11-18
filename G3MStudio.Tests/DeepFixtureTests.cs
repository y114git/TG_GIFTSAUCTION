using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>A generated data file for the parent script and Peek checks: a global script with a function deep in the file, and its child scripts.</summary>
public sealed class DeepFixtureTests
{
    private const string Lib = "gml_GlobalScript_deeplib";

    /// <summary>Sixty small functions, then the one the checks look for, then one that calls it. Set G3MSTUDIO_FIXTURE_DIR to keep the file for a look in the real window.</summary>
    internal static string Write(string folder)
    {
        using var data = TestData.Create(1, true);
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < 60; i++) text.Append(System.Globalization.CultureInfo.InvariantCulture, $"function filler_{i}() {{ return {i}; }}\n");
        text.Append("function deep_target(a) { return a + 1; }\n");
        text.Append("function deep_caller() { return deep_target(5); }\n");
        var group = new CodeImportGroup(data);
        group.QueueReplace(Lib, text.ToString());
        group.Import();
        return TestData.Write(Path.Combine(folder, "deep.win"), data);
    }

    [Fact]
    public async Task PeekStartsAtTheRealLineOfAFunctionDeepInTheFileAndGoToParentThenAssemblyShowsTheParent()
    {
        if (Environment.GetEnvironmentVariable("G3MSTUDIO_FIXTURE_DIR") is { Length: > 0 } keep) Write(keep);
        using var dir = new TempDir();
        var path = Write(Directory.CreateDirectory(dir.File("fixture")).FullName);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, Lib);
            var window = new Window { Content = tab, Width = 800, Height = 500 };
            window.Show();
            try
            {
                await tab.Ready;
                tab.Editor.CaretOffset = tab.Text.IndexOf("deep_target(5)", StringComparison.Ordinal) + 2;
                tab.Run("code.peekDefinition");
                for (var i = 0; i < 100 && tab.PeekText is null; i++) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.NotNull(tab.PeekText);
                Assert.Equal(tab.Text, tab.PeekText);
                Assert.Equal(1 + tab.Text[..tab.Text.IndexOf("function deep_target", StringComparison.Ordinal)].Count(c => c == '\n'), tab.PeekLine);
                Assert.True(tab.PeekLine > 250, $"{tab.PeekLine}"); // the real line far down the file, not 1
                for (var i = 0; i < 100 && tab.PeekTopLine <= 1; i++) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.True(tab.PeekTopLine >= tab.PeekLine - 1 && tab.PeekTopLine <= tab.PeekLine, "top=" + tab.PeekTopLine); // scrolled so the definition is the first line; the text above is still in the document
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheContextMenuOffersGoToAndPeekForAFunctionDeclaredInTheSameFile()
    {
        using var dir = new TempDir();
        var path = Write(Directory.CreateDirectory(dir.File("fixture")).FullName);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, Lib);
            var window = new Window { Content = tab, Width = 800, Height = 500 };
            window.Show();
            try
            {
                await tab.Ready;
                var offset = tab.Text.IndexOf("deep_target(5)", StringComparison.Ordinal) + 2;
                var at = tab.Editor.Document.GetLocation(offset);
                tab.Editor.ScrollTo(at.Line, at.Column);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var view = tab.Editor.TextArea.TextView;
                var spot = view.GetVisualPosition(new AvaloniaEdit.TextViewPosition(at), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle) - view.ScrollOffset;
                var where = view.TranslatePoint(spot, window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseMove(window, where);
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, where, Avalonia.Input.MouseButton.Right);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, where, Avalonia.Input.MouseButton.Right);
                Dispatcher.UIThread.RunJobs();
                var menu = tab.Editor.ContextMenu!;
                var items = menu.Items.OfType<MenuItem>().ToList();
                Assert.True(items[0].IsEnabled, "Go to Definition");
                Assert.True(items[1].IsEnabled, "Peek Definition");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheHoverCardNamesTheCodeEntryByItsNickname()
    {
        using var dir = new TempDir();
        var path = Write(Directory.CreateDirectory(dir.File("fixture")).FullName);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, Lib);
            var window = new Window { Content = tab, Width = 800, Height = 500 };
            window.Show();
            try
            {
                await tab.Ready;
                var offset = tab.Text.IndexOf("deep_target(5)", StringComparison.Ordinal) + 2;
                Assert.Contains(Lib, tab.HoverTextAt(offset), StringComparison.Ordinal);
                Assert.True(workspace.Nicknames.Set("code:" + Lib, "Deep lib"));
                var card = tab.HoverTextAt(offset);
                Assert.Contains("Deep lib", card, StringComparison.Ordinal);
                Assert.DoesNotContain(Lib, card, StringComparison.Ordinal);
            }
            finally { window.Close(); }
        });
    }
}
