using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;

namespace G3MLib.DataFile;

/// <summary>The three counters of the VARI chunk header.</summary>
public readonly record struct VariableCounters(uint VarCount1, uint VarCount2, uint MaxLocalVarCount);

/// <summary>
/// Rebuilds metadata that other tools often leave out of date: code locals and the variable counters of the VARI header.
/// </summary>
public static class DataRepair
{
    /// <summary>
    /// Builds the code locals of a code entry from its instructions: "arguments" first, then every local variable in order of first use.
    /// The result is not added to the data file. Adds the string "arguments" to the string table when the data has none.
    /// </summary>
    public static GameMakerCodeLocals BuildCodeLocals(GameMakerData data, GameMakerCode entry)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(entry);
        bool modern = data.IsVersionAtLeast(2, 3);
        GameMakerCodeLocals locals = new() { Name = entry.Name };
        GameMakerString arguments = data.Strings.MakeString("arguments", out int argumentsId);
        locals.Locals.Add(new GameMakerCodeLocals.LocalVar { Name = arguments, Index = modern ? (uint)argumentsId : 0 });
        HashSet<GameMakerVariable> seen = new(ReferenceEqualityComparer.Instance);
        uint position = 1;
        foreach (GameMakerInstruction instruction in entry.Instructions)
        {
            if (instruction.ValueVariable is { InstanceType: GameMakerInstruction.InstanceType.Local } variable && seen.Add(variable))
            {
                locals.Locals.Add(new GameMakerCodeLocals.LocalVar { Name = variable.Name, Index = modern ? (uint)variable.NameStringID : position });
                position++;
            }
        }
        return locals;
    }

    /// <summary>
    /// Adds code locals to every top-level code entry that has none. Child entries and entries flagged as having no locals are left alone.
    /// </summary>
    /// <returns>The number of entries that got code locals.</returns>
    public static int RestoreMissingCodeLocals(GameMakerData data, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.CodeLocals is null || data.Code is null)
        {
            return 0;
        }
        HashSet<string> present = new(data.CodeLocals.Select(l => l?.Name?.Content).Where(n => n is not null)!, StringComparer.Ordinal);
        int restored = 0;
        for (int i = 0; i < data.Code.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameMakerCode code = data.Code[i];
            if (code?.Name?.Content is string name && code.ParentEntry is null && !code.WeirdLocalFlag && present.Add(name))
            {
                data.CodeLocals.Add(BuildCodeLocals(data, code));
                restored++;
            }
            progress?.Report(new BatchProgress(i + 1, data.Code.Count, code?.Name?.Content ?? string.Empty));
        }
        return restored;
    }

    /// <summary>
    /// Computes the VARI counters from the variables and code locals: one more than the highest id of the variables that are neither local nor built in,
    /// and the largest local count. Bytecode 15 files that keep two different counts get the highest global id and the highest self id.
    /// </summary>
    /// <remarks>
    /// Checked against the counters of Undertale, Deltarune chapter 1, Pizza Tower and the 2022 and 2026 fixtures (all equal); the bytecode 15 case is not checked against a real file.
    /// </remarks>
    public static VariableCounters RecomputeVariableCounters(GameMakerData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        const int builtin = (int)GameMakerInstruction.InstanceType.Builtin;
        static uint Next(IEnumerable<GameMakerVariable> variables) => variables.Aggregate(0u, (highest, v) => Math.Max(highest, (uint)Math.Max(v.VarID + 1, 0)));
        IEnumerable<GameMakerVariable> shared = (data.Variables ?? []).Where(v => v is not null && v.InstanceType != GameMakerInstruction.InstanceType.Local && v.VarID != builtin);
        uint count1, count2;
        if (data.DifferentVarCounts && !data.IsVersionAtLeast(2, 3))
        {
            count1 = Next(shared.Where(v => v.InstanceType == GameMakerInstruction.InstanceType.Global));
            count2 = Next(shared.Where(v => v.InstanceType == GameMakerInstruction.InstanceType.Self));
        }
        else
        {
            count1 = count2 = Next(shared);
        }
        uint maxLocals = (uint)Math.Max(1, (data.CodeLocals ?? []).Select(l => l?.Locals.Count ?? 0).DefaultIfEmpty(0).Max());
        return new VariableCounters(count1, count2, maxLocals);
    }

    /// <summary>Writes <see cref="RecomputeVariableCounters"/> into the VARI header.</summary>
    public static void ApplyVariableCounters(GameMakerData data)
    {
        VariableCounters counters = RecomputeVariableCounters(data);
        data.VarCount1 = counters.VarCount1;
        data.VarCount2 = counters.VarCount2;
        data.MaxLocalVarCount = counters.MaxLocalVarCount;
    }
}
