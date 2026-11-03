using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Adds a vertex to the physics shape of an object, moves one, or removes one (UTMT-OBJ-03). The vertex count the file stores is the list length, so it cannot drift.</summary>
public sealed class PhysicsVertexCommand(string obj, string operation, int index, float x = 0, float y = 0) : EditCommand
{
    public const string Name = "physicsVertex";
    public const string Add = "add";
    public const string Set = "set";
    public const string Remove = "remove";

    private (float X, float Y) _before;

    public override string Kind => Name;

    public override string Editor => "objects";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["object"] = obj,
        ["operation"] = operation,
        ["index"] = index.ToString(CultureInfo.InvariantCulture),
        ["x"] = x.ToString("R", CultureInfo.InvariantCulture),
        ["y"] = y.ToString("R", CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Objects", obj)];

    public static PhysicsVertexCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["object"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), float.Parse(a["x"], CultureInfo.InvariantCulture), float.Parse(a["y"], CultureInfo.InvariantCulture));

    private GameMakerObservableList<GameMakerGameObject.GameMakerPhysicsVertex> Vertices(GameMakerData data) =>
        (data.GameObjects.FirstOrDefault(o => o.Name.Content == obj) ?? throw new InvalidOperationException($"Object '{obj}' does not exist.")).PhysicsVertices;

    public override void Apply(GameMakerData data)
    {
        var list = Vertices(data);
        if (operation == Add)
        {
            list.Add(new GameMakerGameObject.GameMakerPhysicsVertex { X = x, Y = y });
            return;
        }

        if (index < 0 || index >= list.Count) throw new InvalidOperationException($"Object '{obj}' has no vertex {index}.");
        _before = (list[index].X, list[index].Y);
        if (operation == Set) (list[index].X, list[index].Y) = (x, y);
        else if (operation == Remove) list.RemoveAt(index);
        else throw new InvalidDataException($"Unknown vertex operation '{operation}'.");
    }

    public override void Revert(GameMakerData data)
    {
        var list = Vertices(data);
        if (operation == Add) list.RemoveAt(list.Count - 1);
        else if (operation == Set) (list[index].X, list[index].Y) = _before;
        else list.Insert(index, new GameMakerGameObject.GameMakerPhysicsVertex { X = _before.X, Y = _before.Y });
    }
}
