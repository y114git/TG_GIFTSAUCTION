using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>A new resource name is not a plain GML name, or another resource has it; the owner may use it anyway (it is still a safe file name).</summary>
public sealed class UnusualNameException(string message, bool taken = false) : InvalidOperationException(message)
{
    public bool Taken { get; } = taken;
}

/// <summary>
/// Creates an empty resource (object, script, path or timeline) or deletes one. Undo puts the lists back as they were. A delete leaves the code of
/// the resource out too (the events of an object, the code of a script); the caller checks first that nothing uses it.
/// </summary>
public sealed partial class ResourceListCommand : EditCommand
{
    public const string Name = "resourceList";
    public const string New = "new";
    public const string Delete = "delete";

    /// <summary>Types that can be created from nothing.</summary>
    public static readonly string[] Creatable = ["objects", "scripts", "paths", "timelines", "sounds", "rooms", "sprites", "shaders", "fonts", "sequences", "animationCurves", "particleSystems", "extensions"];

    /// <summary>Types that can be deleted.</summary>
    public static readonly string[] Deletable = ["objects", "sprites", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "rooms", "animationCurves", "sequences", "particleSystems", "tileSets", "extensions"];

    private readonly string _type, _operation, _name;
    private List<GameMakerCode>? _code;
    private List<GameMakerCodeLocals>? _locals;
    private List<GameMakerEmbeddedAudio>? _audio;
    private GameMakerSprite? _newSprite;
    private object? _item;
    private List<GameMakerGameObject.Event>? _shifted;
    private int _at = -1;

    private readonly bool _force;
    private List<(IList<GameMakerGameObject.Event> Live, List<GameMakerGameObject.Event> Before)>? _collisionLists;
    private List<(IList<GameMakerGlobalInit> Live, List<GameMakerGlobalInit> Before)>? _inits;

    // force: the owner chose to go on where Studio would refuse: a new name that is unusual or taken (still a safe file name), or a delete that removes the collision events aimed at the object.
    public ResourceListCommand(string type, string operation, string name, bool force = false) => (_type, _operation, _name, _force) = (type, operation, name, force);

    public override string Kind => Name;

    public override string Editor => _type;

    public override IReadOnlyDictionary<string, string> Args => _force
        ? new Dictionary<string, string> { ["type"] = _type, ["operation"] = _operation, ["name"] = _name, ["force"] = "1" }
        : new Dictionary<string, string> { ["type"] = _type, ["operation"] = _operation, ["name"] = _name };

    public override IReadOnlyList<ProjectTouch>? Touches => [new(_type, _name), new("CodeEntries", null), new("Strings", null)];

