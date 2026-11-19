
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

    /// <summary>The filter prefix that narrows the list to one type (<c>spr</c> for sprites), or null when the type has none.</summary>
    public static string? PrefixOf(string typeKey) => Prefixes.FirstOrDefault(p => p.Value == typeKey).Key;

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

                    ExplorerRow Item(object i)
                    {
                        var name = ResourceTypes.NameOf(i);
                        return new(ExplorerRowKind.Item, type.Key, name, depth) { Item = i, Nickname = NicknameFor(type.Key, name) };
                    }

                    changed = true;
                    sections.Add(new Section(type, [.. source.Select((i, n) => i is null ? new ExplorerRow(ExplorerRowKind.Null, type.Key, $"[{n}] {Title("null")}", depth) : Item(i))]));
                }

        changed |= sections.Count != old.Count;
        _sections = sections;
        return changed;
    }

    // A code entry shows the nickname of its owner inside its name; any other entry shows its own nickname.
    private string NicknameFor(string type, string name)
    {
        if (type != "code") return Nicknames.Of(type, name) ?? "";
        var label = Nicknames.CodeLabel(name);
        return label == name ? "" : label;
    }

    private bool SameRows(ExplorerRow[] rows, IEnumerable<object?> source)
    {
        var at = 0;
        foreach (var item in source)
        {
            if (at == rows.Length) return false;
            var row = rows[at++];
            if (item is null ? row.Kind != ExplorerRowKind.Null || row.Text != $"[{at - 1}] {Title("null")}" : row.Kind != ExplorerRowKind.Item || !ReferenceEquals(row.Item, item) || row.Text != ResourceTypes.NameOf(item) || row.Nickname != NicknameFor(row.Key, row.Text)) return false;
        }

        return at == rows.Length;
    }
    private void Rebuild()
    {
        if (!Narrowed) _foldedNarrow.Clear();
        var rows = new List<ExplorerRow>(Total + 40);
        (Matches, Hidden) = (0, 0);
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
            var open = !Narrowed ? _expanded.Contains(DataGroup) : !_foldedNarrow.Contains(DataGroup);
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
            var unfiltered = !Narrowed;
            // In the folder view the types list what no folder holds.
            var restrict = !unfiltered || FolderView && Organizer.FolderOf.Count > 0 || HideItem is not null;
            if (!restrict && section.ItemCount == 0 && Options.HideEmpty) continue;
            var items = !restrict ? section.Items
                : (_typeFilter.Length > 0 && _typeFilter != section.Type.Key) ? []
                : [.. section.Items.Where(r => r.IsItem && r.Matches(_needle) && TagOk(r) && (!unfiltered || !IsFiled(r)) && !IsHidden(r))];
            if (Options.Sort != ExplorerSort.ListOrder) items = Sorted(items);
            if (restrict && items.Length == 0) continue;
            var open = unfiltered ? _expanded.Contains(section.Type.Key) : !_foldedNarrow.Contains(section.Type.Key);
            var count = !restrict ? section.ItemCount : items.Count(r => r.IsItem);
            rows.Add(new ExplorerRow(ExplorerRowKind.Type, section.Type.Key, Title(section.Type.Key), depth) { Count = count, Expanded = open });
            Matches += count;
            if (open) rows.AddRange(items);
        }
    }

    /// <summary>True for the key of a resource or data type of the loaded file, the headings that hold entries.</summary>
    public bool IsType(string key) => _sections.Any(s => s.Type.Key == key);

    /// <summary>Opens or folds every type heading and the data group at once.</summary>
    public void FoldAll(bool open)
    {
        foreach (var key in _sections.Select(s => s.Type.Key).Append(DataGroup))
        {
            if (!Narrowed) _touched.Add(key);
            _ = Narrowed ? (open ? _foldedNarrow.Remove(key) : _foldedNarrow.Add(key)) : open ? _expanded.Add(key) : _expanded.Remove(key);
        }

        Rebuild();
    }

    public void Toggle(string key)
    {
        if (IsOrganizeKey(key)) { if (!_flipped.Remove(key)) _flipped.Add(key); }
        else if (Narrowed && (key == DataGroup || IsType(key))) { if (!_foldedNarrow.Remove(key)) _foldedNarrow.Add(key); }
        else
        {
            _touched.Add(key);
            if (!_expanded.Remove(key)) _expanded.Add(key);
        }

        Rebuild();
    }
}
