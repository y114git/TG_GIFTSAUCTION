using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// An edit that knows its inverse and can be rebuilt from <see cref="Args"/>, so the journal can replay it after a crash (Q52).
/// </summary>
public abstract class EditCommand
{
    public DateTimeOffset Time { get; } = DateTimeOffset.Now;

    /// <summary>Stable name written to the journal.</summary>
    public abstract string Kind { get; }

    /// <summary>Editor the history list groups the command under.</summary>
    public abstract string Editor { get; }

    public abstract IReadOnlyDictionary<string, string> Args { get; }

    /// <summary>Project resources this edit changes, so autosave writes only those. Null means unknown: the whole project is refreshed.</summary>
    public virtual IReadOnlyList<ProjectTouch>? Touches => null;

    public abstract void Apply(GameMakerData data);

    public abstract void Revert(GameMakerData data);

    public static EditCommand Create(string kind, IReadOnlyDictionary<string, string> args) => kind switch
    {
        RenameResourceCommand.Name => new RenameResourceCommand(args["type"], args["old"], args["new"]),
        EditCodeCommand.Name => new EditCodeCommand(args["entry"], args["text"]),
        SetPropertyCommand.Name => new SetPropertyCommand(args["type"], args["name"], args["path"], args["text"]),
        RoomInstanceCommand.Name => RoomInstanceCommand.FromArgs(args),
        GroupCommand.Name => GroupCommand.FromArgs(args),
        RoomLayerCommand.Name => RoomLayerCommand.FromArgs(args),
        RoomCreationOrderCommand.Name => RoomCreationOrderCommand.FromArgs(args),
        PathPointsCommand.Name => PathPointsCommand.FromArgs(args),
        ImportMaskCommand.Name => ImportMaskCommand.FromArgs(args),
        EditStringCommand.Name => EditStringCommand.FromArgs(args),
        TimelineMomentCommand.Name => TimelineMomentCommand.FromArgs(args),
        FontGlyphCommand.Name => FontGlyphCommand.FromArgs(args),
        ParticleEmitterCommand.Name => ParticleEmitterCommand.FromArgs(args),
        SequenceTrackCommand.Name => SequenceTrackCommand.FromArgs(args),
        TimelineActionCommand.Name => TimelineActionCommand.FromArgs(args),
        RoomTilesCommand.Name => RoomTilesCommand.FromArgs(args),
        RoomElementCommand.Name => RoomElementCommand.FromArgs(args),
        RecalcMaskCommand.Name => RecalcMaskCommand.FromArgs(args),
        FrameListCommand.Name => new FrameListCommand(args["sprite"], args["operation"], int.Parse(args["frame"], System.Globalization.CultureInfo.InvariantCulture)),
        ReplaceFrameCommand.Name => new ReplaceFrameCommand(args["sprite"], int.Parse(args["frame"], System.Globalization.CultureInfo.InvariantCulture), Convert.FromBase64String(args["picture"])),
        _ => throw new InvalidDataException($"Unknown edit command '{kind}'."),
    };
}

/// <summary>A project resource an edit changes: a G3MLib modding resource type and a name, or null for the whole type file.</summary>
public sealed record ProjectTouch(string Type, string? Name);

/// <summary>Renames an asset and every name that embeds it, through G3MLib's rename service.</summary>
public sealed class RenameResourceCommand(string type, string oldName, string newName) : EditCommand
{
    public const string Name = "rename";

    private RenameChangeSet? _changes;

    public override string Kind => Name;

    public override string Editor => "explorer";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["old"] = oldName, ["new"] = newName };

    private IReadOnlyList<ProjectTouch>? _touches;

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public override void Apply(GameMakerData data)
    {
        var resource = ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(data)?.OfType<GameMakerNamedResource>().FirstOrDefault(r => ResourceTypes.NameOf(r) == oldName)
            ?? throw new InvalidOperationException($"'{oldName}' was not found in {type}.");
        // The index is built per rename (about 160 ms on a benchmark game) so it never goes stale after other edits.
        var index = ReferenceIndex.Build(data);
        _changes = RenameService.RenameResource(data, resource, newName, index);
        _touches = ProjectTouches(_changes, index.FindUsages(resource), index);
    }

    public override void Revert(GameMakerData data) => _changes?.Undo();

    /// <summary>
    /// The project resources a rename changes: whatever holds a changed name (the resource, its code entries) and whatever refers to the
    /// resource or to its name (rooms, objects, code). Each is listed under its new and its old name so the old folder is removed.
    /// Null (refresh everything) when an owner has no project type.
    /// </summary>
    private static IReadOnlyList<ProjectTouch>? ProjectTouches(RenameChangeSet changes, IEnumerable<Reference> resourceUses, ReferenceIndex index)
    {
        var owners = resourceUses.Concat(changes.Uses).Concat(changes.SelectMany(c => index.FindUsages(c.String))).Select(r => r.Owner)
            .Distinct(ReferenceEqualityComparer.Instance).ToList();
        var touches = new HashSet<ProjectTouch> { new("Strings", null) };
        foreach (var owner in owners)
        {
            var types = ProjectTypes(owner);
            if (types is null) return null;
            var now = ReferenceIndex.NameOf(owner);
            var names = new List<string?> { now };
            names.AddRange(changes.Where(c => c.NewValue == now).Select(c => c.OldValue));
            foreach (var type in types)
                foreach (var name in WholeFile.Contains(type) ? [null] : names)
                    touches.Add(new ProjectTouch(type, name));
        }

        return [.. touches];
    }

    // Types whose project files hold the whole list or section, so a name does not select anything.
    internal static readonly HashSet<string> WholeFile = ["Strings", "Functions", "Variables", "GeneralInfo", "Options"];

    internal static string[]? ProjectTypes(object owner) => owner switch
    {
        GameMakerSprite => ["Sprites"],
        GameMakerBackground => ["Backgrounds", "Tilesets"],
        GameMakerFont => ["Fonts"],
        GameMakerSound => ["Sounds"],
        GameMakerPath => ["Paths"],
        GameMakerShader => ["Shaders"],
        GameMakerTimeline => ["Timelines"],
        GameMakerGameObject => ["GameObjects"],
        GameMakerAnimationCurve => ["AnimationCurves"],
        GameMakerParticleSystem => ["ParticleSystems"],
        GameMakerParticleSystemEmitter => ["ParticleSystemEmitters"],
        GameMakerSequence => ["Sequences"],
        GameMakerRoom => ["Rooms"],
        GameMakerExtension => ["Extensions"],
        GameMakerScript => ["Scripts"],
        GameMakerCode or GameMakerCodeLocals => ["CodeEntries"],
        GameMakerFunction => ["Functions"],
        GameMakerVariable => ["Variables"],
        GameMakerString => ["Strings"],
        GameMakerGeneralInfo => ["GeneralInfo"],
        GameMakerOptions => ["Options"],
        _ => null,
    };
}

