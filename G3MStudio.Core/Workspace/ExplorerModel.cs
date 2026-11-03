using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public enum ExplorerRowKind { Group, Type, Item }

/// <summary>One visible row of the flattened tree. <see cref="Key"/> is the type key for Group and Type rows.</summary>
public sealed record ExplorerRow(ExplorerRowKind Kind, string Key, string Text, int Depth)
{
    public int Count { get; init; }

    public bool Expanded { get; init; }

    public object? Item { get; init; }

    public bool IsItem => Kind == ExplorerRowKind.Item;

    public string Chevron => IsItem ? "" : Expanded ? "▼" : "►";
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

    private record Section(ResourceType Type, ExplorerRow[] Items);

    private readonly HashSet<string> _expanded = [.. ResourceTypes.All.Where(t => !t.IsData).Select(t => t.Key), DataGroup];
    private List<Section> _sections = [];
    private GameMakerData? _data;
    private string _typeFilter = "", _needle = "";

    public IReadOnlyList<ExplorerRow> Rows { get; private set; } = [];

    public string Filter { get; private set; } = "";

    public bool IsFiltering => Filter.Length > 0;

    /// <summary>All entries of the loaded data file.</summary>
    public int Total => _sections.Sum(s => s.Items.Length);

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
        _sections = [];
        if (_data is not null)
            foreach (var type in ResourceTypes.All)
                if (type.Items(_data) is { } items)
                    _sections.Add(new Section(type, [.. items.Select(i => new ExplorerRow(ExplorerRowKind.Item, type.Key, ResourceTypes.NameOf(i), type.IsData ? 3 : 2) { Item = i })]));
        Rebuild();
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
            var items = !IsFiltering ? section.Items
                : (_typeFilter.Length > 0 && _typeFilter != section.Type.Key) ? []
                : [.. section.Items.Where(r => r.Text.Contains(_needle, StringComparison.OrdinalIgnoreCase))];
            if (IsFiltering && items.Length == 0) continue;
            var open = IsFiltering || _expanded.Contains(section.Type.Key);
            rows.Add(new ExplorerRow(ExplorerRowKind.Type, section.Type.Key, title(section.Type.Key), depth) { Count = items.Length, Expanded = open });
            Matches += items.Length;
            if (open) rows.AddRange(items);
        }
    }
}
