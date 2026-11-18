using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>What a hover card says about a resource. <see cref="Nickname"/> is set only when the interface shows another name than the real one; <see cref="Id"/> is the number of the entry in its list (the asset index GML uses for resources).</summary>
public sealed record ResourceHoverInfo(string Key, string Type, string Name, string? Nickname, int? Id)
{
    public string Title => Nickname ?? Name;

    public bool HasPicture => ResourceThumbnails.CanHavePicture(Type);
}

/// <summary>The content of the one hover card every surface shows for a resource (design: item 40). Pure lookup, no drawing.</summary>
public static class ResourceHover
{
    /// <summary>The card content for an Explorer key, or null when the key names nothing in the file (the caller keeps its own text). The ID is the one the status bar shows.</summary>
    public static ResourceHoverInfo? Describe(GameMakerData data, Nicknames nicknames, string key) =>
        ResourceTypes.IdOf(data, key) is { } id && ResourceTypes.SplitKey(key) is var (type, name) ? Make(type, name, id, nicknames) : null;

    private static ResourceHoverInfo Make(string type, string name, int? id, Nicknames nicknames)
    {
        var shown = nicknames.OwnerLabel(type, name);
        return new(ResourceTypes.KeyOf(type, name), type, name, shown == name ? null : shown, id);
    }
}
