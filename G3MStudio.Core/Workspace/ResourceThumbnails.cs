using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Which picture stands for a resource in the Explorer tooltip: frame 0 of a sprite, the picture of a tile set or a font.</summary>
public static class ResourceThumbnails
{
    public static GameMakerTexturePageItem? ItemOf(object? resource) => resource switch
    {
        GameMakerSprite sprite => sprite.Textures.FirstOrDefault()?.Texture,
        GameMakerBackground background => background.Texture,
        GameMakerFont font => font.Texture,
        _ => null,
    };
}
