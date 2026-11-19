using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// An edit that knows its inverse and can be rebuilt from <see cref="Args"/>, so the journal can replay it after a crash.
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

    /// <summary>True when applying or reverting this edit leaves the reverse reference index valid, so the history keeps the cached one (a rename changes only the text of strings).</summary>
    public virtual bool KeepsReferences => false;

    /// <summary>False when this edit cannot add, remove, move or rename an entry of the Explorer, so the Explorer need not read every name again.</summary>
    public virtual bool ChangesNames => true;

    /// <summary>Memory this command keeps for undo, by estimate: the journal arguments, plus any picture or audio it holds. Read once, when the history records it.</summary>
    public virtual long RetainedBytes => Args.Sum(a => 2L * a.Value.Length);

    public abstract void Apply(GameMakerData data);

    public abstract void Revert(GameMakerData data);

    /// <summary>Every kind a command class writes to the journal, found from the classes' Name constants.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [
        .. typeof(EditCommand).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EditCommand)))
            .Select(t => t.GetField("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)?.GetRawConstantValue() as string ?? "")
            .Order(StringComparer.Ordinal),
    ];

    public static EditCommand Create(string kind, IReadOnlyDictionary<string, string> args) => kind switch
    {
        RenameResourceCommand.Name => new RenameResourceCommand(args["type"], args["old"], args["new"], args.GetValueOrDefault("strings") != "0"),
        EditCodeCommand.Name => new EditCodeCommand(args["entry"], args["text"]),
        SetPropertyCommand.Name => new SetPropertyCommand(args["type"], args["name"], args["path"], args["text"]),
        ListEntryCommand.Name => ListEntryCommand.FromArgs(args),
        ExtensionListCommand.Name => ExtensionListCommand.FromArgs(args),
        RoomInstanceCommand.Name => RoomInstanceCommand.FromArgs(args),
        RoomTileCommand.Name => RoomTileCommand.FromArgs(args),
        RoomCreationCodeCommand.Name => RoomCreationCodeCommand.FromArgs(args),
        CodeEntryCommand.Name => CodeEntryCommand.FromArgs(args),
        GroupCommand.Name => GroupCommand.FromArgs(args),
        RoomLayerCommand.Name => RoomLayerCommand.FromArgs(args),
        RoomCreationOrderCommand.Name => RoomCreationOrderCommand.FromArgs(args),
        PathPointsCommand.Name => PathPointsCommand.FromArgs(args),
        PathEditCommand.Name => PathEditCommand.FromArgs(args),
        TileAnimationCommand.Name => TileAnimationCommand.FromArgs(args),
        TextureGroupMoveCommand.Name => TextureGroupMoveCommand.FromArgs(args),
        AudioGroupMoveCommand.Name => AudioGroupMoveCommand.FromArgs(args),
        ImportMaskCommand.Name => ImportMaskCommand.FromArgs(args),
        EditStringCommand.Name => EditStringCommand.FromArgs(args),
        GroupListCommand.Name => GroupListCommand.FromArgs(args),
        StringAddCommand.Name => StringAddCommand.FromArgs(args),
        StringDeleteCommand.Name => StringDeleteCommand.FromArgs(args),
        TimelineMomentCommand.Name => TimelineMomentCommand.FromArgs(args),
        ObjectEventCommand.Name => ObjectEventCommand.FromArgs(args),
        ObjectEventCopyCommand.Name => ObjectEventCopyCommand.FromArgs(args),
        ObjectEventOrderCommand.Name => ObjectEventOrderCommand.FromArgs(args),
        ObjectEventRetypeCommand.Name => ObjectEventRetypeCommand.FromArgs(args),
        TimelineShiftCommand.Name => TimelineShiftCommand.FromArgs(args),
        SequenceEventCommand.Name => SequenceEventCommand.FromArgs(args),
        TimelineMomentCopyCommand.Name => TimelineMomentCopyCommand.FromArgs(args),
        ObjectParentCommand.Name => ObjectParentCommand.FromArgs(args),
        ResourceListCommand.Name => ResourceListCommand.FromArgs(args),
        ResourceMoveCommand.Name => ResourceMoveCommand.FromArgs(args),
        VariableCountsCommand.Name => VariableCountsCommand.FromArgs(args),
        ReplaceAudioCommand.Name => ReplaceAudioCommand.FromArgs(args),
        ReplacePageCommand.Name => ReplacePageCommand.FromArgs(args),
        ReplaceItemCommand.Name => ReplaceItemCommand.FromArgs(args),
        RoomOrderCommand.Name => RoomOrderCommand.FromArgs(args),
        CopyPropertiesCommand.Name => CopyPropertiesCommand.FromArgs(args),
        CloneResourceCommand.Name => CloneResourceCommand.FromArgs(args),
        UnsharePicturesCommand.Name => UnsharePicturesCommand.FromArgs(args),
        ClearReferencesCommand.Name => ClearReferencesCommand.FromArgs(args),
        CopyResourcesCommand.Name => CopyResourcesCommand.FromArgs(args),
        FrameOwnPictureCommand.Name => FrameOwnPictureCommand.FromArgs(args),
        TextureFitCommand.Name => TextureFitCommand.FromArgs(args),
        PageResizeCommand.Name => PageResizeCommand.FromArgs(args),
        FramesFitCommand.Name => FramesFitCommand.FromArgs(args),
        FontSheetShiftCommand.Name => FontSheetShiftCommand.FromArgs(args),
        FramesImportCommand.Name => FramesImportCommand.FromArgs(args),
        FontGlyphCommand.Name => FontGlyphCommand.FromArgs(args),
        FontKerningCommand.Name => FontKerningCommand.FromArgs(args),
        EntryScriptsCommand.Name => EntryScriptsCommand.FromArgs(args),
        EditAssemblyCommand.Name => EditAssemblyCommand.FromArgs(args),
        PhysicsVertexCommand.Name => PhysicsVertexCommand.FromArgs(args),
        ParticleEmitterCommand.Name => ParticleEmitterCommand.FromArgs(args),
        SequenceTrackCommand.Name => SequenceTrackCommand.FromArgs(args),
        SequenceKeyframeCommand.Name => SequenceKeyframeCommand.FromArgs(args),
        CurvePointCommand.Name => CurvePointCommand.FromArgs(args),
        TimelineActionCommand.Name => TimelineActionCommand.FromArgs(args),
        RoomTilesCommand.Name => RoomTilesCommand.FromArgs(args),
        UiLayerCommand.Name => UiLayerCommand.FromArgs(args),
        RoomElementCommand.Name => RoomElementCommand.FromArgs(args),
        RecalcMaskCommand.Name => RecalcMaskCommand.FromArgs(args),
        SpriteMaskCommand.Name => SpriteMaskCommand.FromArgs(args),
        MaskResizeCommand.Name => MaskResizeCommand.FromArgs(args),
        SpriteOriginCommand.Name => SpriteOriginCommand.FromArgs(args),
        NineSliceCommand.Name => NineSliceCommand.FromArgs(args),
        FontReplaceCommand.Name => FontReplaceCommand.FromArgs(args),
        ImageEditCommand.Name => ImageEditCommand.FromArgs(args),
        FrameReorderCommand.Name => FrameReorderCommand.FromArgs(args),
        IncludedPictureCommand.Name => IncludedPictureCommand.FromArgs(args),
        IncludedTextCommand.Name => IncludedTextCommand.FromArgs(args),
        IncludedAudioCommand.Name => IncludedAudioCommand.FromArgs(args),
        ReplaceEmbeddedAudioCommand.Name => ReplaceEmbeddedAudioCommand.FromArgs(args),
        FrameListCommand.Name => new FrameListCommand(args["sprite"], args["operation"], int.Parse(args["frame"], System.Globalization.CultureInfo.InvariantCulture), args.ContainsKey("force")),
        ReplaceFrameCommand.Name => new ReplaceFrameCommand(args["sprite"], int.Parse(args["frame"], System.Globalization.CultureInfo.InvariantCulture), Convert.FromBase64String(args["picture"])),
        _ => throw new InvalidDataException($"Unknown edit command '{kind}'."),
    };
}

