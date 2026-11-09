using G3MStudio.Core.GmlDocs;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The help text of a documented built-in name, as the hover and the parameter hint show it.</summary>
public static class GmlHelp
{
    /// <summary>Signature (or kind and name), the one line description, then the category and flags.</summary>
    public static string Describe(RefEntry entry, string word)
    {
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
