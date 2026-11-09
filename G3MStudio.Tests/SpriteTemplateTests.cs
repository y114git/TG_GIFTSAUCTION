#pragma warning disable CA2000 // the data file owns what the command makes
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SpriteTemplateTests
{
    [Fact]
    public async Task ANewSpriteHasOneTransparentFrameSurvivesAWriteAndARead_AndUndoTakesItsPageBack()
    {
        var folder = Corpus.Folder;
        var tried = 0;
        foreach (var candidate in Corpus.Files(folder))
        {
            using var data = await GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (SpriteTemplate.GroupOf(data) is not { } group) continue;
            tried++;
            var (sprites, pages, items, groupPages) = (data.Sprites.Count, data.EmbeddedTextures.Count, data.TexturePageItems.Count, group.TexturePages.Count);
            var command = EditCommand.Create(ResourceListCommand.Name, new ResourceListCommand("sprites", ResourceListCommand.New, "spr_zz_new").Args);
            command.Apply(data);
            var sprite = data.Sprites.Single(s => s.Name.Content == "spr_zz_new");
            Assert.Equal((32u, 32u), (sprite.Width, sprite.Height));
            Assert.Single(sprite.Textures);
            var frame = FramePicture.Read(data, "spr_zz_new", 0);
            Assert.Equal((32, 32), (frame.Width, frame.Height));
            Assert.True(frame.Data.Where((_, i) => i % 4 == 3).All(a => a == 0)); // every pixel transparent
            Assert.Equal((sprites + 1, pages + 1, items + 1, groupPages + 1), (data.Sprites.Count, data.EmbeddedTextures.Count, data.TexturePageItems.Count, group.TexturePages.Count));
            Assert.ThrowsAny<InvalidOperationException>(() => new ResourceListCommand("sprites", ResourceListCommand.New, "spr_zz_new").Apply(data)); // the name is taken

            using var stream = new MemoryStream();
            await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
            stream.Position = 0;
            using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            var read = again.Sprites.Single(s => s.Name.Content == "spr_zz_new");
            Assert.Equal((32u, 32u), (read.Width, read.Height));
            Assert.Equal((pages + 1, items + 1), (again.EmbeddedTextures.Count, again.TexturePageItems.Count));
            Assert.Equal(frame.Data, FramePicture.Read(again, "spr_zz_new", 0).Data);

            command.Revert(data);
            Assert.Equal((sprites, pages, items, groupPages), (data.Sprites.Count, data.EmbeddedTextures.Count, data.TexturePageItems.Count, group.TexturePages.Count));
            if (tried >= 2) return; // the smallest two games that have texture groups
        }

        Assert.False(tried == 0, "No corpus game has texture groups.");
    }
}
