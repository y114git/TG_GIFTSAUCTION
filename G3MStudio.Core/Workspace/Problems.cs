using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Core.Workspace;

/// <summary>A compile error thrown by an edit: the message for the user and the structured diagnostics for the Problems panel.</summary>
public sealed class CodeCompileException(string entry, IReadOnlyList<GmlDiagnostic> diagnostics, string message) : InvalidOperationException(message)
{
    public string Entry { get; } = entry;

    public IReadOnlyList<GmlDiagnostic> Diagnostics { get; } = diagnostics;
}

/// <summary>One line of the Problems panel. Line and column are one based.</summary>
public sealed record Problem(string Source, string Severity, string Code, string Message, int Line, int Column);

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
        _bySource[source] = [.. diagnostics.Select(d => new Problem(source, d.Severity.ToString(), d.Code, d.Message, d.Span.StartLine, d.Span.StartColumn)).DistinctBy(p => (p.Code, p.Line, p.Column))];
        Changed?.Invoke();
    }

    public void Clear(string source)
    {
        if (_bySource.Remove(source)) Changed?.Invoke();
    }
}
