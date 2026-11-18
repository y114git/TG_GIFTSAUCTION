using System.ComponentModel;
using System.Text.Json;

namespace G3MStudio.Core.Localization;

/// <summary>UI strings per ADR-0006: one G3M-style JSON file per language, nested objects flattened to dotted keys, English as fallback.</summary>
public sealed partial class Localizer : INotifyPropertyChanged
{
    public const string FallbackLanguage = "en";

    private readonly Dictionary<string, Dictionary<string, string>> _strings;
    private readonly Dictionary<string, string> _names;
    private Dictionary<string, string> _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Localizer(IEnumerable<(string Code, Stream Json)> languages)
    {
        _strings = [];
        _names = [];
        foreach (var (code, json) in languages)
        {
            using (json)
            {
                var flat = Flatten(json);
                _names[code] = flat.GetValueOrDefault("metadata.language_name", code);
                _strings[code] = flat;
            }
        }

        Language = FallbackLanguage;
        _current = _strings[FallbackLanguage];
    }

    public string Language { get; private set; }

    public IReadOnlyDictionary<string, string> Languages => _names;

    public string this[string key] => _current.TryGetValue(key, out var text) || _strings[FallbackLanguage].TryGetValue(key, out text) ? text : key;

    /// <summary>Whether the current language or English has this key; the indexer returns the key itself when it has not.</summary>
    public bool Has(string key) => _current.ContainsKey(key) || _strings[FallbackLanguage].ContainsKey(key);

    /// <summary>Fills {name} placeholders. A token {name:one|other} (three forms for Russian) picks the form for the number given as that argument.</summary>
    public string Format(string key, params (string Name, object Value)[] args)
    {
        var text = this[key];
        if (text.Contains(':', StringComparison.Ordinal)) text = PluralToken().Replace(text, m => PluralOf(m.Groups[2].Value.Split('|'), args.Where(a => a.Name == m.Groups[1].Value && a.Value is sbyte or byte or short or ushort or int or uint or long or ulong).Select(a => (long?)Convert.ToInt64(a.Value, System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault(), Language));
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal);
        return text;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{(\w+):([^{}|]*(?:\|[^{}|]*)+)\}")]
    private static partial System.Text.RegularExpressions.Regex PluralToken();

    /// <summary>Index of the plural form for a count: Russian has one, few and many; Japanese, Korean and Chinese have one form; the others have one and other.</summary>
    public static int PluralIndex(string language, long count)
    {
        if (language is "ja" or "ko" or "zh_cn" or "zh_tw") return 0;
        if (language != "ru") return count == 1 ? 0 : 1;
        var n = Math.Abs(count);
        return n % 10 == 1 && n % 100 != 11 ? 0 : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? 1 : 2;
    }

    // A count that is not a whole number (or not given) takes the last form, so no braces ever show.
    private static string PluralOf(string[] forms, long? count, string language) => forms[count is { } n ? Math.Min(PluralIndex(language, n), forms.Length - 1) : forms.Length - 1];

    /// <summary>The language that matches a culture (the language of the operating system), or English when none does. Chinese is split by script or region.</summary>
    public static string Detect(System.Globalization.CultureInfo culture, IEnumerable<string> available)
    {
        var name = culture.Name.ToLowerInvariant();
        var code = name.StartsWith("zh", StringComparison.Ordinal)
            ? (name.Contains("hant", StringComparison.Ordinal) || name.EndsWith("-tw", StringComparison.Ordinal) || name.EndsWith("-hk", StringComparison.Ordinal) || name.EndsWith("-mo", StringComparison.Ordinal) ? "zh_tw" : "zh_cn")
            : culture.TwoLetterISOLanguageName;
        return available.Contains(code) ? code : FallbackLanguage;
    }

    public void SetLanguage(string code)
    {
        if (!_strings.TryGetValue(code, out var strings)) throw new ArgumentException($"Unknown language '{code}'.", nameof(code));
        Language = code;
        _current = strings;
        // "Item[]" and "Item" tell indexer bindings to read every key again.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public static Localizer LoadEmbedded()
    {
        var assembly = typeof(Localizer).Assembly;
        const string prefix = "G3MStudio.Core.Languages.lang_";
        return new Localizer(assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => (n[prefix.Length..^".json".Length], assembly.GetManifestResourceStream(n) ?? throw new InvalidOperationException($"Resource {n} is missing."))));
    }

    public static Dictionary<string, string> Flatten(Stream json)
    {
        var result = new Dictionary<string, string>();
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement, "", result);
        return result;
    }

    private static void Walk(JsonElement element, string path, Dictionary<string, string> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject()) Walk(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name, result);
        }
        else
        {
            result[path] = element.GetString() ?? "";
        }
    }
}
