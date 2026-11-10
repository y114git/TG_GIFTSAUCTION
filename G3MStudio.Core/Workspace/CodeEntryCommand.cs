using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Adds an empty code entry with the given name (Import GML makes one for a file that names no entry). Undo takes the entry and its locals out again.</summary>
public sealed class CodeEntryCommand(string entry) : EditCommand
{
    public const string Name = "codeEntry";

    private GameMakerCode? _created;
    private GameMakerCodeLocals? _createdLocals;

    public override string Kind => Name;

    public override string Editor => "code";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["entry"] = entry };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("CodeEntries", entry), new("Strings", null)];

    public static CodeEntryCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["entry"]);

    public override void Apply(GameMakerData data)
    {
        if (!ResourceListCommand.IsValidName(entry)) throw new InvalidOperationException($"'{entry}' cannot be a code entry name: letters, digits and underscores, not starting with a digit.");
        if (data.Code.ByName(entry) is not null) throw new InvalidOperationException($"A code entry named '{entry}' exists already.");
        _created = GameMakerCode.CreateEmptyEntry(data, entry);
        _createdLocals = data.CodeLocals?.FirstOrDefault(l => l.Name == _created.Name);
    }

    public override void Revert(GameMakerData data)
    {
        if (_created is null) throw new InvalidOperationException("The edit was never applied.");
        data.Code.Remove(_created);
        if (_createdLocals is not null) data.CodeLocals?.Remove(_createdLocals);
    }
}
