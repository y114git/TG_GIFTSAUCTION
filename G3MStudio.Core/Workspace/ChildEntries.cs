using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>The kinds of code entry that are generated from a function inside another entry and have no text of their own.</summary>
public enum ChildEntryKind { Anonymous, Struct, Inner }

public static class ChildEntries
{
    /// <summary>The kind of a code entry that sits inside a parent entry, or null for any other item. The parent is set when the file is read, so this costs a field read.</summary>
    public static ChildEntryKind? KindOf(object? item)
    {
        if (item is not GameMakerCode { ParentEntry: not null } code) return null;
        var name = code.Name?.Content ?? "";
        // The compiler names the functions it makes itself gml_Script_anon_... and the constructors of struct literals gml_Script____struct___N_...
        if (name.Contains("____struct___", StringComparison.Ordinal)) return ChildEntryKind.Struct;
        return name.StartsWith("gml_Script_anon_", StringComparison.Ordinal) ? ChildEntryKind.Anonymous : ChildEntryKind.Inner;
    }

    /// <summary>What the settings say about the entry: Show for anything that is not a child entry.</summary>
    public static EntryVisibility VisibilityOf(object? item, AppSettings settings) => KindOf(item) switch
    {
        ChildEntryKind.Anonymous => settings.ExplorerAnonymousFunctions,
        ChildEntryKind.Struct => settings.ExplorerStructConstructors,
        ChildEntryKind.Inner => settings.ExplorerInnerFunctions,
        _ => EntryVisibility.Show,
    };
}