/// <summary>A project resource an edit changes: a G3MLib modding resource type and a name, or null for the whole type file.</summary>
public sealed record ProjectTouch(string Type, string? Name);

/// <summary>
/// Renames an asset and every name that embeds it, through G3MLib's rename service. The service renames the string entry of the name, so code string literals
/// equal to the old name follow it; with <paramref name="renameStrings"/> off those literals are first moved to an entry of their own and keep the old text.
/// </summary>
public sealed partial class RenameResourceCommand(string type, string oldName, string newName, bool renameStrings = true) : EditCommand
{
    public const string Name = "rename";

    private RenameChangeSet? _changes;
    private GameMakerString? _literals;
    private readonly List<(GameMakerInstruction Instruction, GameMakerResourceById<GameMakerString, GameMakerChunkSTRG> Before)> _detached = [];

    public override string Kind => Name;

    public override string Editor => "explorer";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["old"] = oldName, ["new"] = newName }
        .Concat(renameStrings ? [] : [new KeyValuePair<string, string>("strings", "0")]).ToDictionary();

    private IReadOnlyList<ProjectTouch>? _touches;

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    // Moved literals leave the cached index pointing at the old entry.
    public override bool KeepsReferences => _detached.Count == 0;

    public override void Apply(GameMakerData data)
    {
        var resource = ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(data)?.OfType<GameMakerNamedResource>().FirstOrDefault(r => ResourceTypes.NameOf(r) == oldName)
            ?? throw new InvalidOperationException($"'{oldName}' was not found in {type}.");
        // The history drops the cached index after every edit but a rename, so it is never stale here.
        var index = ReferenceCache.Of(data).Get(data);
        _detached.Clear();
        if (!renameStrings) DetachLiterals(data, resource, index);
        try
        {
            _changes = RenameService.RenameResource(data, resource, newName, index);
        }
        catch
        {
            Reattach(data);
            throw;
        }

        _touches = ProjectTouches(_changes, index.FindUsages(resource), index);
    }

    public override void Revert(GameMakerData data)
    {
        _changes?.Undo();
        Reattach(data);
    }

    /// <summary>Points every code string literal that equals the old name at a new entry with the same text, so the rename of the name entry leaves them alone.</summary>
    private void DetachLiterals(GameMakerData data, GameMakerNamedResource resource, ReferenceIndex index)
    {
        var literals = index.FindUsages(resource.Name).Where(u => u.InstructionOffset >= 0 && u.Holder is GameMakerInstruction { ValueString: not null }).Select(u => (GameMakerInstruction)u.Holder).Distinct().ToList();
        if (literals.Count == 0) return;
        _literals = data.Strings.MakeString(oldName, true);
        foreach (var instruction in literals)
        {
            _detached.Add((instruction, instruction.ValueString));
            instruction.ValueString = new(_literals);
        }
    }

    private void Reattach(GameMakerData data)
    {
        if (_literals is null) return;
        foreach (var (instruction, before) in _detached) instruction.ValueString = before;
        data.Strings.Remove(_literals);
        _literals = null;
    }
}

