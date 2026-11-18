namespace G3MStudio.Editor;

/// <summary>A light check of shader source text: unbalanced brackets, a missing main function and a missing output. It is no compiler, so it can miss errors and never reports a type error.</summary>
public static class ShaderCheck
{
    /// <summary>Returns one message per problem found, empty when the text passes. The checks keep to what holds for GLSL and HLSL9 sources alike.</summary>
    public static IReadOnlyList<string> Check(string source, bool vertex, bool hlsl)
    {
        var problems = new List<string>();
        var code = StripComments(source);
        Balance(code, problems);
        if (!code.Contains("main", StringComparison.Ordinal)) problems.Add("editors.shaderCheck.noMain");
        else if (!hlsl && !code.Contains(vertex ? "gl_Position" : "gl_FragColor", StringComparison.Ordinal) && !(code.Contains("gl_FragData", StringComparison.Ordinal) && !vertex)) problems.Add(vertex ? "editors.shaderCheck.noPosition" : "editors.shaderCheck.noColor");
        return problems;
    }

    private static string StripComments(string text)
    {
        var lines = text.Split('\n').Select(l => l.IndexOf("//", StringComparison.Ordinal) is var at and >= 0 ? l[..at] : l);
        var joined = string.Join('\n', lines);
        while (joined.IndexOf("/*", StringComparison.Ordinal) is var start and >= 0)
        {
            var end = joined.IndexOf("*/", start + 2, StringComparison.Ordinal);
            joined = end < 0 ? joined[..start] : joined[..start] + joined[(end + 2)..];
        }

        return joined;
    }

    private static void Balance(string code, List<string> problems)
    {
        var stack = new Stack<char>();
        foreach (var c in code)
        {
            if (c is '(' or '{' or '[') stack.Push(c);
            else if (c is ')' or '}' or ']')
            {
                var open = c switch { ')' => '(', '}' => '{', _ => '[' };
                if (stack.Count == 0 || stack.Pop() != open)
                {
                    problems.Add("editors.shaderCheck.brackets");
                    return;
                }
            }
        }

        if (stack.Count > 0) problems.Add("editors.shaderCheck.brackets");
    }
}
