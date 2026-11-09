namespace G3MStudio.Core.Workspace;

public sealed partial class ExplorerModel
{
    public const string QuickCollections = "quick-collections", QuickFolders = "quick-folders", FolderPrefix = "folder:", CollectionPrefix = "collection:", BreadcrumbRoot = "breadcrumb-root", SearchPrefix = "search:";

    private readonly HashSet<string> _flipped = [];

    /// <summary>The user's folders, collections and tag colours; the owner sets it from the saved organisation and saves it again when it changes.</summary>
    public Organizer Organizer
    {
        get;
        set
        {
            field = value;
            Rebuild();
        }
    } = Organizer.Empty;

    /// <summary>The folder view of a new file, from the settings; <see cref="Organizer.FolderView"/> of a file wins over it.</summary>
    public bool DefaultFolderView
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    }

    public bool FolderView => Organizer.FolderView ?? DefaultFolderView;

    /// <summary>Folders start open (the setting); a click on a folder row flips it.</summary>
    public bool FoldersOpen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            _flipped.Clear();
            Rebuild();
        }
    } = true;

    public bool ShowCounts
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    } = true;

    /// <summary>The folder (<see cref="FolderPrefix"/> and id) or collection (<see cref="CollectionPrefix"/> and name) the list is narrowed to, or null for the whole tree.</summary>
    public string? Scope
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    }

    /// <summary>The path to the scope, the root first; each part carries the scope that going there sets (null: the whole tree).</summary>
    public IReadOnlyList<(string Text, string? Scope)> Breadcrumb
    {
        get
        {
            List<(string, string?)> parts = [(Title(BreadcrumbRoot), null)];
            if (Scope?.StartsWith(FolderPrefix, StringComparison.Ordinal) == true) parts.AddRange(Organizer.PathOf(Scope[FolderPrefix.Length..]).Select(f => (f.Name, (string?)(FolderPrefix + f.Id))));
            else if (Scope?.StartsWith(SearchPrefix, StringComparison.Ordinal) == true) parts.Add((Scope[SearchPrefix.Length..], Scope));
            else if (Scope == QuickRecent) parts.Add((Title(QuickRecent), Scope));
            else if (Scope is not null) parts.Add((Scope[CollectionPrefix.Length..], Scope));
            return parts;
        }
    }

    // Rows by tab key; built on first use and dropped when the entries are read again.
    private Dictionary<string, ExplorerRow>? _lookup;

    private Dictionary<string, ExplorerRow> LookupRows() => _lookup ??= _sections.SelectMany(s => s.Items).Where(r => r.IsItem).GroupBy(r => ResourceTypes.KeyOf(r.Key, r.Text)).ToDictionary(g => g.Key, g => g.First());

    private static bool IsOrganizeKey(string key) => key.StartsWith(FolderPrefix, StringComparison.Ordinal) || key.StartsWith(CollectionPrefix, StringComparison.Ordinal);

    private bool IsOpen(string key) => key.StartsWith(FolderPrefix, StringComparison.Ordinal) && FoldersOpen ^ _flipped.Contains(key) || key.StartsWith(CollectionPrefix, StringComparison.Ordinal) && _flipped.Contains(key);

    private bool IsFiled(ExplorerRow row) => Organizer.FolderOf.ContainsKey(ResourceTypes.KeyOf(row.Key, row.Text));

    private IEnumerable<ExplorerRow> NamedRows(IEnumerable<string> keys, int depth) =>
        KeyRows(keys, depth).OrderBy(r => r.Text, StringComparer.CurrentCultureIgnoreCase);

    private List<ExplorerRow> FinishRows(List<ExplorerRow> rows) => ShowCounts ? rows : [.. rows.Select(r => r with { ShowCount = false })];

    private void AddCollections(List<ExplorerRow> rows)
    {
        rows.Add(new ExplorerRow(ExplorerRowKind.Type, QuickCollections, Title(QuickCollections), 2) { Count = Organizer.Collections.Count, Expanded = _expanded.Contains(QuickCollections) });
        if (!_expanded.Contains(QuickCollections)) return;
        foreach (var collection in Organizer.Collections.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var key = CollectionPrefix + collection.Name;
            var open = IsOpen(key);
            rows.Add(new ExplorerRow(ExplorerRowKind.Collection, key, collection.Name, 3) { Count = KeyRows(collection.Keys, 0).Count(), Expanded = open, Colour = collection.Colour });
            if (open) rows.AddRange(NamedRows(collection.Keys, 4));
        }
    }

    // Depth 2 is the Quick Access heading; depth 1 is the main tree of the folder view.
    private void AddFolders(List<ExplorerRow> rows, int depth)
    {
        if (depth == 2)
        {
            rows.Add(new ExplorerRow(ExplorerRowKind.Type, QuickFolders, Title(QuickFolders), 2) { Count = Organizer.Folders.Count, Expanded = _expanded.Contains(QuickFolders) });
            if (!_expanded.Contains(QuickFolders)) return;
            depth = 3;
        }

        AddFolderLevel(rows, null, depth);
    }

    private void AddFolderLevel(List<ExplorerRow> rows, string? parent, int depth)
    {
        foreach (var folder in Organizer.ChildrenOf(parent))
        {
            var key = FolderPrefix + folder.Id;
            var open = IsOpen(key);
            rows.Add(new ExplorerRow(ExplorerRowKind.Folder, key, folder.Name, depth) { Count = KeyRows(Organizer.KeysUnder(folder.Id), 0).Count(), Expanded = open, Colour = folder.Colour });
            if (!open) continue;
            AddFolderLevel(rows, folder.Id, depth + 1);
            rows.AddRange(NamedRows(Organizer.MembersOf(folder.Id), depth + 1));
        }
    }

    /// <summary>The rows of a list narrowed to a folder or collection, or null when it is gone (the scope is then cleared).</summary>
    private List<ExplorerRow>? ScopeRows()
    {
        var rows = new List<ExplorerRow>();
        IEnumerable<string> keys;
        var scope = Scope ?? "";
        if (scope.StartsWith(FolderPrefix, StringComparison.Ordinal))
        {
            var id = scope[FolderPrefix.Length..];
            if (Organizer.Folder(id) is null) return Forget();
            keys = IsFiltering ? Organizer.KeysUnder(id) : Organizer.MembersOf(id);
            if (!IsFiltering) AddFolderLevel(rows, id, 1);
        }
        else if (scope.StartsWith(SearchPrefix, StringComparison.Ordinal))
        {
            var text = scope[SearchPrefix.Length..];
            if (!Quick.Searches.Contains(text)) return Forget();
            keys = SearchKeys(text);
        }
        else if (scope == QuickRecent)
        {
            keys = Quick.Recent;
        }
        else
        {
            if (Organizer.Collection(scope[CollectionPrefix.Length..]) is not { } collection) return Forget();
            keys = collection.Keys;
        }

        // Recent keeps its order, newest first; the others are listed by name.
        var found = (scope == QuickRecent ? KeyRows(keys, 1) : NamedRows(keys, 1)).Where(r => (_typeFilter.Length == 0 || _typeFilter == r.Key) && r.Text.Contains(_needle, StringComparison.OrdinalIgnoreCase)).ToList();
        rows.AddRange(found);
        Matches = found.Count;
        return rows;
    }

    /// <summary>The tab keys of the entries a saved search text finds (a type prefix such as spr: narrows to one type).</summary>
    public IReadOnlyList<string> SearchKeys(string text)
    {
        var (type, needle) = SplitPrefix(text.Trim());
        return [.. LookupRows().Values.Where(r => (type.Length == 0 || type == r.Key) && r.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)).Select(r => ResourceTypes.KeyOf(r.Key, r.Text))];
    }

    /// <summary>The scope a row opens as: a folder, collection, saved search or the Recent list; null for any other row.</summary>
    public static string? ScopeOf(ExplorerRow row) => row.Kind switch
    {
        ExplorerRowKind.Folder or ExplorerRowKind.Collection => row.Key,
        ExplorerRowKind.Search => SearchPrefix + row.Text,
        ExplorerRowKind.Type when row.Key == QuickRecent => QuickRecent,
        _ => null,
    };

    private List<ExplorerRow>? Forget()
    {
        Scope = null;
        return null;
    }

    /// <summary>The tab keys behind a row: its own entry, or every entry of a folder, collection, tag or type. Entries that are gone are left out.</summary>
    public IReadOnlyList<string> KeysOf(ExplorerRow row)
    {
        IEnumerable<string> keys = row.Kind switch
        {
            ExplorerRowKind.Item => [ResourceTypes.KeyOf(row.Key, row.Text)],
            ExplorerRowKind.Folder => Organizer.KeysUnder(row.Key[FolderPrefix.Length..]),
            ExplorerRowKind.Collection => Organizer.Collection(row.Key[CollectionPrefix.Length..])?.Keys ?? [],
            ExplorerRowKind.Search => SearchKeys(row.Text),
            ExplorerRowKind.Type when row.Key == QuickRecent => Quick.Recent,
            ExplorerRowKind.Type when row.Key.StartsWith(TagPrefix, StringComparison.Ordinal) => Quick.KeysOf(row.Key[TagPrefix.Length..]),
            ExplorerRowKind.Type when _sections.Any(s => s.Type.Key == row.Key) => KeysOfType(row.Key),
            _ => [],
        };
        return [.. keys.Where(LookupRows().ContainsKey)];
    }

    /// <summary>Opens a folder with every folder below it, a collection or a tag, so that all its entries have rows.</summary>
    public void OpenAll(ExplorerRow row)
    {
        if (row.Kind == ExplorerRowKind.Folder) foreach (var id in Organizer.Subtree(row.Key[FolderPrefix.Length..])) Open(FolderPrefix + id);
        else Open(row.Key);
        Rebuild();
    }

    private void Open(string key)
    {
        if (!IsOrganizeKey(key)) _expanded.Add(key);
        else if (!IsOpen(key)) Toggle(key);
    }

    /// <summary>The tab keys of every entry of one type, in list order.</summary>
    public IReadOnlyList<string> KeysOfType(string type) =>
        [.. _sections.Where(s => s.Type.Key == type).SelectMany(s => s.Items).Where(r => r.IsItem).Select(r => ResourceTypes.KeyOf(r.Key, r.Text))];

    /// <summary>
    /// Type-ahead: the index of the next row whose text starts with <paramref name="typed"/>, looking after <paramref name="from"/> and wrapping round.
    /// One typed letter moves on to the next row; a longer text may stay on the current one. -1 when none matches.
    /// </summary>
    public static int TypeAhead(IReadOnlyList<ExplorerRow> rows, int from, string typed)
    {
        if (typed.Length == 0 || rows.Count == 0) return -1;
        var start = typed.Length == 1 ? from + 1 : Math.Max(from, 0);
        for (var n = 0; n < rows.Count; n++)
        {
            var i = (start + n) % rows.Count;
            if (rows[i].Kind != ExplorerRowKind.Search && rows[i].Text.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)) return i;
        }

        return -1;
    }
}
