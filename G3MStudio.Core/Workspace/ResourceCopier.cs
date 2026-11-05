using G3MLib.DataFile;
using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>Copies resources from one open data file into another as a single undoable edit of the target. The source is only read.</summary>
public static class ResourceCopier
{
    /// <summary>Null when the target can take the copy; else why it cannot (it is saving, its file is read only, or it is the source).</summary>
    public static string? TargetRefusal(Workspace source, Workspace target)
    {
        if (ReferenceEquals(source, target)) return "The target is the source file; use Duplicate to copy inside one data file.";
        if (target.History.Frozen) return $"{target.Name} is being saved; try again when it is done.";
        try
        {
            if (File.GetAttributes(target.Path).HasFlag(FileAttributes.ReadOnly)) return $"{target.Name} is read only; the copy could not be saved.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"{target.Name} cannot be reached: {e.Message}";
        }

        return null;
    }

    /// <summary>The preview: what a copy of <paramref name="items"/> would do to the target.</summary>
    public static Task<CopyPlan> PlanAsync(Workspace source, Workspace target, IReadOnlyList<(string Type, string Name)> items, bool withDependencies,
        CollisionMode defaultMode, IReadOnlyDictionary<string, CollisionMode>? overrides, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            using (source.Lock.Read())
            using (target.Lock.Read())
                return CopyPlanner.Build(source.Data, target.Data, items, withDependencies, defaultMode, overrides, cancellationToken);
        }, cancellationToken);

    /// <summary>Exports the planned items from the source, makes them fit the target and returns the edit. Nothing in either file changes.</summary>
    public static Task<CopyResourcesCommand> BuildAsync(Workspace source, Workspace target, CopyPlan plan, IProgress<BatchProgress>? progress, CancellationToken cancellationToken, bool allowLarge = false) =>
        Task.Run(() =>
        {
            if (plan.Refusal is not null) throw new InvalidOperationException(plan.Refusal);
            if (!plan.CanApply) throw new InvalidOperationException("There is nothing to copy.");
            var folder = Directory.CreateTempSubdirectory("g3mstudio-copy-").FullName;
            try
            {
                var applied = plan.Applied.ToList();
                var firstFree = FirstFreeInstanceId(target);
                using (source.Lock.Read())
                {
                    var graph = CopyGraph.Build(source.Data, cancellationToken);
                    var selection = Select(source.Data, graph, applied, out var renames);
                    CopyPackage.Export(source.Data, source.Path, folder, selection, progress, cancellationToken);
                    var last = CopyPackage.Prepare(folder, renames, firstFree);
                    var package = CopyPackage.Zip(folder, allowLarge);
                    var steps = applied.Select(e => new CopyStep(e.Type, e.TargetName, e.Action == CopyAction.Replace)).ToList();
                    return new CopyResourcesCommand(package, CopyVersion.Of(source.Data), steps, last);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }, cancellationToken);

    /// <summary>Builds the edit and applies it to the target as one undo step. The edit's Progress and Cancellation work during the apply.</summary>
    public static async Task<CopyResourcesCommand> CopyAsync(Workspace source, Workspace target, CopyPlan plan, IProgress<BatchProgress>? progress, CancellationToken cancellationToken, bool allowLarge = false)
    {
        if (TargetRefusal(source, target) is { } refusal) throw new InvalidOperationException(refusal);
        var command = await BuildAsync(source, target, plan, progress, cancellationToken, allowLarge);
        command.Progress = progress;
        command.Cancellation = cancellationToken;
        await target.EditInBackground(command);
        return command;
    }

    // Library folder to names, in source names, and the old to new identifiers (assets and their code entries) for the items that change name.
    private static Dictionary<string, HashSet<string>> Select(GameMakerData data, CopyGraph graph, List<PlanEntry> entries, out Dictionary<string, string> renames)
    {
        var owned = entries.SelectMany(e => graph.OwnedCode(e.Type, e.Name)).Distinct();
        var selection = CopyPackage.Selection(entries.Select(e => (e.Type, e.Name)), owned);
        renames = [];
        foreach (var entry in entries.Where(e => e.TargetName != e.Name))
        {
            renames[entry.Name] = entry.TargetName;
            foreach (var code in graph.OwnedCode(entry.Type, entry.Name))
                if (RenamedCode(entry, code) is { } renamed) renames[code] = renamed;
        }

        AddEmitters(data, entries, selection);
        return selection;
    }

    private static readonly string[] CodePrefixes = ["gml_Object_", "gml_Script_", "gml_RoomCC_", "gml_Room_", "gml_Timeline_"];

    private static string? RenamedCode(PlanEntry entry, string code)
    {
        var prefix = CodePrefixes.FirstOrDefault(p => code.StartsWith(p + entry.Name, StringComparison.Ordinal));
        return prefix is null ? null : prefix + entry.TargetName + code[(prefix.Length + entry.Name.Length)..];
    }

    private static void AddEmitters(GameMakerData data, List<PlanEntry> entries, Dictionary<string, HashSet<string>> selection)
    {
        foreach (var system in data.ParticleSystems.Where(s => entries.Any(e => e.Type == "particleSystems" && e.Name == s.Name.Content)))
        {
            if (!selection.TryGetValue("ParticleSystemEmitters", out var set)) selection["ParticleSystemEmitters"] = set = [];
            foreach (var emitter in system.Emitters ?? []) if (emitter.Resource?.Name?.Content is { } name) set.Add(name);
        }
    }

    // Instance ids of a room are unique over the whole file; copied rooms start above every id the target uses.
    private static uint FirstFreeInstanceId(Workspace target)
    {
        using (target.Lock.Read())
        {
            var data = target.Data;
            var highest = Math.Max(data.GeneralInfo?.LastObj ?? 0, data.GeneralInfo?.LastTile ?? 0);
            foreach (var room in data.Rooms)
                foreach (var instance in RoomInstanceCommand.Lists(room).SelectMany(l => l))
                    highest = Math.Max(highest, instance.InstanceID);
            return Math.Max(highest, 100000u) + 1;
        }
    }
}
