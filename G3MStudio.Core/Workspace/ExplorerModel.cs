using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public enum ExplorerRowKind { Group, Type, Item, Null, Search, Folder, Collection }

/// <summary>One visible row of the flattened tree. <see cref="Key"/> is the type key for Group and Type rows.</summary>
public sealed record ExplorerRow(ExplorerRowKind Kind, string Key, string Text, int Depth)
{
    public int Count { get; init; }

    public bool Expanded { get; init; }

    /// <summary>#RRGGBB of a folder, collection or tag row, or empty.</summary>
    public string Colour { get; init; } = "";

    /// <summary>False when the settings hide the counts.</summary>
    public bool ShowCount { get; init; } = true;

    public bool HasCount => (IsHeader || Kind == ExplorerRowKind.Search) && ShowCount;

    public object? Item { get; init; }

    public bool IsItem => Kind == ExplorerRowKind.Item;

    /// <summary>A group or type heading, the rows that open and close.</summary>
    public bool IsHeader => Kind is ExplorerRowKind.Group or ExplorerRowKind.Type or ExplorerRowKind.Folder or ExplorerRowKind.Collection;

    /// <summary>What a screen reader announces for the row: the text, and the count when the row shows one.</summary>
    public string AutomationName => HasCount ? $"{Text}, {Count}" : Text;

    /// <summary>An empty heading (not a group) has nothing to open, so it shows no chevron at all instead of a state that cannot change anything.</summary>
    public string Chevron => !IsHeader || Count == 0 && Kind != ExplorerRowKind.Group ? "" : Expanded ? "▼" : "►";
}

/// <summary>
/// The Explorer tree as a flat list of rows for a virtualized list (design/shell.md section 5). Types with entries are listed in
/// two groups, resources and data; a filter keeps matching entries only and opens every type that has one.
/// </summary>
public sealed partial class ExplorerModel(Func<string, string> title)
{
    public const string DataGroup = "data";

    private record Section(ResourceType Type, ExplorerRow[] Items)
    {
        /// <summary>Entries without the null rows; counted once, because a list of 100,000 rows is not walked on every redraw.</summary>
        public int ItemCount { get; } = Items.Count(r => r.IsItem);
    }

    public const string QuickGroup = "quick", QuickRecent = "quick-recent", QuickFavourites = "quick-favourites", QuickSearches = "quick-searches", QuickTags = "quick-tags", TagPrefix = "tag:";

    private readonly HashSet<string> _expanded = [.. ResourceTypes.All.Where(t => !t.IsData).Select(t => t.Key), DataGroup, QuickGroup, QuickFolders, QuickCollections];
    private List<Section> _sections = [];
    private GameMakerData? _data;
    private string _typeFilter = "", _needle = "";

    private string Title(string key) => title(key);

    public IReadOnlyList<ExplorerRow> Rows { get; private set; } = [];


    public string Filter { get; private set; } = "";

    public bool IsFiltering => Filter.Length > 0;

    /// <summary>All entries of the loaded data file.</summary>
    public int Total => _sections.Sum(s => s.ItemCount);

    /// <summary>Lists the null entries of a list (GameMaker 2024.11 and later) as rows that cannot be opened (UTMT-TREE-11).</summary>
    public bool ShowNulls
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Reload();
        }
    }

    /// <summary>Entries shown now (equals <see cref="Total"/> without a filter, even when types are collapsed).</summary>
    public int Matches { get; private set; }

    public event Action? Changed;

    public void Load(GameMakerData? data)
    {
        _data = data;
        Reload();
    }

    /// <summary>Reads the names again after an edit or a language change.</summary>
    public void Reload()
    {
        ReadSections();
        Rebuild();
    }

    /// <summary>Like <see cref="Reload"/>, but raises <see cref="Changed"/> only when an entry was added, removed or renamed; an edit of a property changes none.</summary>
    public void Refresh()
    {
        if (ReadSections()) Rebuild();
    }

    public void SetFilter(string text)
    {
        Filter = text.Trim();
        (_typeFilter, _needle) = SplitPrefix(Filter);
        Rebuild();
    }

    public void Toggle(string key)
    {
        if (IsOrganizeKey(key)) { if (!_flipped.Remove(key)) _flipped.Add(key); }
        else if (!_expanded.Remove(key)) _expanded.Add(key);
        Rebuild();
    }
}
