using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Sets the three counters in the header of the variables chunk: instance variables, global variables and the most local variables one script uses (UTMT-AST-10).
/// Undo puts the old values back.
/// </summary>
public sealed class VariableCountsCommand(uint instance, uint global, uint locals) : EditCommand
{
    public const string Name = "varCounts";

    private (uint Instance, uint Global, uint Locals) _old;

    public override string Kind => Name;

    public override string Editor => "variables";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["instance"] = instance.ToString(CultureInfo.InvariantCulture),
        ["global"] = global.ToString(CultureInfo.InvariantCulture),
        ["locals"] = locals.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Variables", null)];

    public static VariableCountsCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(uint.Parse(a["instance"], CultureInfo.InvariantCulture), uint.Parse(a["global"], CultureInfo.InvariantCulture), uint.Parse(a["locals"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        _old = (data.VarCount1, data.VarCount2, data.MaxLocalVarCount);
        (data.VarCount1, data.VarCount2, data.MaxLocalVarCount) = (instance, global, locals);
    }

    public override void Revert(GameMakerData data) => (data.VarCount1, data.VarCount2, data.MaxLocalVarCount) = _old;
}
