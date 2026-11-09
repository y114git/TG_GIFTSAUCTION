using Avalonia.Threading;
using G3MLib.Analyzer.Compiler;
using G3MLib.Analyzer.Mock;
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

    /// <summary>Whether a live check is waiting for the typing pause, for tests.</summary>
    internal bool LivePending => _live.IsEnabled;

    /// <summary>Reads the problems of a text; the parser, replaceable so tests can make it fail.</summary>
    public Func<string, IReadOnlyList<GmlDiagnostic>> LiveParser { get; set; } = LiveCheck.Parse;

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
        };
        // A check cancelled by hiding the tab is owed when the tab comes back and its problems are older than its text.
        AttachedToVisualTree += (_, _) =>
        {
            if (_problemsVersion != _textVersion) ScheduleLive();
        };
    }

    // Apply is about to show the compile errors: a parse that is waiting or running must not overwrite them.
    private void CancelLive()
    {
        _liveRun++;
        _live.Stop();
    }

    // Called on every edit by the user: cancel what runs and wait for a pause.
    private void ScheduleLive()
    {
        _liveRun++;
        _live.Stop();
        if (EditorOptions.LiveCheck) _live.Start();
    }

    /// <summary>Parses the text now and shows what it found. Skipped when the option is off, the text is not GML or it is longer than <see cref="EditorOptions.LiveCheckMaxChars"/> (the compile on Apply checks those).</summary>
    public async Task CheckLiveAsync()
    {
        var run = ++_liveRun;
        if (!EditorOptions.LiveCheck || _loading || Mode != CodeMode.Decompiled || Editor.Text.Length > EditorOptions.LiveCheckMaxChars) return;
        var (text, version) = (Editor.Text, _textVersion);
        IReadOnlyList<GmlDiagnostic> found;
        try
        {
            found = await Task.Run(() => LiveParser(text));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A parser failure must not crash the editor; the squiggles of this entry are dropped.
            found = [];
        }

        if (run != _liveRun || version != _textVersion) return;
        _workspace.Problems.Set(Entry, found);
        _problemsVersion = version;
        RedrawOverlays();
    }
}

/// <summary>The problems the parser reports for a GML text, without touching the data file.</summary>
public static class LiveCheck
{
    public static IReadOnlyList<GmlDiagnostic> Parse(string text)
    {
        var context = new CompileContext(text, CompileScriptKind.Script, null, new GameContextMock());
        context.Parse();
        return [.. context.Diagnostics];
    }
}
