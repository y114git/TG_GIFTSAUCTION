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

    /// <summary>False when the settings hide the counts. A count of 0 is never shown either.</summary>
    public bool ShowCount { get; init; } = true;

    public bool HasCount => (IsHeader || Kind == ExplorerRowKind.Search) && ShowCount && Count > 0;

    public object? Item { get; init; }

    /// <summary>The key of the folder, collection or list the row is shown under, so the same entry listed in two places gives two different rows.</summary>
    public string Holder { get; init; } = "";

    /// <summary>The nickname of an entry, or empty. <see cref="Text"/> stays the real name and the identity of the row.</summary>
    public string Nickname { get; init; } = "";

    /// <summary>What the row shows: the nickname, else the name.</summary>
    public string Label => Nickname.Length > 0 ? Nickname : Text;

    /// <summary>The tooltip: the nickname with the real name behind it, or the name.</summary>
    public string Tip => Nickname.Length > 0 ? $"{Nickname} ({Text})" : Text;

    /// <summary>True when the name or the nickname contains the text.</summary>
    public bool Matches(string needle) => Text.Contains(needle, StringComparison.OrdinalIgnoreCase) || Nickname.Contains(needle, StringComparison.OrdinalIgnoreCase);

    public bool IsItem => Kind == ExplorerRowKind.Item;

    /// <summary>Levels of indent: every row sits one level further in than the heading it is listed under, so an entry never lines up with the text of its parent.</summary>
    public int IndentDepth => Depth;

    /// <summary>A group or type heading, the rows that open and close.</summary>
    public bool IsHeader => Kind is ExplorerRowKind.Group or ExplorerRowKind.Type or ExplorerRowKind.Folder or ExplorerRowKind.Collection;

    /// <summary>What a screen reader announces for the row: the text, and the count when the row shows one.</summary>
    public string AutomationName => HasCount ? $"{Label}, {Count}" : Label;

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
    // While a filter or tag narrows the list every type is open; a click on a heading folds it for as long as the list stays narrowed.
    private readonly HashSet<string> _foldedNarrow = [];
    private List<Section> _sections = [];
    private GameMakerData? _data;
    private string _typeFilter = "", _needle = "";

    private string Title(string key) => title(key);

    public IReadOnlyList<ExplorerRow> Rows { get; private set; } = [];

    /// <summary>The nicknames of the loaded file: rows show them, and the filter matches them. Set it from the workspace, and call <see cref="Reload"/> after a change.</summary>
    public Nicknames Nicknames { get; set; } = new();


    public string Filter { get; private set; } = "";

    public bool IsFiltering => Filter.Length > 0;

    /// <summary>True while a filter text or a tag narrows the list; folding then works on its own set, so it does not disturb the folds of the full tree.</summary>
    private bool Narrowed => IsFiltering || Options.Tag.Length > 0;

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

    /// <summary>A type with more entries than this starts folded when a file is opened: 100,000 rows take seconds to lay out. A filter opens it, and so does a click.</summary>
    public const int FoldedAbove = 5000;

    // Types the user opened or folded by hand keep their state across files.
    private readonly HashSet<string> _touched = [];

    public void Load(GameMakerData? data)
    {
        _data = data;
        ReadSections();
        foreach (var section in _sections.Where(s => !s.Type.IsData && !_touched.Contains(s.Type.Key)))
            _ = section.ItemCount > FoldedAbove ? _expanded.Remove(section.Type.Key) : _expanded.Add(section.Type.Key);
        Rebuild();
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
}
