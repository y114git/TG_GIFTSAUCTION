using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public enum ExplorerRowKind { Group, Type, Item, Null }

/// <summary>One visible row of the flattened tree. <see cref="Key"/> is the type key for Group and Type rows.</summary>
public sealed record ExplorerRow(ExplorerRowKind Kind, string Key, string Text, int Depth)
{
    public int Count { get; init; }

    public bool Expanded { get; init; }

    public object? Item { get; init; }

    public bool IsItem => Kind == ExplorerRowKind.Item;

    /// <summary>A group or type heading, the rows that open and close.</summary>
    public bool IsHeader => Kind is ExplorerRowKind.Group or ExplorerRowKind.Type;

    public string Chevron => !IsHeader ? "" : Expanded ? "▼" : "►";
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

    private readonly HashSet<string> _expanded = [.. ResourceTypes.All.Where(t => !t.IsData).Select(t => t.Key), DataGroup];
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
        if (!_expanded.Remove(key)) _expanded.Add(key);
        Rebuild();
    }
}
