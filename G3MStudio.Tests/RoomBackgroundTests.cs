using G3MLib.DataFile;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class RoomBackgroundTests
{
    [Fact]
    public async Task ARoomOfGameMakerStudio1DrawsItsEnabledBackgroundsAndFillColor()
    {
        var folder = Corpus.Folder;
        foreach (var file in Corpus.Files(folder))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var room = data.Rooms.FirstOrDefault(r => r.Layers is not { Count: > 0 } && r.Backgrounds.Any(b => b is { Enabled: true } && b.BackgroundDefinition?.Texture?.TexturePage is not null));
            if (room is null) continue;
            await HeadlessApp.Run(() =>
            {
                using var scene = RoomScene.Build(room);
                var background = room.Backgrounds.First(b => b is { Enabled: true } && b.BackgroundDefinition?.Texture?.TexturePage is not null);
                var tpag = background.BackgroundDefinition.Texture;
                Assert.Contains(scene.Items, i => i.Element is null && i.Source.Width == tpag.SourceWidth && i.Source.Height == tpag.SourceHeight);
                Assert.Equal(room.DrawBackgroundColor ? room.BackgroundColor : null, scene.FillColor);
                return Task.CompletedTask;
            });
            return;
        }

        Assert.Fail("No corpus game has a Game Maker Studio 1 room with an enabled background.");
    }
}
