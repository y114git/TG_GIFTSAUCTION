using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Removes one top-level track, with everything under it, from a sequence, or adds an empty group track. Undo puts the very same track back or takes the new one out.
/// Only a group track can be added: it has no keyframes, while every other kind needs keyframes of a type that depends on its model.
/// </summary>
#pragma warning disable CA1001 // the track belongs to the sequence or is put back into it; the model disposes it
public sealed class SequenceTrackCommand : EditCommand
{
    public const string Name = "sequenceTrack";
    public const string Remove = "remove";
    public const string AddGroup = "addGroup";

    private readonly string _sequence;
    private readonly string _operation;
    private readonly int _index;
    private readonly string _trackName;
    private GameMakerSequence.Track? _track;

    // index: the track to remove. trackName: the name of a new group track.
    public SequenceTrackCommand(string sequence, string operation, int index = -1, string trackName = "") => (_sequence, _operation, _index, _trackName) = (sequence, operation, index, trackName);

    public override string Kind => Name;

    public override string Editor => "sequences";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["sequence"] = _sequence,
        ["operation"] = _operation,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["name"] = _trackName,
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sequences", _sequence)];

    public static SequenceTrackCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sequence"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), a["name"]);

    private GameMakerSequence Find(GameMakerData data) => data.Sequences?.FirstOrDefault(q => q.Name.Content == _sequence) ?? throw new InvalidOperationException($"Sequence '{_sequence}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var tracks = Find(data).Tracks;
        if (_operation == AddGroup)
        {
            _track = new GameMakerSequence.Track
            {
                ModelName = data.Strings.MakeString("GMGroupTrack", out _),
                Name = data.Strings.MakeString(_trackName, out _),
                BuiltinName = 0,
                Traits = GameMakerSequence.Track.TrackTraits.None,
                Tags = [],
                OwnedResources = [],
                Tracks = [],
#pragma warning disable CS8625 // a group track has no keyframes in the data file; the library reads and writes null for it
                Keyframes = null,
#pragma warning restore CS8625
            };
            tracks.Add(_track);
            return;
        }

        if (_operation != Remove) throw new InvalidDataException($"Unknown sequence operation '{_operation}'.");
        if (_index < 0 || _index >= tracks.Count) throw new InvalidOperationException($"Sequence '{_sequence}' has no track {_index}.");
        _track = tracks[_index];
        tracks.RemoveAt(_index);
    }

    public override void Revert(GameMakerData data)
    {
        var tracks = Find(data).Tracks;
        if (_track is null) throw new InvalidOperationException("The edit was never applied.");
        if (_operation == AddGroup) tracks.Remove(_track);
        else tracks.Insert(_index, _track);
    }
}
#pragma warning restore CA1001
