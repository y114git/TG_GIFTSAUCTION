using System.Security.Cryptography;
using System.Text;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using G3MStudio.Core.Workspace;
using ImageMagick;

namespace G3MStudio.Core.ModPack;

/// <summary>
/// A content hash of one resource: its property rows, plus what the rows do not show (the pixels of a sprite or tile set, the instances of a room, the
/// assembly of a code entry). Pictures are hashed by pixels, not by their place on the texture page, so a repacked page changes nothing.
/// </summary>
public sealed class Fingerprinter(GameMakerData data) : IDisposable
{
    private TextureWorker? _worker;

    public static string HashOf(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The hash, or null when the data file has no such resource.</summary>
    public string? Of(string type, string name)
    {
        if (type == "code") return data.Code.ByName(name) is null ? null : HashOf(CodeText.Disassemble(data, name));
        if (PropertyModel.FindRoot(data, type, name) is not { } root) return null;
        var text = new StringBuilder();
        // A copy does not carry the link from a sprite to its sequence (the sequence is a resource of its own), so the link is not part of the hash.
        // The rows of a picture name its place on the texture page; the pixels below stand for it.
        var skip = type is "sprites" or "tileSets" ? "Texture" : "\0";
        foreach (var row in PropertyModel.Describe(root).Where(r => !r.Path.StartsWith(skip, StringComparison.Ordinal) && r.Path != "V2Sequence"))
            text.Append(row.Path).Append('=').Append(row.Value).Append('\n');
        if (root is GameMakerRoom room) AppendInstances(text, room);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(text.ToString()));
        foreach (var item in Textures(root)) hash.AppendData(Pixels(item, name));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static IEnumerable<GameMakerTexturePageItem> Textures(object root) => root switch
    {
        GameMakerSprite sprite => sprite.Textures.Select(t => t.Texture).OfType<GameMakerTexturePageItem>(),
        GameMakerBackground background => new[] { background.Texture }.OfType<GameMakerTexturePageItem>(),
        _ => [],
    };

    private byte[] Pixels(GameMakerTexturePageItem item, string name)
    {
        _worker ??= new TextureWorker();
        try
        {
            using var image = _worker.GetTextureFor(item, name);
            using var pixels = image.GetPixels();
            return pixels.ToByteArray(PixelMapping.RGBA) ?? [];
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or ImageMagick.MagickException)
        {
            // One unreadable picture must not stop the whole comparison; it counts as its own marker.
            return System.Text.Encoding.UTF8.GetBytes("unreadable:" + name);
        }
    }

    private static void AppendInstances(StringBuilder text, GameMakerRoom room)
    {
        foreach (var i in InstancesOf(room).OrderBy(i => i.InstanceID))
            text.Append(System.FormattableString.Invariant($"#{i.InstanceID}:{i.ObjectDefinition?.Name?.Content}:{i.X}:{i.Y}:{i.ScaleX}:{i.ScaleY}:{i.Rotation}\n"));
    }

    /// <summary>Every instance of the room once; a layer lists the same objects as the room's own list.</summary>
    public static IEnumerable<GameMakerRoom.GameObject> InstancesOf(GameMakerRoom room) =>
        RoomInstanceCommand.Lists(room).SelectMany(l => l).DistinctBy(i => i.InstanceID);

    public void Dispose() => _worker?.Dispose();
}
