
namespace G3MStudio.Core.Workspace;

public sealed partial class ExplorerModel
{
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
    /// <summary>Reads the entries again, keeping the rows of a type whose entries and names did not change. True when anything differs.</summary>
    private bool ReadSections()
    {
        var old = _sections.ToDictionary(s => s.Type.Key);
        _lookup = null;
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
                    sections.Add(new Section(type, [.. source.Select((i, n) => i is null ? new ExplorerRow(ExplorerRowKind.Null, type.Key, $"[{n}] {Title("null")}", depth) : Item(i))]));
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
            if (item is null ? row.Kind != ExplorerRowKind.Null || row.Text != $"[{at - 1}] {Title("null")}" : row.Kind != ExplorerRowKind.Item || !ReferenceEquals(row.Item, item) || row.Text != ResourceTypes.NameOf(item)) return false;
        }

        return at == rows.Length;
    }
    private void Rebuild()
    {
        var rows = new List<ExplorerRow>(Total + 40);
        Matches = 0;
        if (Scope is not null && ScopeRows() is { } scoped)
        {
            Rows = FinishRows(scoped);
            Changed?.Invoke();
            return;
        }

        if (_data is not null && !IsFiltering) AddQuick(rows);
        var typeRows = new List<ExplorerRow>();
        if (FolderView && _data is not null && !IsFiltering && Options.Tag.Length == 0) AddFolders(typeRows, 1);
        AddSections(typeRows, _sections.Where(s => !s.Type.IsData), 1);
        var dataRows = new List<ExplorerRow>();
        AddSections(dataRows, _sections.Where(s => s.Type.IsData), 2);
        if (dataRows.Count > 0)
        {
            var open = IsFiltering || _expanded.Contains(DataGroup);
            var heading = new ExplorerRow(ExplorerRowKind.Group, DataGroup, Title(DataGroup), 1) { Expanded = open, Count = dataRows.Where(r => r.Kind == ExplorerRowKind.Type).Sum(r => r.Count) };
            dataRows = open ? [heading, .. dataRows] : [heading];
        }

        // Groups first lists the data group above the resource types.
        rows.AddRange(Options.GroupsFirst ? dataRows.Concat(typeRows) : typeRows.Concat(dataRows));

        Rows = FinishRows(rows);
        Changed?.Invoke();
    }

    private void AddSections(List<ExplorerRow> rows, IEnumerable<Section> sections, int depth)
    {
        foreach (var section in sections)
        {
            if (Options.Hides(section.Type.Key)) continue;
            var unfiltered = !IsFiltering && Options.Tag.Length == 0;
            // In the folder view the types list what no folder holds.
            var restrict = !unfiltered || FolderView && Organizer.FolderOf.Count > 0;
            var items = !restrict ? section.Items
                : (_typeFilter.Length > 0 && _typeFilter != section.Type.Key) ? []
                : [.. section.Items.Where(r => r.IsItem && r.Text.Contains(_needle, StringComparison.OrdinalIgnoreCase) && TagOk(r) && (!unfiltered || !IsFiled(r)))];
            if (Options.Sort != ExplorerSort.ListOrder) items = Sorted(items);
            if (restrict && items.Length == 0) continue;
            var open = !unfiltered || _expanded.Contains(section.Type.Key);
            var count = !restrict ? section.ItemCount : items.Count(r => r.IsItem);
            rows.Add(new ExplorerRow(ExplorerRowKind.Type, section.Type.Key, Title(section.Type.Key), depth) { Count = count, Expanded = open });
            Matches += count;
            if (open) rows.AddRange(items);
        }
    }
}
