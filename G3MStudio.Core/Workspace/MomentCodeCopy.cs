using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Decompiler;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>The copy-the-code path of <see cref="TimelineMomentCopyCommand"/>.</summary>
internal static class MomentCodeCopy
{
    /// <summary>
    /// Gives each action that runs a code entry a new entry with the same GML, compiled by Studio's compiler and named after the target timeline and step.
    /// Returns what undo needs to put the code lists back, or null when no action has code. A failed compile restores the file and throws.
    /// </summary>
    public static EditCodeCommand.Snapshot? Run(GameMakerData data, List<GameMakerGameObject.EventAction> actions, string from, string target, uint step)
    {
        var sources = new List<(GameMakerGameObject.EventAction Action, GameMakerCode Code, string Entry)>();
        foreach (var a in actions)
            if (a.CodeId is { } code && code.Name?.Content is { } entry) sources.Add((a, code, entry));
        if (sources.Count == 0) return null;
        var context = new GlobalDecompileContext(data);
        var before = new EditCodeCommand.Snapshot(data, sources[0].Code);
        var group = new CodeImportGroup(data);
        var names = new List<string>();
        foreach (var (_, _, entry) in sources)
        {
            var name = FreeName(data, names, $"gml_Timeline_{target}_{step}");
            names.Add(name);
            group.QueueReplace(name, CodeText.Decompile(data, entry, context));
        }

        var result = group.Import(throwOnFailedCompile: false);
        if (!result.Successful)
        {
            before.Restore();
            throw new CodeCompileException(from, [.. result.Errors.Select(e => e.Diagnostic)], result.PrintAllErrors(true));
        }

        foreach (var ((action, _, _), name) in sources.Zip(names)) action.CodeId = data.Code.ByName(name);
        return before;
    }

    private static string FreeName(GameMakerData data, List<string> taken, string baseName)
    {
        var name = baseName;
        for (var n = 2; taken.Contains(name) || data.Code.ByName(name) is not null; n++) name = baseName + "_" + n;
        return name;
    }
}
