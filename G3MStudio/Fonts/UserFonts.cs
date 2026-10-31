using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace G3MStudio.Fonts;

/// <summary>
/// Fonts the user added (Q56): copies of the font files live in one folder of the user data and are registered with Avalonia at start.
/// A font cannot be unregistered while the program runs, so a removed font disappears from the lists at once and from memory at the next start.
/// </summary>
public sealed class UserFonts(string folder)
{
    private static readonly string[] Extensions = [".ttf", ".otf", ".ttc", ".otc"];

    private readonly Dictionary<string, (Uri Key, string File)> _families = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Families => _families.Keys;

    public static IReadOnlyList<string> FilePatterns { get; } = [.. Extensions.Select(e => "*" + e)];

    /// <summary>Registers every font file already in the folder.</summary>
    public void LoadAll()
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder).Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            Register(file);
    }

    /// <summary>Copies a font file in and registers it. Returns the family names it brought, none when the file is not a font.</summary>
    public IReadOnlyList<string> Add(string sourceFile)
    {
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileName(sourceFile));
        if (!File.Exists(target)) File.Copy(sourceFile, target);
        var added = Register(target);
        if (added.Count == 0 && !_families.Values.Any(v => v.File == target)) File.Delete(target); // not a font, nothing else uses the copy
        return added;
    }

    /// <summary>Deletes the font file of a family; the family stays registered until the next start.</summary>
    public void Remove(string family)
    {
        if (!_families.Remove(family, out var entry)) return;
        if (!_families.Values.Any(v => v.File == entry.File)) File.Delete(entry.File);
    }

    /// <summary>The family for a name the user chose among their own fonts, or null for a system font.</summary>
    public FontFamily? Resolve(string name) => _families.TryGetValue(name, out var entry) ? new FontFamily($"{entry.Key.AbsoluteUri}#{name}") : null;

    private List<string> Register(string file)
    {
        var key = new Uri("fonts:g3mstudio-user/" + Uri.EscapeDataString(Path.GetFileName(file)));
#pragma warning disable CA2000 // owned by the font manager once it is added; disposed here when the file holds no font
        var collection = new EmbeddedFontCollection(key, new Uri(file));
#pragma warning restore CA2000
        var names = collection.Select(f => f.Name).Distinct().ToList();
        if (names.Count == 0)
        {
            ((IDisposable)collection).Dispose();
            return names;
        }

        FontManager.Current.AddFontCollection(collection);
        foreach (var name in names) _families[name] = (key, file);
        return names;
    }
}
