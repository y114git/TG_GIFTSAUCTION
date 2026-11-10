using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Replaces the picture of the one texture page item of a font, tile set or background (OI-274), the way <see cref="ReplaceFrameCommand"/> does for a sprite frame:
/// the picture is fitted into the item's rectangle on its page, nothing else is packed again, and Undo puts the old page image back.
/// </summary>
public sealed class ReplaceItemCommand(string type, string name, byte[] picture) : ReplaceFrameCommand("", 0, picture)
{
    public new const string Name = "replaceItem";

    /// <summary>Resource types that keep their picture as a texture page item, so a new image can be put in its place.</summary>
    public static readonly string[] Types = ["fonts", "tileSets"];

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["picture"] = Convert.ToBase64String(picture) };

    public static ReplaceItemCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["name"], Convert.FromBase64String(a["picture"]));

    protected override GameMakerTexturePageItem Item(GameMakerData data) =>
        (Types.Contains(type) ? ResourceTypes.Find(data, ResourceTypes.KeyOf(type, name)) : null) switch
        {
            GameMakerFont { Texture: { } texture } => texture,
            GameMakerBackground { Texture: { } texture } => texture,
            null => throw new InvalidOperationException($"'{name}' was not found in {type}."),
            _ => throw new InvalidOperationException($"'{name}' has no texture picture."),
        };
}
