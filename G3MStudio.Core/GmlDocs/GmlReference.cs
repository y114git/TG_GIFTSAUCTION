using System.Globalization;
using System.Text.Json;

namespace G3MStudio.Core.GmlDocs;

public enum RefKind { Function, Variable, Constant }

/// <summary>The translated texts of one entry: the description and the parameter notes by parameter name. Either may be missing, the English text then stays.</summary>
public sealed record Translation(string? Summary, IReadOnlyDictionary<string, string> Parameters);

/// <summary>One parameter of a documented function. <paramref name="Optional"/> ones may be left out, <paramref name="Rest"/> takes any number of values.</summary>
public sealed record RefParam(string Name, string Type, string Meaning, bool Optional, bool Rest);

/// <summary>A documented built-in name. A constant entry is a group: its <see cref="Name"/> ends in * (vk_* covers every vk_ constant).</summary>
public sealed record RefEntry(string Name, RefKind Kind, string Category, string Summary, IReadOnlyList<RefParam> Parameters, string Returns)
{
    public bool Deprecated { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>The manual page, relative to the manual folder.</summary>
    public string? Page { get; init; }

    /// <summary>How many arguments must be given.</summary>
    public int Required => Parameters.Count(p => !p.Optional && !p.Rest);

    public bool Variadic => Parameters.Any(p => p.Rest);

    /// <summary>name(a, b, [c], ...) with the optional ones in square brackets.</summary>
    public string Signature => Kind != RefKind.Function ? Name : Name + "(" + string.Join(", ", Parameters.Select(p => p.Rest ? "..." + p.Name : p.Optional ? "[" + p.Name + "]" : p.Name)) + ")";
}

/// <summary>
/// The bundled GML reference: names, parameters and short original descriptions for the common built-in functions, variables and constant groups.
/// It holds no text of the GameMaker manual; each entry only knows the manual page it links to.
/// </summary>
public sealed class GmlReference
{
    private readonly Dictionary<string, RefEntry> _byName = new(StringComparer.Ordinal);
    private readonly List<RefEntry> _groups = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, GmlReference> _languages = new(StringComparer.Ordinal);

