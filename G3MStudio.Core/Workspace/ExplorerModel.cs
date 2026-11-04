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
public sealed class ExplorerModel(Func<string, string> title)
{
    public const string DataGroup = "data";

    private static readonly Dictionary<string, string> Prefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spr"] = "sprites",
        ["bg"] = "tileSets",
        ["snd"] = "sounds",
        ["path"] = "paths",
        ["scr"] = "scripts",
        ["shd"] = "shaders",
        ["fnt"] = "fonts",
        ["tl"] = "timelines",
        ["obj"] = "objects",
        ["room"] = "rooms",
        ["ext"] = "extensions",
        ["seq"] = "sequences",
        ["curve"] = "animationCurves",
        ["ps"] = "particleSystems",
        ["code"] = "code",
        ["str"] = "strings",
        ["game"] = "general",
    };

    /// <summary>Splits <c>spr:player</c> into the type key and the text; no known prefix gives an empty type key.</summary>
    internal static (string Type, string Needle) SplitPrefix(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && Prefixes.TryGetValue(text[..colon], out var type) ? (type, text[(colon + 1)..].Trim()) : ("", text);
    }

    private record Section(ResourceType Type, ExplorerRow[] Items)
    {
        /// <summary>Entries without the null rows; counted once, because a list of 100,000 rows is not walked on every redraw.</summary>
        public int ItemCount { get; } = Items.Count(r => r.IsItem);
    }

    private readonly HashSet<string> _expanded = [.. ResourceTypes.All.Where(t => !t.IsData).Select(t => t.Key), DataGroup];
    private List<Section> _sections = [];
    private GameMakerData? _data;
    private string _typeFilter = "", _needle = "";

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

    /// <summary>Reads the entries again, keeping the rows of a type whose entries and names did not change. True when anything differs.</summary>
    private bool ReadSections()
    {
        var old = _sections.ToDictionary(s => s.Type.Key);
        var changed = false;
        List<Section> sections = [];
        if (_data is not null)
            foreach (var type in ResourceTypes.All)
                if (type.Items(_data) is { } items)
                {
                    var depth = type.IsData ? 3 : 2;
                    // Raw keeps the list positions, so a null row sits where the entry would be.
                    var source = ShowNulls && type.Raw?.Invoke(_data) is { } raw ? raw : items;
                    // Most edits change no name: compare with the rows already made before making 100,000 new ones.
                    if (old.TryGetValue(type.Key, out var before) && SameRows(before.Items, source))
                    {
                        sections.Add(before);
                        continue;
                    }

                    ExplorerRow Item(object i) => new(ExplorerRowKind.Item, type.Key, ResourceTypes.NameOf(i), depth) { Item = i };
                    changed = true;
                    sections.Add(new Section(type, [.. source.Select((i, n) => i is null ? new ExplorerRow(ExplorerRowKind.Null, type.Key, $"[{n}] {title("null")}", depth) : Item(i))]));
                }

        changed |= sections.Count != old.Count;
        _sections = sections;
        return changed;
    }

    private bool SameRows(ExplorerRow[] rows, IEnumerable<object?> source)
    {
        var at = 0;
        foreach (var item in source)
        {
            if (at == rows.Length) return false;
            var row = rows[at++];
            if (item is null ? row.Kind != ExplorerRowKind.Null || row.Text != $"[{at - 1}] {title("null")}" : row.Kind != ExplorerRowKind.Item || !ReferenceEquals(row.Item, item) || row.Text != ResourceTypes.NameOf(item)) return false;
        }

        return at == rows.Length;
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

    private void Rebuild()
    {
        var rows = new List<ExplorerRow>(Total + 40);
        Matches = 0;
        AddSections(rows, _sections.Where(s => !s.Type.IsData), 1);
        var dataRows = new List<ExplorerRow>();
        AddSections(dataRows, _sections.Where(s => s.Type.IsData), 2);
        if (dataRows.Count > 0)
        {
            var open = IsFiltering || _expanded.Contains(DataGroup);
            rows.Add(new ExplorerRow(ExplorerRowKind.Group, DataGroup, title(DataGroup), 1) { Expanded = open });
            if (open) rows.AddRange(dataRows);
        }

        Rows = rows;
        Changed?.Invoke();
    }

    private void AddSections(List<ExplorerRow> rows, IEnumerable<Section> sections, int depth)
    {
        foreach (var section in sections)
        {
            var unfiltered = !IsFiltering;
            var items = unfiltered ? section.Items
                : (_typeFilter.Length > 0 && _typeFilter != section.Type.Key) ? []
                : [.. section.Items.Where(r => r.IsItem && r.Text.Contains(_needle, StringComparison.OrdinalIgnoreCase))];
            if (IsFiltering && items.Length == 0) continue;
            var open = IsFiltering || _expanded.Contains(section.Type.Key);
            var count = unfiltered ? section.ItemCount : items.Count(r => r.IsItem);
            rows.Add(new ExplorerRow(ExplorerRowKind.Type, section.Type.Key, title(section.Type.Key), depth) { Count = count, Expanded = open });
            Matches += count;
            if (open) rows.AddRange(items);
        }
    }
}
