using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>How a function of the Functions list can be deleted.</summary>
/// <param name="Kind">OwnScript: a script resource of its own, deleted with its code entry. Inside: declared in a larger code entry, taken out of its source. Refused: no safe delete.</param>
/// <param name="Entry">The code entry that defines the function (OwnScript) or holds its declaration (Inside).</param>
/// <param name="Script">The script resource of an OwnScript function.</param>
/// <param name="Reason">Why a Refused function stays: the end of the key explorer.deleteFunctionNo.</param>
public sealed record FunctionDeletePlan(FunctionDeletePlan.Way Kind, string Entry = "", string Script = "", string Reason = "")
{
    public enum Way { OwnScript, Inside, Refused }
}

/// <summary>Finds the code that defines a function: a global function has the code entry named like it (gml_Script_name), a function of a larger script has that entry as a child.</summary>
public static class FunctionDeletion
{
    public static FunctionDeletePlan Plan(GameMakerData data, string function)
    {
        var code = data.Code.ByName(function);
        if (code is null) return new(FunctionDeletePlan.Way.Refused, Reason: "Code");
        if (code.ParentEntry?.Name?.Content is { } parent) return new(FunctionDeletePlan.Way.Inside, parent);
        var script = data.Scripts.FirstOrDefault(s => ReferenceEquals(s.Code, code));
        return script is null ? new(FunctionDeletePlan.Way.Refused, Reason: "Script") : new(FunctionDeletePlan.Way.OwnScript, function, script.Name?.Content ?? "");
    }
}
