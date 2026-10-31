using G3MLib.Analyzer.Decompiler;
using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;

namespace G3MStudio.Core.Workspace;

public static class CodeText
{
    /// <summary>GML of one code entry, decompiled from the model. Hold <see cref="Workspace.Lock"/> for reading when called off the UI thread.</summary>
    public static string Decompile(GameMakerData data, string entry) => Decompile(data, entry, new GlobalDecompileContext(data));

    /// <summary>
    /// Same, with a context made once for the data file. Building it costs far more than one entry (it reads every function and variable),
    /// so a search over thousands of entries shares one.
    /// </summary>
    public static string Decompile(GameMakerData data, string entry, GlobalDecompileContext context)
    {
        var code = data.Code.ByName(entry) ?? throw new InvalidOperationException($"Code entry '{entry}' was not found.");
        return new DecompileContext(context, code, data.ToolInfo.DecompilerSettings).DecompileToString();
    }
}
