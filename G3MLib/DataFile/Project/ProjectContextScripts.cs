using System.Collections.Generic;
using System.IO;
using G3MLib.DataFile.Scripting;
using G3MLib.DataFile.Util;

namespace G3MLib.DataFile.Project;

partial class ProjectContext
{
    /// <summary>
    /// Full path to currently-executing script, or <see langword="null"/> if no script is executing.
    /// </summary>
    private string _scriptPath = null;

    /// <summary>
    /// Throws if this context does not allow running scripts.
    /// </summary>
    private void InitializeScripting()
    {
        if (!AllowScripts)
        {
            throw new ProjectException("Attempted to run scripts, which has been disallowed");
        }
    }

    /// <summary>
    /// Runs a list of scripts based on their relative paths to the project context's root.
    /// </summary>
    private void RunScriptList(List<string> relativePathList)
    {
        if (relativePathList.Count == 0)
        {
            return;
        }
        InitializeScripting();
        foreach (string relativePath in relativePathList)
        {
            // Get path to script file
            string path = Path.GetFullPath(Path.Join(MainDirectory, relativePath));
            Paths.VerifyWithinDirectory(MainDirectory, path);

            ScriptRunResult result;
            try
            {
                _scriptPath = path;
                result = ScriptRunner.RunAsync(path, CreateScriptGlobals()).GetAwaiter().GetResult();
            }
            finally
            {
                _scriptPath = null;
            }
            if (!result.Success)
            {
                throw new ProjectException($"Script \"{relativePath}\" failed: {result.Error}");
            }
        }
    }

    /// <summary>
    /// Globals for a script started from this project: the project's data and host callbacks behind the shared script host.
    /// </summary>
    private ScriptGlobals CreateScriptGlobals() => new(Data, LoadDataPath)
    {
        Project = this,
        CanSave = CanSave,
        MainThreadAction = MainThreadAction,
        MessageHandler = ScriptMessageHandler,
        WarningHandler = ScriptWarningHandler,
        ScriptRootPath = MainDirectory
    };

    /// <summary>
    /// Executes all pre-import scripts, as defined in the project options.
    /// </summary>
    private void RunPreImportScripts()
    {
        RunScriptList(_mainOptions.PreImportScripts);
    }

    /// <summary>
    /// Executes all pre-asset import scripts, as defined in the project options.
    /// </summary>
    private void RunPreAssetImportScripts()
    {
        RunScriptList(_mainOptions.PreAssetImportScripts);
    }

    /// <summary>
    /// Executes all pre-import scripts, as defined in the project options.
    /// </summary>
    private void RunPostImportScripts()
    {
        RunScriptList(_mainOptions.PostImportScripts);
    }
}
