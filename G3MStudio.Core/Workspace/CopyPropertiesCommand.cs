using System.Text.Json;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Copies the plain properties of one resource onto another. Duplicate no longer makes it (the library clone copies everything), but a journal written
/// before that, for crash recovery or replay, still holds it, so it stays readable. Undo puts the old values back.
/// </summary>
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
