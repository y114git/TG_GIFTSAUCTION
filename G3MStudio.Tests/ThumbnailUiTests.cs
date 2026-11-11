#pragma warning disable CA2000 // windows and bitmaps made for a lookup only
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class ThumbnailUiTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ThumbnailsOnADataFileShowAPictureForSpritesRoomsAndFonts()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var ws = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(Corpus.Files(Corpus.Folder).First(), cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = ThumbnailTests.Shared(ws, textures);
            var sprite = ws.Data.Sprites.FirstOrDefault(s => ResourceThumbnails.KindOf(s) == ThumbnailKind.Item && s.Textures[0].Texture is { SourceWidth: > 0 });
            if (sprite is not null) Assert.NotNull(await service.GetAsync(ThumbnailTests.Key("sprites", sprite.Name.Content!)));
            var font = ws.Data.Fonts.FirstOrDefault(f => f.Texture is { SourceWidth: > 0 });
            if (font is not null) Assert.NotNull(await service.GetAsync(ThumbnailTests.Key("fonts", font.Name.Content!)));
            var room = ws.Data.Rooms.FirstOrDefault();
            if (room is not null) Assert.NotNull(await service.GetAsync(ThumbnailTests.Key("rooms", room.Name.Content!)));
        });
    }

    [Fact]
    public async Task TheBigTreeBuildsInTimeWithThumbnailsOnAndEveryRowShowsAnIcon()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            using var data = ExplorerModelTests.Generate(Perf.Count(50_000));
            var view = new ExplorerView();
            var window = new Window { Width = 400, Height = 700, Content = view };
            window.Show();
            try
            {
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
                var load = Stopwatch.StartNew();
                view.Load("big.win", data);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                output.WriteLine($"load and first layout with thumbnails on: {load.ElapsedMilliseconds} ms");
                Assert.True(load.ElapsedMilliseconds < 5000, "the thumbnails must not slow the tree");
                var items = view.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is ExplorerRow { IsItem: true }).ToList();
                Assert.NotEmpty(items);
                Assert.All(items, i => Assert.NotNull(i.GetVisualDescendants().OfType<Image>().First(x => x.Name == "Thumb").Source));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ARowOfAPictureTypeSwapsItsIconForThePicture()
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            using var ws = ThumbnailTests.Generate();
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = ThumbnailTests.Shared(ws, textures);
            var view = new ExplorerView { Pictures = service };
            var window = new Window { Width = 400, Height = 700, Content = view };
            window.Show();
            try
            {
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
                view.Load("t.win", ws.Data);
                view.Select("sprites:spr_a"); // scrolls it into view
                for (var i = 0; i < 40; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var row = view.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.DataContext is ExplorerRow { IsItem: true, Key: "sprites" });
                var source = row.GetVisualDescendants().OfType<Image>().First(x => x.Name == "Thumb").Source;
                Assert.IsNotType<Avalonia.Media.DrawingImage>(source); // the picture, not the type icon
                Assert.NotNull(source);
            }
            finally { window.Close(); }
        });
    }
}
