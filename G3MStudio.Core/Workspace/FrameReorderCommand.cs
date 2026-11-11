using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001 // the entries belong to the sprite or are put back into it; the model disposes them
/// <summary>
/// Puts the frames of a sprite in a new order. <c>order[n]</c> is the old index of the frame that becomes frame n, so one command moves a single frame or a whole selection.
/// Per-frame collision masks follow their frames. Undo puts the old order back.
/// </summary>
public sealed class FrameReorderCommand(string sprite, IReadOnlyList<int> order) : EditCommand
{
    public const string Name = "frameReorder";

    private GameMakerSprite.TextureEntry[] _frames = [];
    private GameMakerSprite.MaskEntry[] _masks = [];

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["order"] = string.Join(',', order.Select(i => i.ToString(CultureInfo.InvariantCulture))),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sprites", sprite)];

    public static FrameReorderCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sprite"], [.. a["order"].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture))]);

    /// <summary>The order that moves the frames <paramref name="moved"/> (ascending) in front of the frame that is at <paramref name="to"/> now, keeping their order; <paramref name="to"/> == count puts them last.</summary>
    public static int[] MoveTo(int count, IReadOnlyCollection<int> moved, int to)
    {
        var before = Enumerable.Range(0, Math.Min(Math.Max(to, 0), count)).Where(i => !moved.Contains(i));
        var after = Enumerable.Range(Math.Min(Math.Max(to, 0), count), Math.Max(0, count - Math.Max(to, 0))).Where(i => !moved.Contains(i));
        return [.. before.Concat(moved.Order()).Concat(after)];
    }

    private GameMakerSprite Find(GameMakerData data) => data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        if (order.Count != target.Textures.Count || order.Order().Where((v, i) => v != i).Any()) throw new InvalidOperationException($"The new frame order does not match the {target.Textures.Count} frames of '{sprite}'.");
        _frames = [.. target.Textures];
        _masks = [.. target.CollisionMasks];
        Put(target.Textures, [.. order.Select(i => _frames[i])]);
        if (_masks.Length == _frames.Length) Put(target.CollisionMasks, [.. order.Select(i => _masks[i])]);
    }

    public override void Revert(GameMakerData data)
    {
        var target = Find(data);
        Put(target.Textures, _frames);
        if (_masks.Length == _frames.Length) Put(target.CollisionMasks, _masks);
    }

    private static void Put<T>(IList<T> list, IReadOnlyList<T> items)
    {
        list.Clear();
        foreach (var item in items) list.Add(item);
    }
}
#pragma warning restore CA1001
