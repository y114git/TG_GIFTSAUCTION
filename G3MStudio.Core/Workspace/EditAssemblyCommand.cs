using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;

namespace G3MStudio.Core.Workspace;

/// <summary>Replaces the instructions of a code entry with edited VM assembly text (UTMT-CODE-05). A failed assembly leaves the entry as it was.</summary>
public sealed class EditAssemblyCommand(string entry, string text) : EditCommand
{
    public const string Name = "assembly";

    private EditCodeCommand.Snapshot? _before;

    public override string Kind => Name;

    public override string Editor => "code";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["entry"] = entry, ["text"] = text };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("CodeEntries", entry), new("Strings", null), new("Variables", null), new("Functions", null)];

    public static EditAssemblyCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["entry"], a["text"]);

    public override void Apply(GameMakerData data)
    {
        var code = data.Code.ByName(entry) ?? throw new InvalidOperationException($"Code entry '{entry}' was not found.");
        var before = new EditCodeCommand.Snapshot(data, code);
        try
        {
            var instructions = Assembler.Assemble(text, data, action => action(), code);
            code.Instructions.Clear();
            foreach (var instruction in instructions) code.Instructions.Add(instruction);
            code.Length = (uint)instructions.Sum(i => i.CalculateInstructionSize()) * 4;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            before.Restore();
            throw new InvalidOperationException($"Could not assemble '{entry}': {e.Message}", e);
        }

        _before = before;
    }

    public override void Revert(GameMakerData data) => _before?.Restore();
}