/// <summary>
/// Replaces the GML of a code entry. The macros other entries declared (see <see cref="MacroRegistry"/>) are expanded in it, and the ones it declares are remembered for them.
/// Undo puts back the instructions and the code, script and function lists as they were.
/// </summary>
public sealed class EditCodeCommand(string entry, string text) : EditCommand
{
    public const string Name = "code";

    private Snapshot? _before;
    private (IReadOnlyDictionary<string, string>? Macros, bool Set) _macros;

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
        // The compile adds the macros this text declares itself; the ones of the other entries come from the registry, and the text's own declaration wins.
        var scan = new CompileGroup(data, group.GlobalContext);
        scan.AddMacros(text);
        var own = scan.Macros;
        var registry = MacroRegistry.Of(data);
        foreach (var (name, value) in registry.Table(entry))
            if (!own.ContainsKey(name)) group.Macros[name] = value;
        var result = group.Import(throwOnFailedCompile: false);
        if (!result.Successful)
        {
            before.Restore();
            throw CodeCompileException.From(entry, result);
        }

        _before = before;
        _macros = (registry.Set(entry, own), true);
    }

    public override void Revert(GameMakerData data)
    {
        _before?.Restore();
        if (_macros.Set) MacroRegistry.Of(data).Set(entry, _macros.Macros);
        _macros = default;
    }

    internal sealed class Snapshot
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
    private bool _addedStrings;
    private IReadOnlyList<ProjectTouch>? _touches;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["path"] = path, ["text"] = text };

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public override bool ChangesNames => _addedStrings || type == "strings" || path.Contains("Name", StringComparison.Ordinal); // a string entry is listed under its text

    public override void Apply(GameMakerData data)
    {
        var root = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        _before = PropertyModel.ReadRaw(root, path);
        var strings = data.Strings.Count;
        try
        {
            PropertyModel.Set(data, root, path, text);
            _addedStrings = data.Strings.Count != strings;
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