    private GmlReference(string manual, List<RefEntry> entries)
    {
        Manual = manual;
        Entries = entries;
        foreach (var entry in entries)
        {
            if (entry.Kind == RefKind.Constant && entry.Name.EndsWith('*')) _groups.Add(entry);
            else _byName.TryAdd(entry.Name, entry);
        }

        // The longest prefix first, so gp_axis* wins over gp_* for gp_axislh.
        _groups.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
        Categories = [.. entries.Select(e => e.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>The manual folder every page link is relative to, ending in a slash.</summary>
    public string Manual { get; }

    public IReadOnlyList<RefEntry> Entries { get; }

    public IReadOnlyList<string> Categories { get; }

    private static readonly Lazy<GmlReference> Embedded = new(() =>
    {
        var assembly = typeof(GmlReference).Assembly;
        using var stream = assembly.GetManifestResourceStream("G3MStudio.Core.Resources.gml-reference.json") ?? throw new InvalidOperationException("The GML reference resource is missing.");
        return Parse(stream);
    });

    // The reference in the assembly resource.
    public static GmlReference Default => Embedded.Value;

    /// <summary>The key of an entry in a translation file: the kind badge and the name, like f:abs or c:vk_*.</summary>
    public static string KeyOf(RefEntry entry) => Badge(entry.Kind) + ":" + entry.Name;

    /// <summary>The translation file of a language from the assembly resources, or null when there is none (English or an unknown code).</summary>
    public static IReadOnlyDictionary<string, Translation>? LoadTranslation(string language)
    {
        using var stream = typeof(GmlReference).Assembly.GetManifestResourceStream("G3MStudio.Core.Resources.gml-reference." + language + ".json");
        return stream is null ? null : ParseTranslation(stream);
    }

    public static IReadOnlyDictionary<string, Translation> ParseTranslation(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, Translation>(StringComparer.Ordinal);
        foreach (var e in document.RootElement.EnumerateObject())
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (e.Value.TryGetProperty("p", out var list))
                foreach (var p in list.EnumerateObject()) parameters[p.Name] = p.Value.GetString() ?? "";
            result[e.Name] = new Translation(e.Value.TryGetProperty("s", out var s) ? s.GetString() : null, parameters);
        }

        return result;
    }

    /// <summary>The reference with the descriptions and parameter notes of a language; every text without a translation stays English. English and unknown codes give this reference.</summary>
    public GmlReference For(string language)
    {
        if (language == "en") return this;
        return _languages.GetOrAdd(language, code => LoadTranslation(code) is { } table ? Translated(table) : this);
    }

    private GmlReference Translated(IReadOnlyDictionary<string, Translation> table)
    {
        RefEntry Apply(RefEntry entry)
        {
            if (!table.TryGetValue(KeyOf(entry), out var t)) return entry;
            return entry with
            {
                Summary = string.IsNullOrEmpty(t.Summary) ? entry.Summary : t.Summary,
                Parameters = [.. entry.Parameters.Select(p => t.Parameters.TryGetValue(p.Name, out var meaning) && meaning.Length > 0 ? p with { Meaning = meaning } : p)],
            };
        }

        return new GmlReference(Manual, [.. Entries.Select(Apply)]);
    }

    public static GmlReference Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var entries = new List<RefEntry>();
        foreach (var e in root.GetProperty("entries").EnumerateArray())
        {
            string Text(string key) => e.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
            var kind = Text("k") switch { "f" => RefKind.Function, "v" => RefKind.Variable, _ => RefKind.Constant };
            var parameters = new List<RefParam>();
            if (e.TryGetProperty("p", out var list))
            {
                foreach (var p in list.EnumerateArray())
                {
                    var name = p[0].GetString() ?? "";
                    var rest = name.StartsWith("...", StringComparison.Ordinal);
                    var optional = name.StartsWith('?');
                    parameters.Add(new RefParam(name.TrimStart('?').TrimStart('.'), p[1].GetString() ?? "", p[2].GetString() ?? "", optional, rest));
                }
            }

            entries.Add(new RefEntry(Text("n"), kind, Text("c"), Text("s"), parameters, Text("r")) { Deprecated = e.TryGetProperty("d", out _), ReadOnly = e.TryGetProperty("ro", out _), Page = e.TryGetProperty("u", out var page) ? page.GetString() : null });
        }

        return new GmlReference(root.GetProperty("manual").GetString() ?? "", entries);
    }

    /// <summary>The entry of a name: a function or variable by its exact name, else the constant group it belongs to.</summary>
    public RefEntry? Find(string name)
    {
        if (_byName.TryGetValue(name, out var found)) return found;
        return _groups.FirstOrDefault(g => name.StartsWith(g.Name.AsSpan(0, g.Name.Length - 1), StringComparison.Ordinal));
    }

    /// <summary>The web address of the manual page of an entry; entries without a page of their own lead to the reference index.</summary>
    public string ManualLink(RefEntry entry) => entry.Page is { Length: > 0 } page ? Manual + page : Manual.TrimEnd('/') + ".htm";

    /// <summary>Entries whose name or description matches every word of the query, best first: name starts with the text, name contains it, description contains it. A category narrows the list.</summary>
    public IReadOnlyList<RefEntry> Search(string query, string? category = null, int limit = 300)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scored = new List<(RefEntry Entry, int Score)>();
        foreach (var entry in Entries)
        {
            if (category is { Length: > 0 } && entry.Category != category) continue;
            var score = 0;
            foreach (var word in words)
            {
                var s = entry.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase) ? 3 : entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase) ? 2 : entry.Summary.Contains(word, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                if (s == 0) { score = -1; break; }
                score += s;
            }

            if (score >= 0) scored.Add((entry, score));
        }

        return [.. scored.OrderByDescending(x => x.Score).ThenBy(x => x.Entry.Name, StringComparer.Ordinal).Take(limit).Select(x => x.Entry)];
    }

    /// <summary>The kinds as the list shows them, for the one letter badge: f, v or c.</summary>
    public static string Badge(RefKind kind) => kind switch { RefKind.Function => "f", RefKind.Variable => "v", _ => "c" };

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Entries.Count} entries");
}
