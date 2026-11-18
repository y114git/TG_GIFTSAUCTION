namespace G3MStudio.Editor;

/// <summary>The text inserted for a function chosen in the completion list, and the tab stops in it: one per required parameter.</summary>
public sealed record CallSnippet(string Text, IReadOnlyList<(int Start, int Length)> Stops, int End)
{
    /// <summary>name(a, b) with each required parameter name as a stop. A function with no required parameter gets name() and one empty stop between the brackets when it has optional ones.</summary>
    public static CallSnippet For(Signature signature)
    {
        var name = signature.Name;
        var required = signature.Parameters.Take(signature.Required).ToList();
        var stops = new List<(int, int)>();
        var text = name + "(";
        for (var i = 0; i < required.Count; i++)
        {
            if (i > 0) text += ", ";
            stops.Add((text.Length, required[i].Length));
            text += required[i];
        }

        if (required.Count == 0 && (signature.Parameters.Count > 0 || signature.Variadic)) stops.Add((text.Length, 0));
        text += ")";
        return new CallSnippet(text, stops, text.Length);
    }
}