    public static ResourceListCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["operation"], a["name"], a.ContainsKey("force"));

    /// <summary>True when the text can name a resource: letters, digits and underscores, not starting with a digit.</summary>
    public static bool IsValidName(string name) => ResourceName().IsMatch(name);

    /// <summary>True when an entry of any resource type already has the name (GameMaker wants one name per asset).</summary>
    public static bool NameTaken(GameMakerData data, string name) =>
        ResourceTypes.All.Where(t => !t.IsData && t.Key != "general").Any(t => t.Items(data)?.Any(i => ResourceTypes.NameOf(i) == name) == true);

    public override void Apply(GameMakerData data)
    {
        var (codeBefore, localsBefore, audioBefore) = (new List<GameMakerCode>(data.Code), data.CodeLocals is { } l ? new List<GameMakerCodeLocals>(l) : null, new List<GameMakerEmbeddedAudio>(data.EmbeddedAudio));
        if (_operation == New) Create(data);
        else if (_operation == Delete) Remove(data);
        else throw new InvalidDataException($"Unknown resource operation '{_operation}'.");
        (_code, _locals, _audio) = (codeBefore, localsBefore, audioBefore);
    }

    private void CheckName(GameMakerData data)
    {
        var unusual = !IsValidName(_name);
        if (unusual && !CopyPackage.IsSafeName(_name)) throw new InvalidOperationException($"'{_name}' cannot be a name: it is empty, too long, or has characters that a file name cannot hold (slash, colon and others), and resources are stored as files.");
        var taken = NameTaken(data, _name) || _type == "scripts" && data.Code.ByName("gml_Script_" + _name) is not null;
        if (_force || !unusual && !taken) return;
        throw new UnusualNameException(taken
            ? $"A resource or code entry named '{_name}' exists already. Allow risky actions in Settings > Advanced to use the name twice anyway."
            : $"'{_name}' is not a plain name: letters, digits and underscores, not starting with a digit. Code cannot use other names. Allow risky actions in Settings > Advanced to use it anyway.", taken);
    }

    private void Create(GameMakerData data)
    {
        if (!Creatable.Contains(_type)) throw new InvalidOperationException($"A new {_type} entry cannot be created here.");
        CheckName(data);
        if (ResourceTypes.All.First(t => t.Key == _type).Items(data) is null) throw new InvalidOperationException($"This data file has no {_type} section, so it cannot hold a new one.");
        var name = data.Strings.MakeString(_name, out _);
        switch (_type)
        {
            case "objects":
                _item = new GameMakerGameObject { Name = name };
                data.GameObjects.Add((GameMakerGameObject)_item);
                break;
            case "scripts":
                var script = new GameMakerScript { Name = name, Code = GameMakerCode.CreateEmptyEntry(data, "gml_Script_" + _name) };
                _item = script;
                data.Scripts.Add(script);
                break;
            case "paths":
                _item = new GameMakerPath { Name = name };
                data.Paths.Add((GameMakerPath)_item);
                break;
            case "timelines":
                _item = new GameMakerTimeline { Name = name };
                data.Timelines.Add((GameMakerTimeline)_item);
                break;
            case "sprites":
                _newSprite = SpriteTemplate.Create(data, _name);
                _item = _newSprite;
                break;
            case "shaders":
                _item = ShaderTemplate.Create(data, name);
                data.Shaders.Add((GameMakerShader)_item);
                break;
            case "rooms":
                _item = new GameMakerRoom { Name = name, Width = 1024, Height = 768 };
                data.Rooms.Add((GameMakerRoom)_item);
                break;
            case "fonts":
                _item = new GameMakerFont { Name = name, DisplayName = data.Strings.MakeString("Arial", out _), EmSize = 12, ScaleX = 1, ScaleY = 1, RangeStart = 32, RangeEnd = 127, AntiAliasing = 1, Charset = 1 };
                data.Fonts.Add((GameMakerFont)_item);
                break;
            case "sequences":
                _item = new GameMakerSequence { Name = name, Tracks = [], Moments = [], BroadcastMessages = [], FunctionIDs = [] };
                data.Sequences.Add((GameMakerSequence)_item);
                break;
            case "animationCurves":
                _item = new GameMakerAnimationCurve { Name = name, Channels = [] };
                data.AnimationCurves.Add((GameMakerAnimationCurve)_item);
                break;
            case "particleSystems":
                _item = new GameMakerParticleSystem { Name = name, Emitters = [] };
                data.ParticleSystems.Add((GameMakerParticleSystem)_item);
                break;
            case "extensions":
                _item = new GameMakerExtension { Name = name, FolderName = data.Strings.MakeString("", out _), ClassName = data.Strings.MakeString("", out _), Version = data.Strings.MakeString("1.0.0", out _), Files = [], Options = [] };
                data.Extensions.Add((GameMakerExtension)_item);
                break;
            default:
                // A sound starts as one second of silence (WAV) in its own embedded audio slot, like the library's own import makes it.
                var audio = new GameMakerEmbeddedAudio { Data = WavEdit.Silence(1) };
                data.EmbeddedAudio.Add(audio);
                var sound = new GameMakerSound
                {
                    Name = name,
                    File = data.Strings.MakeString(_name + ".wav"),
                    Type = data.Strings.MakeString(".wav"),
                    Volume = 1f,
                    Pitch = 1f,
                    Preload = true,
                    AudioLength = 1f,
                    Flags = GameMakerSound.AudioEntryFlags.Regular | GameMakerSound.AudioEntryFlags.IsEmbedded,
                    AudioFile = audio,
                    AudioID = data.EmbeddedAudio.Count - 1,
                };
                if (data.AudioGroups is { Count: > 0 })
                {
                    sound.AudioGroup = data.AudioGroups[0];
                    sound.GroupID = 0;
                }

                _item = sound;
                data.Sounds.Add(sound);
                break;
        }

        _at = ListOf(data).Count - 1;
    }

    private void Remove(GameMakerData data)
    {
        if (!Deletable.Contains(_type)) throw new InvalidOperationException($"A {_type} entry cannot be deleted here.");
        var list = ListOf(data);
        _at = list.FindIndex(i => ResourceTypes.NameOf(i) == _name);
        if (_at < 0) throw new InvalidOperationException($"'{_name}' was not found in {_type}.");
        _item = list[_at];
        if (_item is GameMakerGameObject owner)
        {
            // A collision event names its partner by position in the object list: the events after the removed entry move up by one, and an event that aims at it blocks the delete.
            var index = (uint)_at;
            DropCollisionsAimedAt(data, index);
            var collisions = data.GameObjects.SelectMany(o => o.Events[(int)EventType.Collision]).ToList();
            _shifted = [.. collisions.Where(e => e.EventSubtype > index)];
            foreach (var shifted in _shifted) shifted.EventSubtype--;
            foreach (var code in owner.Events.SelectMany(e => e).SelectMany(e => e.Actions).Select(a => a.CodeId).OfType<GameMakerCode>().ToList()) Drop(data, code);
        }
        else if (_item is GameMakerScript { Code: { } own })
        {
            // The start and end script lists point at the code of a script; an entry left behind would name code that is no longer in the file.
            _inits = [.. new[] { data.GlobalInitScripts, data.GameEndScripts }.OfType<IList<GameMakerGlobalInit>>().Select(l => (l, new List<GameMakerGlobalInit>(l)))];
            foreach (var (live, _) in _inits)
                foreach (var entry in live.Where(g => ReferenceEquals(g.Code, own)).ToList()) live.Remove(entry);
            Drop(data, own);
        }

        list.RemoveAt(_at);
        Sync(data, list);
    }

    // Events that aim at the removed object would point at the next object, or past the end of the list. Forced, they are removed with their code (undo puts the lists back).
    private void DropCollisionsAimedAt(GameMakerData data, uint index)
    {
        var aimed = data.GameObjects.Select(o => o.Events[(int)EventType.Collision]).Where(l => l.Any(e => e.EventSubtype == index)).ToList();
        if (aimed.Count == 0) return;
        if (!_force) throw new RiskyRefusalException("risky.collisionTitle", "risky.collision", $"'{_name}' is the partner of a collision event: remove those events first.");
        _collisionLists = [.. aimed.Select(l => (l, new List<GameMakerGameObject.Event>(l)))];
        foreach (var list in aimed)
            foreach (var doomed in list.Where(e => e.EventSubtype == index).ToList())
            {
                foreach (var code in doomed.Actions.Select(a => a.CodeId).OfType<GameMakerCode>().ToList()) Drop(data, code);
                list.Remove(doomed);
            }
    }

    private static void Drop(GameMakerData data, GameMakerCode code)
    {
        data.Code.Remove(code);
        if (data.CodeLocals is { } locals && locals.FirstOrDefault(x => x.Name == code.Name) is { } entry) locals.Remove(entry);
    }

    public override void Revert(GameMakerData data)
    {
        if (_item is null || _code is null) throw new InvalidOperationException("The edit was never applied.");
        var list = ListOf(data);
        if (_operation == New && _newSprite is { Textures: [{ Texture: { } item }, ..] }) SpriteTemplate.RemoveItem(data, item);

        if (_operation == New) list.Remove(_item);
        else
        {
            foreach (var shifted in _shifted ?? []) shifted.EventSubtype++;
            foreach (var (live, before) in _collisionLists ?? []) Refill(live, before);
            foreach (var (live, before) in _inits ?? []) Refill(live, before);
            list.Insert(_at, _item);
        }
        Sync(data, list);
        Refill(data.Code, _code);
        if (_audio is not null) Refill(data.EmbeddedAudio, _audio);
        if (_locals is not null && data.CodeLocals is { } locals) Refill(locals, _locals);
    }

    // The typed lists hold the entries; this view is a copy, so a change goes back through the typed list.
    private List<object> ListOf(GameMakerData data) => [.. (ResourceTypes.All.First(t => t.Key == _type).Items(data) ?? [])];

    private void Sync(GameMakerData data, List<object> list)
    {
        switch (_type)
        {
            case "objects": Replace(data.GameObjects, list); break;
            case "sprites": Replace(data.Sprites, list); break;
            case "sounds": Replace(data.Sounds, list); break;
            case "paths": Replace(data.Paths, list); break;
            case "scripts": Replace(data.Scripts, list); break;
            case "shaders": Replace(data.Shaders, list); break;
            case "fonts": Replace(data.Fonts, list); break;
            case "timelines": Replace(data.Timelines, list); break;
            case "rooms": Replace(data.Rooms, list); break;
            case "animationCurves": Replace(data.AnimationCurves, list); break;
            case "sequences": Replace(data.Sequences, list); break;
            case "particleSystems": Replace(data.ParticleSystems, list); break;
            case "tileSets": Replace(data.Backgrounds, list); break;
            case "extensions": Replace(data.Extensions, list); break;
            default: throw new InvalidOperationException($"Unknown type {_type}.");
        }
    }

    private static void Replace<T>(IList<T>? target, List<object> items)
    {
        if (target is null) return;
        target.Clear();
        foreach (var item in items) target.Add((T)item);
    }

    private static void Refill<T>(IList<T> list, List<T> items)
    {
        list.Clear();
        foreach (var item in items) list.Add(item);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ResourceName();
}
