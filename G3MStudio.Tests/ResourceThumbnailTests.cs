using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ResourceThumbnailTests
{
#pragma warning disable CA2000 // model objects made for a lookup only
    [Fact]
    public void SpritesTileSetsAndFontsHaveAPictureAndOtherResourcesDoNot()
    {
        var item = new GameMakerTexturePageItem();
        var sprite = new GameMakerSprite();
        sprite.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
        Assert.Same(item, ResourceThumbnails.ItemOf(sprite));
        Assert.Same(item, ResourceThumbnails.ItemOf(new GameMakerBackground { Texture = item }));
        Assert.Same(item, ResourceThumbnails.ItemOf(new GameMakerFont { Texture = item }));
        Assert.Null(ResourceThumbnails.ItemOf(new GameMakerSprite()));
        Assert.Null(ResourceThumbnails.ItemOf(new GameMakerGameObject()));
        Assert.Null(ResourceThumbnails.ItemOf(null));
    }
#pragma warning restore CA2000
}
