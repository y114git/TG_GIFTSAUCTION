using System.Globalization;
using System.Reflection;
using G3MLib.Analyzer.Decompiler;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The decompiler settings of GameMaker code (UTMT-GMLS-01 to GMLS-08, UTMT-SET-16): which of the library's <see cref="DecompileSettings"/> the user can change, read from the type so a
/// setting the library adds shows up without a change here, and how values saved in the settings file are applied to the settings of a data file.
/// </summary>
public static class DecompilerOptions
{
    /// <summary>The plain on or off switches and text values of the library's settings, in the library's own order.</summary>
    public static IReadOnlyList<PropertyInfo> Fields { get; } =
    [.. typeof(DecompileSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && (p.PropertyType == typeof(bool) || p.PropertyType == typeof(string))).OrderBy(p => p.MetadataToken)];

    public static bool IsText(PropertyInfo field) => field.PropertyType == typeof(string);

    /// <summary>The value the library starts with, as text.</summary>
    public static string DefaultOf(PropertyInfo field) => Show(field.GetValue(new DecompileSettings())!);

    public static string Show(object value) => value switch
    {
        bool on => on ? "true" : "false",
        _ => value.ToString() ?? "",
    };

    /// <summary>
    /// Puts the saved values into the settings of a data file: unknown names are ignored, a value that does not fit its type (a switch that is not true or false, a pattern without {0}, an empty enum name)
    /// leaves the library's default, and every field that has no saved value goes back to the default.
    /// </summary>
    public static void Apply(DecompileSettings target, IReadOnlyDictionary<string, string> saved)
    {
        foreach (var field in Fields)
        {
            var value = saved.TryGetValue(field.Name, out var text) && Valid(field, text) ? text : DefaultOf(field);
            field.SetValue(target, IsText(field) ? value : bool.Parse(value));
        }
    }

    /// <summary>Whether text is acceptable for a field: a switch is true or false; a text setting is not empty, a pattern keeps its {0}, and a name is a GML name.</summary>
    public static bool Valid(PropertyInfo field, string text)
    {
        if (!IsText(field)) return bool.TryParse(text, out _);
        if (text.Length == 0 && field.Name != nameof(DecompileSettings.IndentString)) return false;
        if (field.Name.EndsWith("Pattern", StringComparison.Ordinal)) return text.Contains("{0}", StringComparison.Ordinal);
        if (field.Name == nameof(DecompileSettings.UnknownEnumName)) return text.Length > 0 && !char.IsDigit(text[0]) && text.All(c => char.IsLetterOrDigit(c) || c == '_');
        return true;
    }

    /// <summary>A setting name as words ("OpenBlockBraceOnSameLine" becomes "Open block brace on same line").</summary>
    public static string Label(string name)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)).Split(' ');
        var text = string.Join(' ', words.Select((w, i) => i == 0 ? w : (w.All(char.IsUpper) ? w : w.ToLower(CultureInfo.InvariantCulture))));
        return text;
    }
}
