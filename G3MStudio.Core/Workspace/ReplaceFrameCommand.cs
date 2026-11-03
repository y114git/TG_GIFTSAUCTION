using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>Replaces one frame of a sprite with a picture (any format Magick reads). The page image it overwrote is kept, so Undo puts it back. The picture is stored in the journal.</summary>
public sealed class ReplaceFrameCommand(string sprite, int frame, byte[] picture) : EditCommand
{
    public const string Name = "replaceFrame";

    private GMImage? _before;
    private GMImage? _after;
    private (ushort Width, ushort Height) _size;

    private const uint MaxSide = 8192;

    public override string Kind => Name;

    public override string Editor => "sprites";

    /// <summary>
    /// Reads a picture of a plain raster format only. The file signature is checked here, before any image library call, and the reader is
    /// then pinned to that format, so a file that claims to be a vector, script or document type is refused. Journal replay reads through this too.
    /// </summary>
    public static MagickImage ReadPicture(byte[] bytes)
    {
        var format = SignatureOf(bytes) ?? throw new InvalidDataException("The picture format is not supported. Use PNG, BMP, GIF, JPEG or WebP.");
        var settings = new MagickReadSettings { Format = format };
        var info = new MagickImageInfo(bytes, settings);
        if (info.Width > MaxSide || info.Height > MaxSide) throw new InvalidDataException($"The picture is larger than {MaxSide} pixels on a side.");
        return new MagickImage(bytes, settings);
    }

    private static MagickFormat? SignatureOf(byte[] b) => b switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => MagickFormat.Png,
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => MagickFormat.Gif,
        [0xFF, 0xD8, 0xFF, ..] => MagickFormat.Jpeg,
        [(byte)'B', (byte)'M', ..] => MagickFormat.Bmp,
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => MagickFormat.WebP,
        _ => null,
    };

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["picture"] = Convert.ToBase64String(picture),
    };

    private GameMakerTexturePageItem Item(GameMakerData data)
    {
        var found = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        return found.Textures.ElementAtOrDefault(frame)?.Texture ?? throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
    }

    public override void Apply(GameMakerData data)
    {
        var item = Item(data);
        var data2 = item.TexturePage.TextureData;
        _before ??= data2.Image;
        _size = (item.TargetWidth, item.TargetHeight);
        if (_after is null)
        {
            using var image = ReadPicture(picture);
            item.ReplaceTexture(image);
            _after = data2.Image;
        }
        else
        {
            data2.Image = _after;
        }
    }

    public override void Revert(GameMakerData data)
    {
        var item = Item(data);
        item.TexturePage.TextureData.Image = _before ?? throw new InvalidOperationException("The frame was never replaced.");
        (item.TargetWidth, item.TargetHeight) = _size;
    }
}
