using G3MLib.DataFile;
using G3MLib.DataFile.Util;

namespace G3MStudio.Core.Workspace;

/// <summary>What is worth telling the user right after a data file opens (UTMT-FILE-06, FILE-07, FILE-08): a game compiled with the YoYo Compiler, the GameMaker debugger flag, textures in a format the program cannot show, parts the reader could not model.</summary>
public static class LoadChecks
{
    public const string Yyc = "warnings.yyc", Debugger = "warnings.debugger", UnknownTextures = "warnings.unknownTextures", MayLoseData = "warnings.mayLoseData";

    /// <summary>Language key and a count (0 when the text has none), in the order they are shown.</summary>
    public static IReadOnlyList<(string Key, int Count)> Find(GameMakerData data)
    {
        var found = new List<(string, int)>();
        // A YYC game keeps its code in the executable: objects exist but no code entry does.
        if ((data.Code is null || data.Code.Count == 0) && data.GameObjects?.Count > 0) found.Add((Yyc, 0));
        if (data.GeneralInfo is { IsDebuggerDisabled: false }) found.Add((Debugger, 0));
        var unknown = data.EmbeddedTextures?.Count(t => t.TextureData?.Image is { Format: GMImage.ImageFormat.Unknown }) ?? 0;
        if (unknown > 0) found.Add((UnknownTextures, unknown));
        // The reader skipped or could not model some of the file (UTMT-FILE-04): saving may not write all of it back.
        if (data.MayLoseDataOnSave) found.Add((MayLoseData, data.LoadWarnings.Count));
        return found;
    }
}
