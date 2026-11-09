using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>Pictures of dialogs, to check their look by eye. The pictures go to G3MSTUDIO_SHOTS, or to a temp folder when it is not set.</summary>
public sealed class DialogShotTests
{
    [Fact]
    public async Task TheRuntimeChoiceShowsVersionAndPathColumns()
    {
        var folder = Corpus.Shots;
        Directory.CreateDirectory(folder);
        using var dir = new TempDir();
        foreach (var version in new[] { "2024.1.0.1", "2023.8.0.0" })
        {
            var runner = dir.File(Path.Combine("runtimes", "runtime-" + version, "windows", "x64", "Runner.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(runner)!);
            await File.WriteAllTextAsync(runner, "", TestContext.Current.CancellationToken);
        }
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Workspaces.Settings.Update(x => x with { RuntimeFolders = dir.File("runtimes") });
            try
            {
                window.Show();
                var pick = window.RunWithRuntimeAsync();
                Window? dialog = null;
                for (var i = 0; i < 20 && dialog is null; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                    dialog = window.OwnedWindows.Count > 0 ? window.OwnedWindows[0] : null;
                }

                Assert.NotNull(dialog);
                var list = dialog.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "ChoiceList");
                list.SelectedIndex = 1;
                Dispatcher.UIThread.RunJobs();
                dialog.UpdateLayout();
                using var target = new RenderTargetBitmap(new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
                target.Render(dialog);
                target.Save(Path.Combine(folder, "20-runtime-choice.png"), new PngBitmapEncoderOptions());
                dialog.Close();
                await pick;
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheRenameDialogPreviewsTheChanges()
    {
        var folder = Corpus.Shots;
        var corpus = Corpus.Folder;
        Directory.CreateDirectory(folder);
        var file = Corpus.Files(corpus).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                await window.OpenAsync(file);
                var data = window.Workspaces.Current!.Data;
                var obj = data.GameObjects.First(o => o.Events.Any(e => e.Count > 0)).Name.Content!;
                window.Explorer.Select(G3MStudio.Core.Workspace.ResourceTypes.KeyOf("objects", obj));
                window.Rename();
                Window? dialog = null;
                for (var i = 0; i < 20 && dialog is null; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                    dialog = window.OwnedWindows.Count > 0 ? window.OwnedWindows[0] : null;
                }

                Assert.NotNull(dialog);
                dialog.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "NewName").Text = obj + "_renamed";
                var preview = dialog.GetVisualDescendants().OfType<TextBlock>().Single(b => b.Name == "Preview");
                for (var i = 0; i < 100 && string.IsNullOrEmpty(preview.Text); i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.False(string.IsNullOrEmpty(preview.Text));
                dialog.UpdateLayout();
                using var target = new RenderTargetBitmap(new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
                target.Render(dialog);
                target.Save(Path.Combine(folder, "21-rename-preview.png"), new PngBitmapEncoderOptions());
                dialog.Close();
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task SeveralSelectedResourcesDeleteAfterOneQuestion()
    {
        var folder = Corpus.Shots;
        var corpus = Corpus.Folder;
        Directory.CreateDirectory(folder);
        var file = Corpus.Files(corpus).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 1100;
                window.Height = 700;
                window.Show();
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var names = workspace.Data.GameObjects.Where(o => o.Name.Content is { Length: > 0 }).Take(3).Select(o => o.Name.Content!).ToList();
                window.Explorer.Select(G3MStudio.Core.Workspace.ResourceTypes.KeyOf("objects", names[0]));
                Dispatcher.UIThread.RunJobs();
                var list = window.Explorer.GetVisualDescendants().OfType<ListBox>().First();
                foreach (var row in list.ItemsSource!.OfType<G3MStudio.Core.Workspace.ExplorerRow>().Where(r => r.IsItem && r.Key == "objects" && names.Contains(r.Text)))
                    if (!list.SelectedItems!.Contains(row)) list.SelectedItems.Add(row);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, window.Explorer.SelectedKeys.Count);
                ScreenshotTests.Shoot(window, Path.Combine(folder, "22-multi-select.png"));

                var count = workspace.Data.GameObjects.Count;
                window.DeleteResource();
                Window? dialog = null;
                for (var i = 0; i < 100 && dialog is null; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                    dialog = window.OwnedWindows.Count > 0 ? window.OwnedWindows[0] : null;
                }

                Assert.NotNull(dialog);
                dialog.UpdateLayout();
                using var target = new RenderTargetBitmap(new PixelSize((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
                target.Render(dialog);
                target.Save(Path.Combine(folder, "23-delete-many.png"), new PngBitmapEncoderOptions());
                dialog.Close();
                Assert.Equal(count, workspace.Data.GameObjects.Count); // closing the question without Yes deletes nothing
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ASpriteTooltipShowsItsPicture()
    {
        var folder = Corpus.Shots;
        var corpus = Corpus.Folder;
        Directory.CreateDirectory(folder);
        var file = Corpus.Files(corpus).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                await window.OpenAsync(file);
                var data = window.Workspaces.Current!.Data;
                var sprite = data.Sprites.First(s => s.Textures.Count > 0 && s.Textures[0].Texture is { SourceWidth: > 8 });
                var key = G3MStudio.Core.Workspace.ResourceTypes.KeyOf("sprites", sprite.Name.Content!);
                var picture = await window.Explorer.Thumbnail!(key);
                Assert.NotNull(picture);
                Assert.Null(await window.Explorer.Thumbnail!(G3MStudio.Core.Workspace.ResourceTypes.KeyOf("objects", data.GameObjects[0].Name.Content!)));

                var tip = new G3MStudio.Explorer.ExplorerRowTip(sprite.Name.Content!, () => Task.FromResult<Avalonia.Media.IImage?>(picture));
                var holder = new Window { Width = 220, Height = 180, Content = new Border { Padding = new Thickness(8), Child = tip } };
                holder.Show();
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                }

                using var target = new RenderTargetBitmap(new PixelSize(220, 180));
                target.Render(holder);
                target.Save(Path.Combine(folder, "25-sprite-tooltip.png"), new PngBitmapEncoderOptions());
                holder.Close();
            }
            finally { window.Close(); }
        });
    }
}