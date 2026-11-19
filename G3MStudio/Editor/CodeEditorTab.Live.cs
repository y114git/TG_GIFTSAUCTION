using Avalonia.Threading;
using G3MLib.Analyzer.Compiler;
using G3MLib.Analyzer.Mock;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

// Syntax problems while typing: about 400 ms after the last key the text is parsed on a background thread. A key press makes the run in flight obsolete,
// and a result for older text is dropped, so typing never waits and a squiggle never lands on the wrong place.
public sealed partial class CodeEditorTab
{
    // Quiet time after the last key before the text is parsed.
    public static readonly TimeSpan LiveDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _live = new() { Interval = LiveDelay };
    private int _liveRun;

    // Stops the code check of the run in flight, which a key press, Apply or hiding the tab made obsolete; null when none runs.
    private Action? _stopLint;

    private void StopLint()
    {
        _stopLint?.Invoke();
        _stopLint = null;
    }

    // The text version whose problems the last live check showed; -1 once Apply shows its own.
    private int _liveShown = -1;

    /// <summary>Whether a live check is waiting for the typing pause, for tests.</summary>
    internal bool LivePending => _live.IsEnabled;

    /// <summary>Reads the problems of a text; the parser, replaceable so tests can make it fail. The default expands the macros other entries declare, so they are never reported as errors.</summary>
    public Func<string, IReadOnlyList<GmlDiagnostic>>? LiveParser { get; set; }

    private void InstallLive()
    {
        _live.Tick += (_, _) =>
        {
            _live.Stop();
            _ = CheckLiveAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _live.Stop();
            _liveRun++;
            StopLint();
            GmlProjectSymbols.Ready -= SymbolsReady;
        };
        // A check cancelled by hiding the tab is owed when the tab comes back and its problems are older than its text.
        AttachedToVisualTree += (_, _) =>
        {
            GmlProjectSymbols.Ready += SymbolsReady;
            if (_problemsVersion != _textVersion) ScheduleLive();
        };
    }

    // The names other entries declare are known now: findings made without them are checked again, unless Apply's compile errors are shown.
    private void SymbolsReady(Workspace ready)
    {
        if (ReferenceEquals(ready, _workspace) && LiveLint && _liveShown == _textVersion) _ = CheckLiveAsync();
    }

    // Apply is about to show the compile errors: a parse that is waiting or running must not overwrite them.
    private void CancelLive()
    {
        _liveShown = -1;
        _liveRun++;
        StopLint();
        _live.Stop();
    }

    // Called on every edit by the user: cancel what runs and wait for a pause.
    private void ScheduleLive()
    {
        _liveRun++;
        StopLint();
        _live.Stop();
        if (EditorOptions.LiveCheck) _live.Start();
    }

    /// <summary>Parses the text now and shows what it found. Skipped when the option is off, the text is not GML or it is longer than <see cref="EditorOptions.LiveCheckMaxChars"/> (the compile on Apply checks those).</summary>
    public async Task CheckLiveAsync()
    {
        var run = ++_liveRun;
        StopLint();
        if (!EditorOptions.LiveCheck || _loading || Mode == CodeMode.Assembly || Editor.Text.Length > EditorOptions.LiveCheckMaxChars) return;
        using var source = new CancellationTokenSource();
        var cancel = source.Token;
        _stopLint = source.Cancel;
        var (text, version) = (Editor.Text, _textVersion);
        var context = LiveLint ? GmlLintSource.For(_workspace, Target) : null;
        IReadOnlyList<GmlDiagnostic> found;
        IReadOnlyList<LintIssue> issues = [];
        try
        {
            found = await Task.Run(() => LiveParser?.Invoke(text) ?? LiveCheck.Parse(text, MacroRegistry.Of(_workspace.Data).Table(Target)));
            // The code check reads brackets and statements from tokens: on text the parser rejects it would guess, so it waits for a clean parse.
            if (context is not null && !found.Any(d => d.Severity == DiagnosticSeverity.Error) && run == _liveRun)
                issues = await Task.Run(() => Lint(text, context, cancel));
        }
        catch (OperationCanceledException)
        {
            found = []; // a newer key press stopped this run; the newer run shows the squiggles
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A parser failure must not crash the editor; the squiggles of this entry are dropped.
            FailureReport.Report(e, "lint.parse");
            found = [];
        }
        finally
        {
            if (_stopLint?.Target == source) _stopLint = null; // a newer run has its own
        }

        if (run != _liveRun || version != _textVersion) return;
        _lintIssues = issues;
        _workspace.Problems.Set(Target, [.. found, .. issues.Select(i => i.Diagnostic)]);
        _problemsVersion = _liveShown = version;
        RedrawOverlays();
    }

    /// <summary>Whether the live check also runs the code check (unknown names, argument counts, unused locals and the rest).</summary>
    public bool LiveLint { get; set; } = true;

    // The findings of the last check, for their quick fixes; they belong to the text of _problemsVersion.
    private IReadOnlyList<LintIssue> _lintIssues = [];

    /// <summary>Empty once the text changed since the check: the offsets of the findings no longer match it.</summary>
    internal IEnumerable<QuickFix> LintFixesAt(int offset) => _problemsVersion != _textVersion ? [] :
        _lintIssues.Where(i => i.Fix is not null && offset >= i.Diagnostic.Span.StartOffset && offset <= i.Diagnostic.Span.EndOffset).Select(i => i.Fix).OfType<QuickFix>();

    private static IReadOnlyList<LintIssue> Lint(string text, GmlLintContext context, CancellationToken cancellationToken)
    {
        try
        {
            return GmlLint.Analyze(GmlDoc.Parse(text), context, cancellationToken);
        }
        catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException)) // a stopped run is dropped by the caller
        {
            // A check that fails on odd text shows nothing rather than stopping the parse results.
            FailureReport.Report(e, "lint.check");
            return [];
        }
    }
}

/// <summary>The problems the parser reports for a GML text, without touching the data file.</summary>
public static class LiveCheck
{
    /// <param name="text">The GML.</param>
    /// <param name="macros">The macros other code entries declare, expanded like the compile does.</param>
    public static IReadOnlyList<GmlDiagnostic> Parse(string text, IReadOnlyDictionary<string, string>? macros = null)
    {
        var context = new CompileContext(text, CompileScriptKind.Script, null, new GameContextMock()) { ExternalMacros = macros };
        context.Parse();
        return [.. context.Diagnostics];
    }
}
