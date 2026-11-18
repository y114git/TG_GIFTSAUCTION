using G3MLib.Analyzer.Compiler;
using G3MLib.DataFile.Compiler;

namespace G3MStudio.Core.Workspace;

/// <summary>A compile error thrown by an edit: the message for the user and the structured diagnostics for the Problems panel.</summary>
public sealed class CodeCompileException(string entry, IReadOnlyList<GmlDiagnostic> diagnostics, string message) : InvalidOperationException(message)
{
    public string Entry { get; } = entry;

    public IReadOnlyList<GmlDiagnostic> Diagnostics { get; } = diagnostics;

    /// <summary>The exception of a failed compile. The parser reports one fault several times (the same place, or a cascade from it): each place is listed once, with its first message.</summary>
    public static CodeCompileException From(string entry, CompileResult result)
    {
        var errors = (result.Errors ?? []).DistinctBy(e => (e.Code.Name?.Content, e.Diagnostic.Span.StartOffset)).ToList();
        var message = string.Join('\n', errors.Select(e => (e.Code.Name?.Content is { Length: > 0 } code ? $"{code}: " : "") + e.GenerateDetailedMessage().TrimEnd()));
        return new CodeCompileException(entry, [.. errors.Select(e => e.Diagnostic)], message.Length > 0 ? message : result.PrintAllErrors(true));
    }
}

/// <summary>One line of the Problems panel. Line and column are one based; <paramref name="Length"/> is the length of the marked text, 0 when unknown.</summary>
public sealed record Problem(string Source, string Severity, string Code, string Message, int Line, int Column, int Length = 0);

/// <summary>What the Problems panel lists: the latest problems of each source (a code entry), replaced as a whole.</summary>
public sealed class ProblemsModel
{
    private readonly Dictionary<string, IReadOnlyList<Problem>> _bySource = [];

    public IEnumerable<Problem> All => _bySource.Values.SelectMany(p => p).OrderBy(p => p.Source, StringComparer.Ordinal).ThenBy(p => p.Line).ThenBy(p => p.Column);

    public event Action? Changed;

    public void Set(string source, IReadOnlyList<GmlDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0) { Clear(source); return; }
        // The compiler can report the same spot twice with the same code (a short and a longer wording); the panel shows the first.
        _bySource[source] = [.. diagnostics.Select(d => new Problem(source, d.Severity.ToString(), d.Code, d.Message, d.Span.StartLine, d.Span.StartColumn, d.MacroName is null ? d.Span.Length : 0)).DistinctBy(p => (p.Code, p.Line, p.Column))];
        Changed?.Invoke();
    }

    /// <summary>Replaces what a source other than a code entry reported (the last run); the rows keep their own <see cref="Problem.Source"/>.</summary>
    public void SetRaw(string key, IReadOnlyList<Problem> problems)
    {
        if (problems.Count == 0) { Clear(key); return; }
        _bySource[key] = problems;
        Changed?.Invoke();
    }

    public void Clear(string source)
    {
        if (_bySource.Remove(source)) Changed?.Invoke();
    }
}
