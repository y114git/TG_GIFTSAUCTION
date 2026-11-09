namespace G3MStudio.Core.Workspace;

public enum ExplorerSort { ListOrder, NameAscending, NameDescending }

/// <summary>The Explorer filter popup: sort, where the data group sits, the types that are hidden and one tag that narrows the list.</summary>
public sealed record ExplorerFilter(ExplorerSort Sort = ExplorerSort.ListOrder, bool GroupsFirst = false, IReadOnlyList<string>? HiddenTypes = null, string Tag = "")
{
    public bool IsDefault => this == new ExplorerFilter();

    public bool Hides(string type) => HiddenTypes?.Contains(type) == true;

    public ExplorerFilter Toggle(string type) =>
        this with { HiddenTypes = Hides(type) ? [.. (HiddenTypes ?? []).Where(t => t != type)] : [.. HiddenTypes ?? [], type] };
}

/// <summary>
/// The Quick Access group of the Explorer: recent and favourite entries (tab keys), saved filter texts and tags per entry.
/// It lives in the view state beside the data file, so it follows the file and never enters it. Every change returns a new value.
/// </summary>
public sealed record QuickAccess(
    IReadOnlyList<string> Recent,
    IReadOnlyList<string> Favourites,
    IReadOnlyList<string> Searches,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Tags,
    ExplorerFilter Filter)
{
    public const int MaxRecent = 20;

    public static readonly QuickAccess Empty = new([], [], [], new Dictionary<string, IReadOnlyList<string>>(), new ExplorerFilter());

    /// <summary>The entry was opened: it moves to the front of Recent.</summary>
    public QuickAccess Touch(string key) => this with { Recent = [.. Recent.Where(k => k != key).Prepend(key).Take(MaxRecent)] };

    public bool IsFavourite(string key) => Favourites.Contains(key);

    public QuickAccess ToggleFavourite(string key) => this with { Favourites = IsFavourite(key) ? [.. Favourites.Where(k => k != key)] : [.. Favourites, key] };

    public QuickAccess AddSearch(string text) => text.Trim() is { Length: > 0 } clean && !Searches.Contains(clean) ? this with { Searches = [.. Searches, clean] } : this;

    public QuickAccess RemoveSearch(string text) => this with { Searches = [.. Searches.Where(s => s != text)] };

    /// <summary>The tags, sorted, with the keys they hold.</summary>
    public IReadOnlyList<string> TagNames => [.. Tags.Where(p => p.Value.Count > 0).Select(p => p.Key).Order(StringComparer.CurrentCultureIgnoreCase)];

    public IReadOnlyList<string> KeysOf(string tag) => Tags.GetValueOrDefault(tag) ?? [];

    public IReadOnlyList<string> TagsOf(string key) => [.. TagNames.Where(t => KeysOf(t).Contains(key))];

    public QuickAccess AddTag(string key, string tag)
    {
        tag = tag.Trim();
        if (tag.Length == 0 || KeysOf(tag).Contains(key)) return this;
        return this with { Tags = new Dictionary<string, IReadOnlyList<string>>(Tags) { [tag] = [.. KeysOf(tag), key] } };
    }

    public QuickAccess RemoveTag(string key, string tag) =>
        this with { Tags = Tags.ToDictionary(p => p.Key, p => p.Key == tag ? [.. p.Value.Where(k => k != key)] : p.Value) };

    /// <summary>A resource was renamed or deleted: its key follows the new name, or leaves every list when <paramref name="newKey"/> is null.</summary>
    public QuickAccess Rekey(string oldKey, string? newKey)
    {
        IReadOnlyList<string> Map(IEnumerable<string> keys) => [.. keys.Select(k => k == oldKey ? newKey : k).OfType<string>().Distinct()];
        return this with { Recent = Map(Recent), Favourites = Map(Favourites), Tags = Tags.ToDictionary(p => p.Key, p => Map(p.Value)) };
    }

    /// <summary>Puts a deleted resource back (undo): its tags and its place among the favourites.</summary>
    public QuickAccess Restore(string key, IEnumerable<string> tags, bool favourite)
    {
        var next = tags.Aggregate(this, (q, tag) => q.AddTag(key, tag));
        return favourite && !next.IsFavourite(key) ? next.ToggleFavourite(key) : next;
    }
}
