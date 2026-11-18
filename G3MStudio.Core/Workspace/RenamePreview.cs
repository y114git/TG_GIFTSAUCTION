using G3MLib.DataFile;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>What renaming a resource would change, found without changing anything: the names and strings that follow, the places that use the resource, or why it is refused.</summary>
public sealed record RenamePreview(string? Refusal, IReadOnlyList<string> Changes, int Uses)
{
    /// <summary>Plans the rename with the library's planner. <paramref name="index"/> adds the use count; it can be null.</summary>
    public static RenamePreview Of(GameMakerData data, string type, string oldName, string newName, ReferenceIndex? index)
    {
        var resource = ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(data)?.OfType<GameMakerNamedResource>().FirstOrDefault(r => ResourceTypes.NameOf(r) == oldName);
        if (resource is null) return new($"'{oldName}' was not found in {type}.", [], 0);
        var plan = RenameService.PlanRenames(data, [RenameRequest.ForResource(resource, newName)], index);
        if (plan.Conflicts.Count > 0) return new(plan.Conflicts[0].Message, [], 0);
        return new(null, [.. plan.Changes.Select(c => $"{c.OldValue} > {c.NewValue}")], plan.Uses.Count);
    }
}
