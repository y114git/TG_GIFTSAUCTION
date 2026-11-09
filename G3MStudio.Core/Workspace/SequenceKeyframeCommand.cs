using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds, splits or deletes a keyframe of one track of a sequence. The keyframe types differ per track kind, so they are copied by their public
/// properties; an added keyframe copies the shape (channels and values) of the keyframe before it, so a track with no keyframe cannot get one here.
/// Undo puts the list back with the very same objects.
/// </summary>
public sealed class SequenceKeyframeCommand(string sequence, string track, string operation, int index, float frame) : EditCommand
{
    public const string Name = "sequenceKeyframe";
    public const string Add = "add";
    public const string Split = "split";
    public const string Delete = "delete";

    private object?[]? _before;
    private (object Item, float Length)? _lengthBefore;

    public override string Kind => Name;

    public override string Editor => "sequences";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sequence"] = sequence,
        ["track"] = track,
        ["operation"] = operation,
        ["index"] = index.ToString(CultureInfo.InvariantCulture),
        ["frame"] = frame.ToString("R", CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sequences", sequence)];

    public override bool ChangesNames => false;

    public static SequenceKeyframeCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sequence"], a["track"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), float.Parse(a["frame"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        var owner = data.Sequences?.FirstOrDefault(q => q.Name.Content == sequence) ?? throw new InvalidOperationException($"Sequence '{sequence}' does not exist.");
        var list = KeyframeReflection.ListOf(owner, track) ?? throw new InvalidOperationException($"Track '{track}' of '{sequence}' has no keyframes.");
        _before = [.. list.Cast<object?>()];
        _lengthBefore = null;
        switch (operation)
        {
            case Delete:
                if (index < 0 || index >= list.Count) throw new InvalidOperationException($"The track has no keyframe {index}.");
                list.RemoveAt(index);
                break;
            case Add:
                KeyframeReflection.AddAt(list, frame);
                break;
            case Split:
                _lengthBefore = KeyframeReflection.Split(list, index, frame);
                break;
            default:
                throw new InvalidDataException($"Unknown keyframe operation '{operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_before is null) throw new InvalidOperationException("The edit was never applied.");
        var owner = data.Sequences?.FirstOrDefault(q => q.Name.Content == sequence) ?? throw new InvalidOperationException($"Sequence '{sequence}' does not exist.");
        var list = KeyframeReflection.ListOf(owner, track) ?? throw new InvalidOperationException($"Track '{track}' of '{sequence}' has no keyframes.");
        list.Clear();
        foreach (var item in _before) list.Add(item);
        if (_lengthBefore is { } kept) KeyframeReflection.Set(kept.Item, "Length", kept.Length);
    }
}

/// <summary>Snapping for the dope sheet.</summary>
public static class SequenceSnap
{
    /// <summary>The key moved to the nearest of <paramref name="targets"/> within <paramref name="reach"/> frames, else rounded to a whole frame.</summary>
    public static float Snap(float key, IEnumerable<float> targets, float reach)
    {
        var best = targets.Select(t => (Target: t, Gap: Math.Abs(t - key))).Where(t => t.Gap <= reach).OrderBy(t => t.Gap).Select(t => (float?)t.Target).FirstOrDefault();
        return best ?? MathF.Round(key);
    }
}

/// <summary>Reads, writes and copies keyframes of any data type by their public properties, since each track kind has its own generic keyframe type.</summary>
internal static partial class KeyframeReflection
{
    internal static void AddAt(IList list, float frame)
    {
        if (list.Count == 0) throw new InvalidOperationException("An empty track has no keyframe to copy the shape from.");
        var items = list.Cast<object>().ToList();
        if (items.Any(k => Get(k, "Key") <= frame && frame < Get(k, "Key") + Math.Max(1, Get(k, "Length")))) throw new InvalidOperationException($"Frame {frame} already holds a keyframe.");
        var model = items.Where(k => Get(k, "Key") < frame).OrderBy(k => Get(k, "Key")).LastOrDefault() ?? items.OrderBy(k => Get(k, "Key")).First();
        var created = CloneItem(model);
        Set(created, "Key", frame);
        Set(created, "Length", 1);
        var at = items.Count(k => Get(k, "Key") < frame);
        list.Insert(at, created);
    }

    internal static (object Item, float Length) Split(IList list, int index, float frame)
    {
        if (index < 0 || index >= list.Count) throw new InvalidOperationException($"The track has no keyframe {index}.");
        var item = list[index] ?? throw new InvalidOperationException($"The track has no keyframe {index}.");
        float key = Get(item, "Key"), length = Get(item, "Length");
        if (frame <= key || frame >= key + length) throw new InvalidOperationException($"Frame {frame} is not inside the keyframe.");
        var second = CloneItem(item);
        Set(second, "Key", frame);
        Set(second, "Length", key + length - frame);
        Set(item, "Length", frame - key);
        list.Insert(index + 1, second);
        return (item, length);
    }

    [GeneratedRegex(@"Tracks\[(\d+)\]")]
    private static partial Regex Step();

    /// <summary>The keyframe list of the track a timeline row path names (<c>Tracks[0].Tracks[1].Keyframes.List</c>), or null for a track without keyframes or a path that is not a track.</summary>
    internal static IList? ListOf(GameMakerSequence owner, string path)
    {
        var steps = Step().Matches(path);
        if (steps.Count == 0 || owner.Tracks is null) return null;
        GameMakerSequence.Track? at = null;
        IList<GameMakerSequence.Track> tracks = owner.Tracks;
        foreach (Match step in steps)
        {
            var i = int.Parse(step.Groups[1].Value, CultureInfo.InvariantCulture);
            if (tracks is null || i >= tracks.Count) return null;
            at = tracks[i];
            tracks = at.Tracks;
        }

        var keyframes = at?.Keyframes;
        return keyframes?.GetType().GetProperty("List")?.GetValue(keyframes) as IList;
    }

    internal static float Get(object item, string name) => (float)(item.GetType().GetProperty(name)?.GetValue(item) ?? 0f);

    internal static void Set(object item, string name, float value) => item.GetType().GetProperty(name)?.SetValue(item, value);

    /// <summary>A new object of the same type with the same property values; lists and the types nested in a sequence are copied, resources and strings are shared.</summary>
    internal static object CloneItem(object item)
    {
        var copy = Activator.CreateInstance(item.GetType()) ?? throw new InvalidOperationException($"Cannot create a {item.GetType().Name}.");
        foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null && p.SetMethod?.IsPublic == true))
        {
            var value = property.GetValue(item);
            property.SetValue(copy, value switch
            {
                IList list when value is not Array && list.GetType().IsGenericType => CloneList(list),
                not null when IsNested(value.GetType()) => CloneItem(value),
                _ => value,
            });
        }

        return copy;
    }

    internal static IList CloneList(IList list)
    {
        var copy = Activator.CreateInstance(list.GetType()) as IList ?? throw new InvalidOperationException($"Cannot create a {list.GetType().Name}.");
        foreach (var item in list) copy.Add(item is not null && IsNested(item.GetType()) ? CloneItem(item) : item);
        return copy;
    }

    internal static bool IsNested(Type type)
    {
        for (var at = type; at is not null; at = (at.IsGenericType ? at.GetGenericTypeDefinition() : at).DeclaringType)
            if ((at.IsGenericType ? at.GetGenericTypeDefinition() : at).DeclaringType == typeof(GameMakerSequence)) return true;
        return false;
    }
}
