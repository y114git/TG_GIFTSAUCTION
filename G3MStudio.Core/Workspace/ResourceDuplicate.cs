using System.Text.Json;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Copies the plain properties (the sprite, flags, depth, parent, physics) of one resource onto another. Undo puts the old values back.</summary>
public sealed class CopyPropertiesCommand(string type, string source, string target, IReadOnlyList<string> paths) : EditCommand
{
    public const string Name = "copyProperties";

    private readonly List<(string Path, object? Value)> _before = [];

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["type"] = type,
        ["source"] = source,
        ["target"] = target,
        ["paths"] = JsonSerializer.Serialize(paths),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new(type, target)];

    public static CopyPropertiesCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["type"], a["source"], a["target"], JsonSerializer.Deserialize<List<string>>(a["paths"]) ?? []);

    public override void Apply(GameMakerData data)
    {
        var from = PropertyModel.FindRoot(data, type, source) ?? throw new InvalidOperationException($"'{source}' was not found in {type}.");
        var to = PropertyModel.FindRoot(data, type, target) ?? throw new InvalidOperationException($"'{target}' was not found in {type}.");
        _before.Clear();
        foreach (var path in paths)
        {
            try
            {
                var old = PropertyModel.ReadRaw(to, path);
                PropertyModel.WriteRaw(to, path, PropertyModel.ReadRaw(from, path));
                _before.Add((path, old));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.Reflection.TargetInvocationException or NotSupportedException)
            {
                // A property the library does not let us set stays as the new resource made it.
            }
        }
    }

    public override void Revert(GameMakerData data)
    {
        var to = PropertyModel.FindRoot(data, type, target) ?? throw new InvalidOperationException($"'{target}' was not found in {type}.");
        foreach (var (path, value) in Enumerable.Reverse(_before)) PropertyModel.WriteRaw(to, path, value);
    }
}

/// <summary>Builds the one undoable edit that copies an object (properties and events with their code) or a script (its code) under a new name.</summary>
public static class ResourceDuplicate
{
    /// <summary>The types that can be duplicated.</summary>
    public static readonly string[] Types = ["objects", "scripts"];

    public static EditCommand Build(GameMakerData data, string type, string source, string copy)
    {
        List<EditCommand> steps = [new ResourceListCommand(type, ResourceListCommand.New, copy)];
        var context = new G3MLib.DataFile.Decompiler.GlobalDecompileContext(data);
        switch (type)
        {
            case "scripts":
                var script = data.Scripts.FirstOrDefault(s => s.Name.Content == source) ?? throw new InvalidOperationException($"Script '{source}' does not exist.");
                // A script of GameMaker 2.3 and later keeps its code as part of a larger entry; that cannot be copied alone.
                if (script.Code is { ParentEntry: not null }) throw new InvalidOperationException($"The code of '{source}' is part of a larger code entry, so it cannot be copied on its own.");
                if (script.Code?.Name?.Content is { Length: > 0 } code) steps.Add(new EditCodeCommand("gml_Script_" + copy, CodeText.Decompile(data, code, context)));
                break;
            case "objects":
                var obj = data.GameObjects.FirstOrDefault(o => o.Name.Content == source) ?? throw new InvalidOperationException($"Object '{source}' does not exist.");
                var paths = PropertyModel.Describe(obj)
                    .Where(r => r.Depth == 0 && !r.ReadOnly && r.Path != "Name" && (r.Target is not null || r.Kind is PropertyKind.Text or PropertyKind.Bool or PropertyKind.Whole or PropertyKind.Real or PropertyKind.Enum or PropertyKind.GmString))
                    .Select(r => r.Path)
                    .ToList();
                // The sprite, parent and mask are links the property list only shows, but the model lets us set them.
                paths.AddRange(["Sprite", "ParentId", "TextureMaskId"]);
                steps.Add(new CopyPropertiesCommand("objects", source, copy, paths));
                for (var kind = 0; kind < obj.Events.Count; kind++)
                {
                    foreach (var evt in obj.Events[kind])
                    {
                        if (evt.Actions.Count > 1) throw new InvalidOperationException($"{source} has an event with {evt.Actions.Count} actions; only events with one action can be copied.");
                        steps.Add(new ObjectEventCommand(copy, ObjectEventCommand.Add, (uint)kind, evt.EventSubtype));
                        if (evt.Actions.FirstOrDefault()?.CodeId?.Name?.Content is { Length: > 0 } eventCode)
                            steps.Add(new EditCodeCommand(ObjectEventCommand.CodeName(copy, (uint)kind, evt.EventSubtype), CodeText.Decompile(data, eventCode, context)));
                    }
                }

                break;
            default:
                throw new InvalidOperationException($"A {type} entry cannot be duplicated.");
        }

        return new GroupCommand(steps);
    }
}