/// <summary>Replaces the GML of a code entry. Undo puts back the instructions and the code, script and function lists as they were.</summary>
public sealed class EditCodeCommand(string entry, string text) : EditCommand
{
    public const string Name = "code";

    private Snapshot? _before;

    public override string Kind => Name;

    public override string Editor => "code";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["entry"] = entry, ["text"] = text };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } =
        [new("CodeEntries", entry), new("Strings", null), new("Variables", null), new("Functions", null)];

    public override void Apply(GameMakerData data)
    {
        var code = data.Code.ByName(entry) ?? throw new InvalidOperationException($"Code entry '{entry}' was not found.");
        var before = new Snapshot(data, code);
        var group = new CodeImportGroup(data);
        group.QueueReplace(code, text);
        var result = group.Import(throwOnFailedCompile: false);
        if (!result.Successful)
        {
            before.Restore();
            throw new CodeCompileException(entry, [.. result.Errors.Select(e => e.Diagnostic)], result.PrintAllErrors(true));
        }

        _before = before;
    }

    public override void Revert(GameMakerData data) => _before?.Restore();

    private sealed class Snapshot
    {
        private readonly GameMakerData _data;
        private readonly List<GameMakerCode> _codeList, _children;
        private readonly List<GameMakerScript> _scripts;
        private readonly List<GameMakerFunction> _functions;
        private readonly GameMakerCode _root;
        private readonly List<(GameMakerCode Code, List<GameMakerInstruction> Instructions, uint Length, uint Offset, uint Locals, ushort Arguments)> _entries;

        public Snapshot(GameMakerData data, GameMakerCode root)
        {
            _data = data;
            _root = root;
            _codeList = [.. data.Code];
            _scripts = [.. data.Scripts];
            _functions = [.. data.Functions];
            _children = [.. root.ChildEntries];
            _entries = [.. _children.Prepend(root).Select(c => (c, new List<GameMakerInstruction>(c.Instructions), c.Length, c.Offset, c.LocalsCount, c.ArgumentsCount))];
        }

        public void Restore()
        {
            Refill(_data.Code, _codeList);
            Refill(_data.Scripts, _scripts);
            Refill(_data.Functions, _functions);
            _root.ChildEntries = [.. _children];
            foreach (var (code, instructions, length, offset, locals, arguments) in _entries)
            {
                code.Instructions.Clear();
                code.Instructions.AddRange(instructions);
                (code.Length, code.Offset, code.LocalsCount, code.ArgumentsCount) = (length, offset, locals, arguments);
            }
        }

        private static void Refill<T>(IList<T> list, List<T> items)
        {
            list.Clear();
            foreach (var item in items) list.Add(item);
        }
    }
}

/// <summary>Sets one property of a resource (or of the general info or options) from text. Undo puts back the old value, the old string table entry included.</summary>
public sealed class SetPropertyCommand(string type, string name, string path, string text) : EditCommand
{
    public const string Name = "property";

    private object? _before;
    private IReadOnlyList<ProjectTouch>? _touches;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["path"] = path, ["text"] = text };

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public override void Apply(GameMakerData data)
    {
        var root = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        _before = PropertyModel.ReadRaw(root, path);
        try
        {
            PropertyModel.Set(data, root, path, text);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            throw new InvalidOperationException($"'{text}' does not fit {path}.", e);
        }

        var types = RenameResourceCommand.ProjectTypes(root);
        _touches = types is null ? null : [.. types.Select(t => new ProjectTouch(t, RenameResourceCommand.WholeFile.Contains(t) ? null : name)), new ProjectTouch("Strings", null)];
    }

    public override void Revert(GameMakerData data)
    {
        if (PropertyModel.FindRoot(data, type, name) is { } root) PropertyModel.WriteRaw(root, path, _before);
    }
}
