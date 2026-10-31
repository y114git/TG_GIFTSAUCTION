using System.ComponentModel;
using System.Text.Json;

namespace G3MStudio.Core.Localization;

/// <summary>UI strings per ADR-0006: one G3M-style JSON file per language, nested objects flattened to dotted keys, English as fallback.</summary>
public sealed class Localizer : INotifyPropertyChanged
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

    public string LanguageName => _names[Language];

    public IReadOnlyDictionary<string, string> Languages => _names;

    public string this[string key] => _current.TryGetValue(key, out var text) || _strings[FallbackLanguage].TryGetValue(key, out text) ? text : key;

    public string Format(string key, params (string Name, object Value)[] args)
    {
        var text = this[key];
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal);
        return text;
    }

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
            .Select(n => (n[prefix.Length..^".json".Length], assembly.GetManifestResourceStream(n)!)));
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
