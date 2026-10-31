using G3MLib.Modding.Internal;
using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Export and import of resources to and from files, on the library's batch services: one result per item, progress per item, cancellation
/// that removes what a run added. An Explorer type key (<c>sprites</c>) maps to the folders the library writes (<c>Sprites</c>).
/// </summary>
public static class ResourceTransfer
{
    // Folders of the library's resource layout that belong to an Explorer type. Data-only types (strings, variables) have none.
    private static readonly Dictionary<string, string[]> Folders = new()
    {
        ["sprites"] = ["Sprites"],
        ["tileSets"] = ["Backgrounds", "Tilesets"],
        ["sounds"] = ["Sounds"],
        ["paths"] = ["Paths"],
        ["scripts"] = ["Scripts"],
        ["shaders"] = ["Shaders"],
        ["fonts"] = ["Fonts"],
        ["timelines"] = ["Timelines"],
        ["objects"] = ["GameObjects"],
        ["rooms"] = ["Rooms"],
        ["extensions"] = ["Extensions"],
        ["sequences"] = ["Sequences"],
        ["animationCurves"] = ["AnimationCurves"],
        ["particleSystems"] = ["ParticleSystems", "ParticleSystemEmitters"],
        ["code"] = ["CodeEntries"],
        ["general"] = ["GeneralInfo", "Options"],
    };

    /// <summary>True when the type can be exported (and imported) as files.</summary>
    public static bool Supports(string typeKey) => Folders.ContainsKey(typeKey);

    /// <summary>
    /// Writes the named entries of a type (all of them when <paramref name="names"/> is null) under <paramref name="directory"/>.
    /// Runs off the calling thread under the model's read lock.
    /// </summary>
    public static Task<IReadOnlyList<ItemResult>> ExportAsync(Workspace workspace, string directory, string typeKey, ISet<string>? names, IProgress<BatchProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            if (!Folders.TryGetValue(typeKey, out var folders)) throw new ArgumentException($"'{typeKey}' cannot be exported as files.", nameof(typeKey));
            var results = new List<ItemResult>();
            using (workspace.Lock.Read())
                foreach (var folder in folders) results.AddRange(ExportFolder(workspace, directory, folder, names, progress, cancellationToken));
            return (IReadOnlyList<ItemResult>)results;
        }, cancellationToken);

    /// <summary>Everything the data file holds as files, type by type, one progress stream.</summary>
    public static async Task<IReadOnlyList<ItemResult>> ExportAllAsync(Workspace workspace, string directory, IProgress<BatchProgress>? progress, CancellationToken cancellationToken)
    {
        var results = new List<ItemResult>();
        var types = Folders.Keys.ToList();
        for (var i = 0; i < types.Count; i++)
        {
            var step = new Progress<BatchProgress>(p => progress?.Report(new BatchProgress(i, types.Count, $"{types[i]}: {p.Item}")));
            results.AddRange(await ExportAsync(workspace, directory, types[i], null, step, cancellationToken));
        }

        progress?.Report(new BatchProgress(types.Count, types.Count, ""));
        return results;
    }

    private static IReadOnlyList<ItemResult> ExportFolder(Workspace workspace, string dir, string folder, ISet<string>? names, IProgress<BatchProgress>? progress, CancellationToken ct)
    {
        var data = workspace.Data;
        return folder switch
        {
            "Sprites" => ResourceExportService.ExportSprites(data, dir, names, progress, ct),
            "Backgrounds" => ResourceExportService.ExportBackgrounds(data, dir, names, progress, ct),
            "Tilesets" => ResourceExportService.ExportTilesets(data, dir, names, progress, ct),
            "Sounds" => ResourceExportService.ExportSounds(data, dir, workspace.Path, names, progress, ct),
            "Paths" => ResourceExportService.ExportPaths(data, dir, names, progress, ct),
            "Scripts" => ResourceExportService.ExportScripts(data, dir, names, progress, ct),
            "Shaders" => ResourceExportService.ExportShaders(data, dir, names, progress, ct),
            "Fonts" => ResourceExportService.ExportFonts(data, dir, names, progress, ct),
            "Timelines" => ResourceExportService.ExportTimelines(data, dir, names, progress, ct),
            "GameObjects" => ResourceExportService.ExportGameObjects(data, dir, names, progress, ct),
            "Rooms" => ResourceExportService.ExportRooms(data, dir, names, progress, ct),
            "Extensions" => ResourceExportService.ExportExtensions(data, dir, progress, ct),
            "Sequences" => ResourceExportService.ExportSequences(data, dir, names, progress, ct),
            "AnimationCurves" => ResourceExportService.ExportAnimationCurves(data, dir, names, progress, ct),
            "ParticleSystems" => ResourceExportService.ExportParticleSystems(data, dir, names, progress, ct),
            "ParticleSystemEmitters" => ResourceExportService.ExportParticleSystemEmitters(data, dir, names, progress, ct),
            "CodeEntries" when names is null => ResourceExportService.ExportCodeEntries(data, dir, progress, ct),
            "CodeEntries" => ResourceExportService.ExportCodeEntriesSelective(data, dir, names, progress, ct),
            "GeneralInfo" => ResourceExportService.ExportGeneralInfo(data, dir, progress, ct),
            "Options" => ResourceExportService.ExportOptions(data, dir, progress, ct),
            _ => throw new ArgumentException($"Unknown folder '{folder}'.", nameof(folder)),
        };
    }

    /// <summary>The resource folders found in a directory, in the order the library imports them (dependencies first).</summary>
    public static IReadOnlyList<string> FoldersIn(string directory) =>
        [.. ResourceTypeRegistry.ImportOrder.Where(type => Directory.Exists(Path.Combine(directory, type)))];

    /// <summary>
    /// Imports the given folders of a directory into the data file, in the library's import order. The model changes in place: the caller makes the
    /// change a bulk change of the workspace (see <see cref="Workspace.ApplyBulk"/>), because it cannot be undone.
    /// </summary>
    public static IReadOnlyList<ItemResult> Import(Workspace workspace, string directory, IEnumerable<string> folders, IProgress<BatchProgress>? progress, CancellationToken cancellationToken)
    {
        var wanted = folders.ToHashSet(StringComparer.Ordinal);
        var results = new List<ItemResult>();
        foreach (var folder in ResourceTypeRegistry.ImportOrder.Where(wanted.Contains))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(ResourceImportService.Import(folder, workspace.Data, Path.Combine(directory, folder), progress, cancellationToken));
        }

        return results;
    }
}
