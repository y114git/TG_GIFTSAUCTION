using G3MStudio.Core.GmlDocs;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The help text of a documented built-in name, as the hover and the parameter hint show it.</summary>
public static class GmlHelp
{
    /// <summary>Signature (or kind and name), the one line description, then the category and flags. Place turns the code entry a game function is defined in into what the interface shows (its nickname).</summary>
    public static string Describe(RefEntry entry, string word, Func<string, string>? place = null)
    {
        if (entry.Game) return DescribeGame(entry, place);
        var head = entry.Kind switch
        {
            RefKind.Function => entry.Signature + (entry.Returns.Length > 0 && entry.Returns != "Undefined" ? "  ->  " + entry.Returns : ""),
            RefKind.Variable => "var " + word + (entry.Returns.Length > 0 ? ": " + entry.Returns : ""),
            _ => "const " + word,
        };
        var flags = new List<string> { entry.Category };
        if (entry.Deprecated) flags.Add(Loc.Current["code.hover.deprecated"]);
        if (entry.ReadOnly) flags.Add(Loc.Current["code.hover.readOnly"]);
        return head + "\n" + entry.Summary + "\n" + string.Join("  |  ", flags);
    }

    /// <summary>A game function: signature, description, the documented parameters, then what it is and where it is defined.</summary>
    private static string DescribeGame(RefEntry entry, Func<string, string>? place)
    {
        var lines = new List<string> { entry.Signature + (entry.Returns.Length > 0 ? "  ->  " + entry.Returns : "") };
        if (entry.Summary.Length > 0) lines.Add(entry.Summary);
        lines.AddRange(entry.Parameters.Where(p => p.Type.Length + p.Meaning.Length > 0).Select(p => $"{p.Name}: {(p.Type.Length > 0 ? p.Type : "?")}" + (p.Meaning.Length > 0 ? " - " + p.Meaning : "")));
        lines.Add(Loc.Current["code.hover.gameFunction"] + (entry.Where is { Length: > 0 } where ? "  |  " + Loc.Current.Format("code.hover.where", ("where", (place?.Invoke(where) ?? where).Replace(":", ": ", StringComparison.Ordinal))) : ""));
        return string.Join('\n', lines);
    }

    /// <summary>The line under the parameter hint: the active parameter with its type and meaning, then the description of the function. Empty without documentation.</summary>
    public static string ActiveParameter(Signature signature, int argument)
    {
        if (signature.Doc is not { } doc) return "";
        var named = doc.Parameters.Where(p => !p.Rest).ToList();
        // Past the named parameters a variadic function reads its rest parameter; a variadic one without a rest parameter repeats the last.
        var parameter = argument < 0 ? null : argument < named.Count ? named[argument] : doc.Variadic ? doc.Parameters.FirstOrDefault(p => p.Rest) ?? named.LastOrDefault() : null;
        var line = parameter is null ? "" : $"{parameter.Name}: {parameter.Type}" + (parameter.Meaning.Length > 0 ? " - " + parameter.Meaning : "") + "\n";
        return line + doc.Summary;
    }
}
