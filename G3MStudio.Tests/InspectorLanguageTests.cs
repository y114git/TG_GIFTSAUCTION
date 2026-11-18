using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Panels;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

public sealed class InspectorLanguageTests
{
    [Fact]
    public async Task ASelectedResourceKeepsItsNameWhenTheLanguageChanges()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var panel = new InspectorPanel(() => { });
            var window = new Window { Content = panel, Width = 400, Height = 500 };
            window.Show();
            try
            {
                await panel.ShowAsync(workspace, "scripts:scr_0");
                Loc.Current.SetLanguage("ru");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("scr_0", panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "InspectorName").Text);
                await panel.ShowAsync(null, null);
                Assert.Equal(Loc.Current["inspector.none"], panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "InspectorName").Text);
                Loc.Current.SetLanguage("ja");
                Assert.Equal(Loc.Current["inspector.none"], panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "InspectorName").Text);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ASelectedFolderShowsItsNameItsKindAndItsEntryCount()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var panel = new InspectorPanel(() => { });
            var window = new Window { Content = panel, Width = 400, Height = 500 };
            window.Show();
            try
            {
                panel.ShowRow(new ExplorerRow(ExplorerRowKind.Folder, "folder:1", "Heroes", 1) { Count = 3 });
                string Text(string name) => panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text!;
                Assert.Equal(("Heroes", "Folder"), (Text("InspectorName"), Text("InspectorType")));
                Assert.Equal("3 resources", Text("InspectorUses"));
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
