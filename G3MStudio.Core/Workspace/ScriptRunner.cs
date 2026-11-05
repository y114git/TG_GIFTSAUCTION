using G3MLib.DataFile;
using G3MLib.DataFile.Scripting;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// What the window gives a running script for the parts that need a person: questions, text boxes, file and folder prompts, progress and the open tab.
/// Each member is optional; a script that calls one that is missing fails with the library's "requires a UI host" error. The script runs on a pool thread,
/// so a host member may block until the user has answered.
/// </summary>
public sealed class ScriptHost
{
    public Func<string, bool>? Question { get; init; }

    /// <summary>Title, label, default text and whether the box has several lines; returns the text or null when cancelled.</summary>
    public Func<string, string, string, bool, string?>? Input { get; init; }

    public Func<string?>? ChooseDirectory { get; init; }

    /// <summary>Default extension and filter text of the script's call; returns the path or null when cancelled.</summary>
    public Func<string, string, string?>? LoadFile { get; init; }

    public Func<string, string, string?>? SaveFile { get; init; }

    /// <summary>Label (null when unchanged), status, value and maximum of the script's progress bar.</summary>
    public Action<string?, string, int, double>? Progress { get; init; }

    /// <summary>The assets a script asked to show with ChangeSelection, in order; the window opens the last one after the script ended (the data is locked while it runs).</summary>
    public List<object> Selections { get; } = [];

    /// <summary>False after the script called SetFinishedMessage(false).</summary>
    public bool FinishedMessage { get; set; } = true;
}

/// <summary>The globals a .csx script runs against in Studio: everything of the library's UndertaleModTool-compatible script interface, with the questions and prompts answered by the window.</summary>
public sealed class StudioScripts(GameMakerData data, string filePath, string scriptPath, ScriptHost host) : ScriptGlobals(data, filePath, scriptPath)
{
    public override bool ScriptQuestion(string message)
    {
        if (host.Question is { } ask) return ask(message);
        ScriptMessage(message);
        return true;
    }

    /// <summary>Stops the script; the title goes in front of the text, as in UndertaleModTool.</summary>
    public override void ScriptError(string error, string title = "Error", bool SetConsoleText = true) =>
        base.ScriptError(string.IsNullOrEmpty(title) ? error : title + ": " + error, title, SetConsoleText);

    public override string ScriptInputDialog(string title, string label, string defaultInput, string cancelText, string submitText, bool isMultiline, bool preventClose) =>
        host.Input is { } input ? Answer(input(title, label, defaultInput, isMultiline)) : base.ScriptInputDialog(title, label, defaultInput, cancelText, submitText, isMultiline, preventClose);

    public override string SimpleTextInput(string title, string label, string defaultValue, bool allowMultiline, bool showDialog = true) =>
        !showDialog ? defaultValue : host.Input is { } input ? Answer(input(title, label, defaultValue, allowMultiline)) : base.SimpleTextInput(title, label, defaultValue, allowMultiline, showDialog);

    public override string PromptChooseDirectory() => host.ChooseDirectory is { } choose ? Answer(choose()) : base.PromptChooseDirectory();

    public override string PromptLoadFile(string defaultExt, string filter) => host.LoadFile is { } load ? Answer(load(defaultExt, filter)) : base.PromptLoadFile(defaultExt, filter);

    public override string PromptSaveFile(string defaultExt, string filter) => host.SaveFile is { } save ? Answer(save(defaultExt, filter)) : base.PromptSaveFile(defaultExt, filter);

    public override void ChangeSelection(object newSelection, bool inNewTab = false)
    {
        base.ChangeSelection(newSelection, inNewTab);
        if (newSelection is not null) host.Selections.Add(newSelection);
    }

    public override void SetFinishedMessage(bool isFinishedMessageEnabled) => host.FinishedMessage = isFinishedMessageEnabled;

    public override Task ClickableSearchOutput(string title, string query, int resultsCount, IDictionary<string, List<(int lineNum, string codeLine)>> resultsDict, bool showInDecompiledView, IEnumerable<string>? failedList = null)
    {
        ScriptMessage($"{title}: {resultsCount} results for \"{query}\" in {resultsDict.Count} entries.");
        return Task.CompletedTask;
    }

    public override Task ClickableSearchOutput(string title, string query, int resultsCount, IOrderedEnumerable<KeyValuePair<string, List<(int lineNum, string codeLine)>>> resultsDict, bool showInDecompiledView, IOrderedEnumerable<string>? failedList = null) =>
        ClickableSearchOutput(title, query, resultsCount, resultsDict.ToDictionary(p => p.Key, p => p.Value), showInDecompiledView, failedList);

#nullable disable
    // Scripts test a cancelled answer against null, though the base signatures say string.
    private static string Answer(string answer) => answer;
#nullable restore

    protected override void ReportProgress(string label, string status, int value, double maximum) => host.Progress?.Invoke(label, status, value, maximum);
}

/// <summary>Runs a .csx script on the open data file as one bulk change: it cannot be undone and Revert to Saved is the way back.</summary>
public static class ScriptRunner
{
    /// <summary>Runs the script. Returns null when it finished, or the error text (compile error, ScriptError or an exception in the script).</summary>
    public static async Task<string?> RunAsync(Workspace workspace, string scriptPath, Action<string> message, Action<string> warning, ScriptHost? host = null, bool allowUnsaved = false)
    {
        var globals = new StudioScripts(workspace.Data, workspace.Path, scriptPath, host ?? new ScriptHost())
        {
            MessageHandler = message,
            WarningHandler = warning,
            DataFilePath = workspace.Path,
        };
        string? error = null;
        await workspace.ApplyBulkAsync(() =>
        {
            // The change runs on a pool thread without a synchronization context, so waiting here cannot deadlock; the write lock must stay held until the script ends.
            var result = G3MLib.DataFile.Scripting.ScriptRunner.RunAsync(scriptPath, globals).GetAwaiter().GetResult();
            error = result.Success ? null : result.Error;
        }, allowUnsaved);
        return error;
    }
}
