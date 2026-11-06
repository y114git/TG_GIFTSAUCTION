using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Gives a room (instance id 0) or one of its instances an empty creation code entry, named like GameMaker names it, so the code tab can open it.
/// An entry of that name that already exists is linked instead of created. Undo unlinks it and removes an entry this edit created.
/// </summary>
public sealed class RoomCreationCodeCommand(string room, uint instanceId) : EditCommand
{
    public const string Name = "roomCreationCode";

    private GameMakerCode? _created;
    private GameMakerCodeLocals? _createdLocals;
    private Action? _unlink;

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["room"] = room,
        ["instance"] = instanceId.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", room), new("CodeEntries", null), new("Strings", null)];

    public static RoomCreationCodeCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["room"], uint.Parse(a["instance"], CultureInfo.InvariantCulture));

    /// <summary>The name GameMaker gives the creation code entry of a room (instance 0) or of an instance.</summary>
    public static string CodeName(string room, uint instanceId) => instanceId == 0 ? $"gml_Room_{room}_Create" : $"gml_RoomCC_{room}_{instanceId}_Create";

    /// <summary>The creation code entry of the room or instance, or null when it has none.</summary>
    public static GameMakerCode? CodeOf(GameMakerRoom owner, uint instanceId) =>
        instanceId == 0 ? owner.CreationCodeId : RoomInstanceCommand.Lists(owner).SelectMany(l => l).FirstOrDefault(i => i.InstanceID == instanceId)?.CreationCode;

    public override void Apply(GameMakerData data)
    {
        var owner = data.Rooms.FirstOrDefault(r => r.Name.Content == room) ?? throw new InvalidOperationException($"Room '{room}' does not exist.");
        if (CodeOf(owner, instanceId) is not null) throw new InvalidOperationException(instanceId == 0 ? $"Room '{room}' already has creation code." : $"Instance {instanceId} already has creation code.");
        var instance = instanceId == 0 ? null : RoomInstanceCommand.Lists(owner).SelectMany(l => l).FirstOrDefault(i => i.InstanceID == instanceId) ?? throw new InvalidOperationException($"Room '{room}' has no instance {instanceId}.");
        var name = CodeName(room, instanceId);
        var existing = data.Code.FirstOrDefault(c => c.Name.Content == name);
        _created = existing is null ? GameMakerCode.CreateEmptyEntry(data, name) : null;
        _createdLocals = _created is null ? null : data.CodeLocals?.FirstOrDefault(l => l.Name == _created.Name);
        var code = existing ?? _created;
        if (instance is null)
        {
            owner.CreationCodeId = code;
            _unlink = () => owner.CreationCodeId = null;
        }
        else
        {
            instance.CreationCode = code;
            _unlink = () => instance.CreationCode = null;
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_unlink is null) throw new InvalidOperationException("The edit was never applied.");
        _unlink();
        if (_created is not null) data.Code.Remove(_created);
        if (_createdLocals is not null) data.CodeLocals?.Remove(_createdLocals);
    }
}
