using G3MLib.DataFile.Models;

namespace G3MLib.DataFile.Util;

/// <summary>
/// A texture replacement whose image work is done. <see cref="Commit"/> only swaps the page image, so it is cheap enough to run inside a write lock.
/// </summary>
/// <remarks>
/// The page image was composed when <see cref="GameMakerTexturePageItem.PrepareReplace"/> ran. Two replacements prepared for the same page
/// must be committed one at a time, each prepared after the previous commit: <see cref="Commit"/> throws when the page image changed since the preparation.
/// </remarks>
public sealed class PreparedTextureReplace
{
    private readonly GameMakerTexturePageItem _item;
    private readonly GMImage _pageImage;
    private readonly GMImage _baseImage;
    private readonly ushort _targetWidth;
    private readonly ushort _targetHeight;
    private bool _committed;

    internal PreparedTextureReplace(GameMakerTexturePageItem item, GMImage baseImage, GMImage pageImage, ushort targetWidth, ushort targetHeight)
    {
        _item = item;
        _baseImage = baseImage;
        _pageImage = pageImage;
        _targetWidth = targetWidth;
        _targetHeight = targetHeight;
    }

    /// <summary>Puts the prepared image on the texture page and updates the target size of the item.</summary>
    /// <exception cref="InvalidOperationException">The replacement was already committed, or the page image changed since it was prepared.</exception>
    public void Commit()
    {
        if (_committed)
        {
            throw new InvalidOperationException("The texture replacement was already committed.");
        }
        var data = _item.TexturePage.TextureData;
        lock (data)
        {
            if (!ReferenceEquals(data.Image, _baseImage))
            {
                throw new InvalidOperationException("The texture page changed after this replacement was prepared; prepare it again.");
            }
            data.Image = _pageImage;
        }
        _item.TargetWidth = _targetWidth;
        _item.TargetHeight = _targetHeight;
        _committed = true;
    }
}
