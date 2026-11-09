namespace G3MStudio.Core.Workspace;

public sealed partial class ExplorerModel
{
    /// <summary>Recent, favourites, saved searches and tags; the owner sets it from the view state and saves it again on <see cref="Changed"/>.</summary>
    public QuickAccess Quick
    {
        get;
        set
        {
            field = value;
            Rebuild();
        }
    } = QuickAccess.Empty;

    public ExplorerFilter Options => Quick.Filter;

    private bool TagOk(ExplorerRow row) => Options.Tag.Length == 0 || Quick.KeysOf(Options.Tag).Contains(ResourceTypes.KeyOf(row.Key, row.Text));

    private ExplorerRow[] Sorted(ExplorerRow[] items)
    {
        var sorted = items.OrderBy(r => r.Text, StringComparer.CurrentCultureIgnoreCase);
        return [.. Options.Sort == ExplorerSort.NameAscending ? sorted : sorted.Reverse()];
    }

    private void AddQuick(List<ExplorerRow> rows)
    {
        var open = _expanded.Contains(QuickGroup);
        rows.Add(new ExplorerRow(ExplorerRowKind.Group, QuickGroup, Title(QuickGroup), 1) { Expanded = open });
        if (!open) return;
        void Heading(string key, int count) => rows.Add(new ExplorerRow(ExplorerRowKind.Type, key, Title(key), 2) { Count = count, Expanded = _expanded.Contains(key) });
        Heading(QuickRecent, Quick.Recent.Count);
        if (_expanded.Contains(QuickRecent)) rows.AddRange(KeyRows(Quick.Recent, 3));
        Heading(QuickFavourites, Quick.Favourites.Count);
        if (_expanded.Contains(QuickFavourites)) rows.AddRange(KeyRows(Quick.Favourites, 3));
        Heading(QuickSearches, Quick.Searches.Count);
        if (_expanded.Contains(QuickSearches)) rows.AddRange(Quick.Searches.Select(q => new ExplorerRow(ExplorerRowKind.Search, "search", q, 3) { Count = SearchKeys(q).Count }));
        Heading(QuickTags, Quick.TagNames.Count);
        if (_expanded.Contains(QuickTags))
        {
            foreach (var tag in Quick.TagNames)
            {
                var key = TagPrefix + tag;
                rows.Add(new ExplorerRow(ExplorerRowKind.Type, key, tag, 3) { Count = KeyRows(Quick.KeysOf(tag), 0).Count(), Expanded = _expanded.Contains(key), Colour = Organizer.TagColours.GetValueOrDefault(tag, "") });
                if (_expanded.Contains(key)) rows.AddRange(KeyRows(Quick.KeysOf(tag), 4));
            }
        }

        AddCollections(rows);
        if (!FolderView) AddFolders(rows, 2);
    }

    // Entries that are gone from the data file are skipped; the key stays in the list in case an undo brings the entry back.
    private IEnumerable<ExplorerRow> KeyRows(IEnumerable<string> keys, int depth) =>
        keys.Select(k => LookupRows().GetValueOrDefault(k)).OfType<ExplorerRow>().Select(r => r with { Depth = depth });
}
