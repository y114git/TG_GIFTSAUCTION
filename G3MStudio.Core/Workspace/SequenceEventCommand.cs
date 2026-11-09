using System.Collections;
using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001 // the keyframe belongs to the sequence or is put back into it; the model disposes it
/// <summary>
/// Adds a moment or a broadcast message keyframe to a sequence at a frame, removes one, or replaces its text (one string per line). A moment holds the
/// names of functions the sequence calls at that frame; a message holds the broadcast strings. Undo puts the very same keyframe back.
/// </summary>
public sealed class SequenceEventCommand(string sequence, string list, string operation, int index, float frame = 0, string text = "") : EditCommand
{
    public const string Name = "sequenceEvent";
    public const string Moments = "Moments";
    public const string Messages = "BroadcastMessages";
    public const string Add = "add";
    public const string Delete = "delete";
    public const string SetText = "setText";

    private object? _item;
    private int _at;
    private string[] _oldText = [];

    public override string Kind => Name;

    public override string Editor => "sequences";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sequence"] = sequence,
        ["list"] = list,
        ["operation"] = operation,
        ["index"] = index.ToString(CultureInfo.InvariantCulture),
        ["frame"] = frame.ToString("R", CultureInfo.InvariantCulture),
        ["text"] = text,
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sequences", sequence), new("Strings", null)];

    public override bool ChangesNames => false;

    public static SequenceEventCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sequence"], a["list"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), float.Parse(a["frame"], CultureInfo.InvariantCulture), a["text"]);

    private IList Find(GameMakerData data)
    {
        var owner = data.Sequences?.FirstOrDefault(q => q.Name.Content == sequence) ?? throw new InvalidOperationException($"Sequence '{sequence}' does not exist.");
        return list switch
        {
            Moments => owner.Moments,
            Messages => owner.BroadcastMessages,
            _ => throw new InvalidDataException($"Unknown sequence list '{list}'."),
        };
    }

    public override void Apply(GameMakerData data)
    {
        var items = Find(data);
        if (operation == Add)
        {
            if (items.Cast<object>().Any(k => KeyframeReflection.Get(k, "Key") == frame)) throw new InvalidOperationException($"Frame {frame} already holds an entry.");
            _item = SequenceEventStrings.Make(list == Moments, data, text, frame);
            _at = items.Cast<object>().Count(k => KeyframeReflection.Get(k, "Key") < frame);
            items.Insert(_at, _item);
            return;
        }

        if (index < 0 || index >= items.Count) throw new InvalidOperationException($"The sequence has no entry {index}.");
        _at = index;
        _item = items[index];
        if (operation == Delete)
        {
            items.RemoveAt(index);
        }
        else if (operation == SetText && _item is not null && SequenceEventStrings.Of(_item) is { } strings)
        {
            _oldText = [.. strings.Select(s => s.Content ?? "")];
            SequenceEventStrings.Replace(strings, data, SequenceEventStrings.Lines(text));
        }
        else
        {
            throw new InvalidDataException($"Unknown sequence operation '{operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        var items = Find(data);
        if (_item is null) throw new InvalidOperationException("The edit was never applied.");
        if (operation == Add) items.Remove(_item);
        else if (operation == Delete) items.Insert(_at, _item);
        else if (SequenceEventStrings.Of(_item) is { } strings) SequenceEventStrings.Replace(strings, data, _oldText);
    }
}
#pragma warning restore CA1001
