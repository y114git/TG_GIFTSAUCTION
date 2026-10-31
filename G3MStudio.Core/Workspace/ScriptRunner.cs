using G3MLib.DataFile;
using G3MLib.DataFile.Util;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// What a .csx script can call. This is a small part of what UndertaleModTool offers: the data (<c>Data</c>), the file paths and the message calls.
/// Scripts that need dialogs, the decompiler helpers or the UI will not compile here; the error says which name is missing.
/// </summary>
#pragma warning disable CA1822 // scripts call these as plain names, so they have to be instance members of the globals object
public sealed class StudioScriptGlobals(GameMakerData data, string filePath, string scriptPath, Action<string> message, Action<string> warning)
{
    public GameMakerData Data { get; } = data;

    public string FilePath { get; } = filePath;

    public string ScriptPath { get; } = scriptPath;

    public void ScriptMessage(string text) => message(text);

    public void ScriptWarning(string text) => warning(text);

    /// <summary>Stops the script with an error, like UndertaleModTool's ScriptError.</summary>
    public void ScriptError(string error, string title = "Error", bool setConsoleText = true) => throw new ScriptFailedException(string.IsNullOrEmpty(title) ? error : title + ": " + error);

    /// <summary>There is no dialog here: a question is answered yes, and the script's own message is shown.</summary>
    public bool ScriptQuestion(string text)
    {
        message(text);
        return true;
    }

    public bool RunUMTScript(string path) => throw new ScriptFailedException("Running another script from a script is not supported.");
}

#pragma warning restore CA1822

public sealed class ScriptFailedException(string message) : Exception(message);

/// <summary>Runs a .csx script on the open data file as one bulk change: it cannot be undone and Revert to Saved is the way back.</summary>
public static class ScriptRunner
{
    /// <summary>Runs the script. Returns null when it finished, or the error text (compile error, ScriptError or an exception in the script).</summary>
    public static async Task<string?> RunAsync(Workspace workspace, string scriptPath, Action<string> message, Action<string> warning)
    {
        var text = await File.ReadAllTextAsync(scriptPath);
        var globals = new StudioScriptGlobals(workspace.Data, workspace.Path, scriptPath, message, warning);
        string? error = null;
        await workspace.ApplyBulkAsync(() =>
        {
            try
            {
                var options = ScriptingUtil.CreateDefaultScriptOptions().WithFilePath(scriptPath).WithFileEncoding(System.Text.Encoding.UTF8);
                // The change runs on a pool thread without a synchronization context, so waiting here cannot deadlock; the write lock must stay held until the script ends.
                CSharpScript.EvaluateAsync(text, options, globals, typeof(StudioScriptGlobals)).GetAwaiter().GetResult();
            }
            catch (ScriptFailedException e)
            {
                error = e.Message;
            }
            catch (CompilationErrorException e)
            {
                error = string.Join(Environment.NewLine, e.Diagnostics.Take(5).Select(d => d.ToString()));
            }
            catch (Exception e)
            {
                // Whatever the script threw: it is reported, and the data file may be half changed (Revert to Saved).
                error = ScriptingUtil.PrettifyException(e);
            }
        });
        return error;
    }
}
