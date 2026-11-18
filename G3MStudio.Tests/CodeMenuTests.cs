using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class CodeMenuTests
{
    private static readonly string[] Languages = ["en", "ru", "es", "ja", "ko", "zh_cn", "zh_tw"];

    [Fact]
    public async Task TheCodeMenuListsCopyNameOnceWhenTheWindowAddsTheResourceCommands()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var tab = new CodeEditorTab(workspace, "gml_Script_probe") { ResourceMenu = () => [new MenuItem { Header = "Copy Name" }] };
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            try
            {
                await tab.Ready;
                var menu = tab.Editor.ContextMenu!;
                // Open is a no-op without a platform popup host in the headless session, so the Opening handler is run directly.
                var opening = (MulticastDelegate)typeof(ContextMenu).GetField("Opening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(menu)!;
                opening.DynamicInvoke(menu, new System.ComponentModel.CancelEventArgs());
                Assert.Single(menu.Items.OfType<MenuItem>(), i => i.Header as string == "Copy Name");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheReferenceTabShowsAShortTitleInEveryLanguage()
    {
        await HeadlessApp.Run(() =>
        {
            try
            {
                foreach (var language in Languages)
                {
                    Loc.Current.SetLanguage(language);
                    var dock = new StudioDock();
                    var tool = dock.Find<Dock.Model.Avalonia.Controls.Tool>("Reference")!;
                    Assert.Equal(Loc.Current["panel.referenceTab"], tool.Title);
                    Assert.True(tool.Title!.Length <= Loc.Current["panel.reference"].Length);
                }
            }
            finally { Loc.Current.SetLanguage("en"); }

            return Task.CompletedTask;
        });
    }
}
