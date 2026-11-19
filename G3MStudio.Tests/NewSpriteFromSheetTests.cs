using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using ImageMagick;

#pragma warning disable CA2000 // the window is closed by the test
namespace G3MStudio.Tests;

/// <summary>New Sprite from Sheet or Strip: a generated strip becomes the frames of a new sprite named after the file, in one undo step.</summary>
public sealed class NewSpriteFromSheetTests
{
    [Fact]
    public async Task AStripBecomesANewSpriteAndUndoRemovesIt()
    {
        using var dir = new TempDir();
        string path;
        using (var data = TestData.Create(0))
        {
            TestData.AddPage(data);
            path = TestData.Write(dir.File("data.win"), data);
        }

        var strip = dir.File("hero_strip3.png");
        using (var image = new MagickImage(MagickColors.Red, 48, 16)) await image.WriteAsync(strip, MagickFormat.Png, TestContext.Current.CancellationToken);
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

                var workspace = window.Workspaces.Current!;
                window.SheetImportOpened += dialog => { Dispatcher.UIThread.Post(() => dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "SheetOk").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))); };
                await window.ImportSheetAsync(true, strip);
                var sprite = workspace.Data.Sprites.Single(s => s.Name.Content == "hero_strip3");
                Assert.Equal(3, sprite.Textures.Count);
                Assert.Equal((16u, 16u), (sprite.Width, sprite.Height));
                workspace.Undo();
                Assert.DoesNotContain(workspace.Data.Sprites, s => s.Name.Content == "hero_strip3");
            }
            finally
            {
                window.Workspaces.Close();
                window.Close();
            }
        });
    }
}
