using System.Text.Json;
using System.Text.Json.Serialization;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The auto-tile rule sets of one data file, kept in Studio's own data folder (<c>autotile/&lt;file key&gt;.json</c>) because the data file stores no
/// auto-tile rules. Every change is written at once through a temporary file and a rename. A rule set is found by its tile set and name.
/// </summary>
public sealed class AutoTileStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Largest import file (4 MB) and most slots one rule set may hold; the biggest layout has 256.</summary>
    public const long MaxImportBytes = 4 * 1024 * 1024;

    public const int MaxSlots = 1024;

    private sealed record Content(List<AutoTileRuleSet> RuleSets);

    private readonly string _file;
    private List<AutoTileRuleSet> _sets = [];

    public AutoTileStore(UserData userData, string dataFile)
    {
        _file = userData.Combine("autotile", UserData.KeyFor(dataFile) + ".json");
        if (!File.Exists(_file)) return;
        try
        {
            if (JsonSerializer.Deserialize<Content>(File.ReadAllText(_file), Json) is { RuleSets: { } sets }) _sets = [.. sets.Select(Clean).OfType<AutoTileRuleSet>().DistinctBy(s => (s.TileSet, s.Name))];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _sets = []; // a damaged sidecar starts empty; the next change overwrites it
        }
    }

    /// <summary>Raised after any change.</summary>
    public event Action? Changed;

    /// <summary>Raised when a change could not be written; the change is not applied.</summary>
    public event Action<Exception>? Failed;

    public IReadOnlyList<AutoTileRuleSet> All => _sets;

    public IReadOnlyList<AutoTileRuleSet> For(string tileSet) => [.. _sets.Where(s => s.TileSet == tileSet)];

    /// <summary>The <paramref name="index"/>th rule set of a tile set, or null.</summary>
    public AutoTileRuleSet? At(string tileSet, int index) => For(tileSet) is var list && index >= 0 && index < list.Count ? list[index] : null;

    /// <summary>Adds the rule set, or replaces the one of the same tile set and name (or of <paramref name="oldName"/> when it is renamed).</summary>
    public void Save(AutoTileRuleSet rules, string? oldName = null)
    {
        var index = _sets.FindIndex(s => s.TileSet == rules.TileSet && s.Name == (oldName ?? rules.Name));
        var next = new List<AutoTileRuleSet>(_sets);
        if (index >= 0) next[index] = rules;
        else next.Add(rules);
        Commit(next);
    }

    public void Remove(string tileSet, string name) => Commit([.. _sets.Where(s => !(s.TileSet == tileSet && s.Name == name))]);

    /// <summary>A name not used yet in the tile set: the wanted name, then name 2, name 3.</summary>
    public string FreeName(string tileSet, string wanted)
    {
        var name = wanted;
        for (var i = 2; _sets.Any(s => s.TileSet == tileSet && s.Name == name); i++) name = wanted + " " + i;
        return name;
    }

    public static string Export(AutoTileRuleSet rules) => JsonSerializer.Serialize(rules, Json);

    /// <summary>Reads a rule set exported by <see cref="Export"/>; null when the text is not one. Slots outside the layout are dropped.</summary>
    public static AutoTileRuleSet? Import(string text)
    {
        try
        {
            return Clean(JsonSerializer.Deserialize<AutoTileRuleSet>(text, Json));
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Reads a rule set from a file; null when the file is over <see cref="MaxImportBytes"/> or is not a rule set. A file that cannot be read throws IOException.</summary>
    public static async Task<AutoTileRuleSet?> ImportFileAsync(string path) =>
        new FileInfo(path).Length > MaxImportBytes ? null : Import(await File.ReadAllTextAsync(path));

    private static AutoTileRuleSet? Clean(AutoTileRuleSet? rules)
    {
        if (rules is not { Name.Length: > 0, TileSet.Length: > 0 } || !Enum.IsDefined(rules.Layout) || rules.Slots?.Count > MaxSlots) return null;
        var valid = AutoTiler.Slots(rules.Layout).ToHashSet();
        var slots = (rules.Slots ?? new Dictionary<int, int>()).Where(p => valid.Contains(p.Key) && p.Value > 0).ToDictionary();
        return rules with { Terrain = Math.Clamp(rules.Terrain, 1, 255), Slots = slots };
    }

    /// <summary>A tile set was renamed (or the rename undone): its rule sets follow. Rule sets left under the new name by a deleted tile set are replaced.</summary>
    public void RenameTileSet(string from, string to)
    {
        if (!_sets.Any(s => s.TileSet == from)) return;
        Commit([.. _sets.Where(s => s.TileSet != to).Select(s => s.TileSet == from ? s with { TileSet = to } : s)]);
    }

    private void Commit(List<AutoTileRuleSet> next)
    {
        try
        {
            UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(new Content(next), Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            Failed?.Invoke(e); // the change is dropped, so the screen keeps showing what is on disk
            return;
        }

        _sets = next;
        Changed?.Invoke();
    }
}
