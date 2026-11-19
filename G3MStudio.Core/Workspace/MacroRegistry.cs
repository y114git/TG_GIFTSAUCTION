using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The macros declared in the GML text last applied to each code entry of a data file. GameMaker expands a macro while compiling and keeps no record of it, so
/// the data file cannot tell them: a macro one entry declares and another uses would show as a wrong name or a syntax error in the code editor unless the text it
/// came from is remembered. Applying an entry (<see cref="EditCodeCommand"/>) fills this, so undo, redo and the journal replay keep it right.
/// Every member is safe to call from any thread.
/// </summary>
public sealed class MacroRegistry
{
    private static readonly ConditionalWeakTable<GameMakerData, MacroRegistry> Registries = [];

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _byEntry = new(StringComparer.Ordinal);

    public static MacroRegistry Of(GameMakerData data) => Registries.GetValue(data, _ => new MacroRegistry());

    /// <summary>The macros of every entry but <paramref name="except"/> (its own text brings its own), as the name and the GML it stands for. The first entry to declare a name keeps it.</summary>
    public IReadOnlyDictionary<string, string> Table(string? except = null)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (entry, macros) in _byEntry.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (entry == except) continue;
            foreach (var (name, value) in macros) table.TryAdd(name, value);
        }

        return table;
    }

    /// <summary>Remembers the macros of the entry's new text (none forgets the old ones) and returns what it held before, for undo.</summary>
    internal IReadOnlyDictionary<string, string>? Set(string entry, IReadOnlyDictionary<string, string>? macros)
    {
        _byEntry.TryGetValue(entry, out var before);
        if (macros is { Count: > 0 }) _byEntry[entry] = macros;
        else _byEntry.TryRemove(entry, out _);
        return before;
    }
}
