using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using G3MLib.Analyzer.Decompiler;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Decompiler;
using G3MLib.DataFile.Models;

namespace G3MLib.DataFile.Interop;

/// <summary>
/// Converts a GameMaker script resource (<c>.yy</c> plus its <c>.gml</c> file) to and from code in a data file.
/// </summary>
/// <remarks>
/// The compiler adds the variable and function entries the code needs. Scripts of data files before GameMaker 2.3 use the
/// <c>gml_Script_</c> code entry, newer ones the <c>gml_GlobalScript_</c> entry that holds the functions.
/// </remarks>
public static class YyScript
{
    /// <summary>
    /// Queues the script's <c>.gml</c> text on <paramref name="group"/>. The caller runs <see cref="CodeImportGroup.Import(bool)"/>.
    /// </summary>
    /// <returns>Script name.</returns>
    public static string Read(string yyPath, CodeImportGroup group)
    {
        JsonElement root = YyFile.Parse(yyPath);
        string name = root.Str("name") ?? throw new InvalidDataException($"{yyPath}: script has no name.");
        string gml = File.ReadAllText(Path.Combine(Path.GetDirectoryName(yyPath), name + ".gml"));
        string entry = EntryName(group.Data, name);
        group.QueueReplace(entry, gml);
        // GameMaker lists the script asset itself next to its functions; the group only creates the function entries.
        if (entry.StartsWith("gml_GlobalScript_", StringComparison.Ordinal) && group.Data.Code.ByName(entry) is { } code)
        {
            if (group.Data.Scripts.ByName(name) is { } script)
                script.Code = code;
            else
                group.Data.Scripts.Add(new() { Name = group.Data.Strings.MakeString(name), Code = code });
        }
        return name;
    }

    internal static string EntryName(GameMakerData data, string name) =>
        (data.IsVersionAtLeast(2, 3) ? "gml_GlobalScript_" : "gml_Script_") + name;

    /// <summary>
    /// Writes the script's decompiled source as <c>{folder}/{name}.gml</c> and its <c>{name}.yy</c>.
    /// </summary>
    /// <param name="parent">Folder name shown in the asset browser (LTS 2022), or the project name (LTS 2026).</param>
    public static void Write(string name, GameMakerData data, GlobalDecompileContext globalContext, string folder, YyFormat format, string parent = "Scripts")
    {
        GameMakerCode code = data.Code.ByName(EntryName(data, name)) ?? data.Scripts.ByName(name)?.Code
            ?? throw new InvalidOperationException($"No code entry for script \"{name}\".");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".gml"), new DecompileContext(globalContext, code, new DecompileSettings()).DecompileToString());

        JsonObject root = YyFile.Resource(format, "GMScript", name, "1.0", "v1");
        root["isDnD"] = false;
        root["isCompatibility"] = false;
        root["parent"] = format == YyFormat.Lts2026
            ? new JsonObject { ["name"] = parent, ["path"] = parent + ".yyp" }
            : new JsonObject { ["name"] = parent, ["path"] = $"folders/{parent}.yy" };
        if (format == YyFormat.Lts2022)
            root["tags"] = new JsonArray();
        YyFile.Write(Path.Combine(folder, name + ".yy"), root);
    }
}
