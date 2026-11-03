using G3MLib.Analyzer.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlScopesTests
{
    private static List<(string Name, string Scope)> Find(string text)
    {
        var tokens = GmlTokenizer.Tokenize(text, true);
        return [.. GmlScopes.Find(text, tokens).OrderBy(p => p.Key).Select(p => (text[tokens[p.Key].Span.StartOffset..tokens[p.Key].Span.EndOffset], p.Value))];
    }

    [Fact]
    public void LocalsAreColouredWhereverTheyAreUsedButNotAsMembers()
    {
        var found = Find("var a = 1, b = c + 2; a += b; obj.a = 3;");
        Assert.Equal([("a", "Local"), ("b", "Local"), ("a", "Local"), ("b", "Local")], found);
    }

    [Fact]
    public void GlobalsComeFromGlobalDotNameAndFromGlobalvar()
    {
        var found = Find("global.score = 1; globalvar lives; lives = global.score + other;");
        Assert.Equal([("score", "Global"), ("lives", "Global"), ("lives", "Global"), ("score", "Global")], found);
    }

    [Fact]
    public void AValueWithCommasInsideCallsDoesNotStartAnotherDeclaration()
    {
        var found = Find("var a = max(x, y), b = 2;");
        Assert.Equal([("a", "Local"), ("b", "Local")], found);
    }
}
