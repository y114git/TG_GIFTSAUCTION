using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using G3MLib.DataFile.Compiler;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Integrity;
using G3MLib.Modding.Patching;
using G3MLib.Modding.Resources;

namespace G3MLib.DataFile.Project;

/// <summary>
/// Settings for <see cref="G3MProjectService.Export"/>.
/// </summary>
public sealed class G3MProjectOptions
{
    /// <summary>
    /// Project name; also the name of the <c>.g3mproj</c> file.
    /// </summary>
    public string Name { get; init; } = "Project";

    /// <summary>
    /// Full writes every resource. Delta writes only the resources named in <see cref="Only"/>.
    /// </summary>
    public ProjectExportMode Mode { get; init; } = ProjectExportMode.Full;

    /// <summary>
    /// Resource names to write per modding resource type (for example "Sprites"), used by delta mode.
    /// A type without an entry is skipped.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Only { get; init; }

    /// <summary>
    /// Resource names removed from the game, per modding resource type. They are recorded in the project file and
    /// removed from the data file on build. Null keeps the deletions already recorded in the project file.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Deleted { get; init; }
}

/// <summary>
/// Outcome of an export or a build.
/// </summary>
public sealed class G3MProjectResult
{
    /// <summary>Path of the project file (export) or of the written data file (build).</summary>
    public string Path { get; init; }

    /// <summary>Resources written (export) or imported (build).</summary>
    public int Resources { get; init; }

    /// <summary>Resources a build left untouched because their files match the export.</summary>
    public int Unchanged { get; init; }

    /// <summary>Non-fatal problems.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// The native project format: a <c>.g3mproj</c> file plus one folder per resource, laid out like a GameMaker project.
/// </summary>
/// <remarks>
/// Resource files are produced and read by <see cref="ResourceExportService"/> and <see cref="ResourceImportService"/>,
/// so one serializer per resource type serves projects, the modding export and patches.
/// </remarks>
public static class G3MProjectService
{
    /// <summary>
    /// Newest project format version this library reads and the only one it writes.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// File extension of the project file.
    /// </summary>
    public const string FileExtension = ".g3mproj";

    // Modding resource type, project folder. Texture page items are left out: they are derived from sprites and fonts.
    // Strings, variables, functions and UI layers come from ProjectDataTypes.
    private static readonly (string Type, string Folder)[] ResourceFolders =
    [
        ("Sprites", "sprites"), ("Backgrounds", "backgrounds"), ("Fonts", "fonts"), ("Sounds", "sounds"),
        ("Paths", "paths"), ("Tilesets", "tilesets"), ("Shaders", "shaders"), ("Timelines", "timelines"),
        ("GameObjects", "objects"), ("AnimationCurves", "animcurves"), ("ParticleSystemEmitters", "emitters"),
        ("ParticleSystems", "particles"), ("Sequences", "sequences"), ("Rooms", "rooms"), ("Extensions", "extensions"),
        ("Scripts", "scripts"), ("CodeEntries", "code"),
        ("EmbeddedTextures", "textures"), ("EmbeddedAudio", "audio"), ("EmbeddedImages", "images"),
        ("Strings", "strings"), ("Variables", "variables"), ("Functions", "functions"), ("UILayers", "uilayers"),
        ("GeneralInfo", "options/GeneralInfo"), ("Options", "options/Options"), ("GlobalScripts", "options/GlobalScripts"),
        ("Language", "options/Language"), ("FeatureFlags", "options/FeatureFlags"), ("Tags", "options/Tags"),
        ("FilterEffects", "options/FilterEffects"), ("AudioGroups", "options/AudioGroups"),
        ("TextureGroupInfo", "options/TextureGroupInfo")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly string[] TextExtensions = [".json", ".gml", ".asm"];

    /// <summary>
    /// Writes <paramref name="data"/> as a project into <paramref name="projectDirectory"/>.
    /// </summary>
    /// <param name="data">Game data to write.</param>
    /// <param name="projectDirectory">Project folder; managed resource folders in it are replaced.</param>
    /// <param name="baseDataFilePath">Data file the data was read from, recorded as the project's base file (may be null).</param>
    /// <param name="options">Project name and mode.</param>
    public static G3MProjectResult Export(GameMakerData data, string projectDirectory, string baseDataFilePath, G3MProjectOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Mode == ProjectExportMode.Delta && options.Only is null)
        {
            throw new ArgumentException("Delta mode needs the resources to write in Only", nameof(options));
        }
        projectDirectory = Path.GetFullPath(projectDirectory);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Join(projectDirectory, options.Name + FileExtension);
        List<string> warnings = [];

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string staging = Path.Join(projectDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
        int previousMinWorkers = 0;
        try
        {
            Directory.CreateDirectory(staging);
            // Types only read the model and write to their own staging folder, so they run side by side;
            // warnings are collected per type and added in type order to keep the output deterministic.
            string[] typeWarnings = new string[ResourceFolders.Length];
            // Most of the time goes into file writes that block on the file system, so the pool is allowed more threads than cores.
            ExportText.NormalizeForProject = true;
            ThreadPool.GetMinThreads(out previousMinWorkers, out int minIo);
            ThreadPool.SetMinThreads(Math.Max(previousMinWorkers, Environment.ProcessorCount * 6), minIo);
            Parallel.For(0, ResourceFolders.Length, i =>
            {
                string type = ResourceFolders[i].Type;
                HashSet<string> filter = null;
                if (options.Mode == ProjectExportMode.Delta)
                {
                    if (!options.Only.TryGetValue(type, out IReadOnlySet<string> names))
                    {
                        return;
                    }
                    filter = [.. names];
                }
                CultureInfo threadCulture = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                try
                {
                    ExportType(data, staging, baseDataFilePath ?? "", type, filter);
                }
                catch (Exception e)
                {
                    string partial = Path.Join(staging, TypeDirectoryName(type));
                    if (Directory.Exists(partial))
                    {
                        Directory.Delete(partial, true);
                    }
                    typeWarnings[i] = $"{type} was not exported: {e.Message}";
                }
                finally
                {
                    CultureInfo.CurrentCulture = threadCulture;
                }
            });
            warnings.AddRange(typeWarnings.Where(w => w is not null));

            foreach ((string type, string folder) in ResourceFolders)
            {
                string target = Path.Join(projectDirectory, folder);
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
                string exported = Path.Join(staging, TypeDirectoryName(type));
                if (Directory.Exists(exported))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    Directory.Move(exported, target);
                }
            }
        }
        finally
        {
            ExportText.NormalizeForProject = false;
            if (previousMinWorkers > 0)
            {
                ThreadPool.GetMinThreads(out _, out int restoreIo);
                ThreadPool.SetMinThreads(previousMinWorkers, restoreIo);
            }
            CultureInfo.CurrentCulture = previousCulture;
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }

        ProjectFile existing = ReadExisting(projectPath);
        ProjectFile project = new()
        {
            Name = options.Name,
            FormatVersion = FormatVersion,
            Mode = options.Mode == ProjectExportMode.Full ? "full" : "delta",
            BaseFile = DescribeBaseFile(baseDataFilePath),
            Resources = ListResources(projectDirectory),
            Folders = existing?.Folders ?? [],
            Deleted = options.Deleted is null
                ? existing?.Deleted ?? []
                : [.. options.Deleted.OrderBy(d => d.Key, StringComparer.Ordinal)
                    .SelectMany(d => d.Value.Order(StringComparer.Ordinal).Select(name => new DeletedEntry(d.Key, name)))],
            ResourceOrder = CaptureResourceOrder(data),
            AudioGroups = [.. (data.AudioGroups ?? []).Select(g => new NamedEntry(g?.Name?.Content))],
            TextureGroups = [.. (data.TextureGroupInfo ?? []).Select(g => new NamedEntry(g?.Name?.Content))]
        };
        WriteProjectFile(projectPath, project);
        return new() { Path = projectPath, Resources = project.Resources.Count, Warnings = warnings };
    }

    /// <summary>
    /// Rebuilds a data file from a project.
    /// </summary>
    /// <remarks>
    /// In a full project opened against its recorded base file, resources whose files are unchanged since the export
    /// are left as they are, so an untouched project rebuilds the base file byte for byte.
    /// Every other resource is imported over the base data.
    /// </remarks>
    /// <param name="projectFile">Path to the <c>.g3mproj</c> file.</param>
    /// <param name="baseDataFilePath">Data file to build on.</param>
    /// <param name="outputDataFilePath">Where to write the result.</param>
    /// <exception cref="ProjectException">When the project cannot be read or is newer than this library.</exception>
    public static G3MProjectResult Build(string projectFile, string baseDataFilePath, string outputDataFilePath)
    {
        projectFile = Path.GetFullPath(projectFile);
        string root = Path.GetDirectoryName(projectFile);
        ProjectFile project = ReadExisting(projectFile) ?? throw new ProjectException($"Could not read project file \"{projectFile}\"");
        List<string> warnings = [];
        bool baseVerified = false;
        if (project.BaseFile is { Md5: not null } recorded)
        {
            FileInfo info = new(baseDataFilePath);
            baseVerified = info.Length == recorded.Size && HashFile(baseDataFilePath) == recorded.Md5;
            if (!baseVerified)
            {
                warnings.Add($"The base data file \"{info.Name}\" differs from the one this project was exported from; " +
                             "all resources are imported over it instead of being rebuilt from the project.");
            }
        }

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string staging = Path.Join(TempRoot.Path,"g3mproj-" + Guid.NewGuid().ToString("N"));
        try
        {
            Dictionary<string, string> recordedHashes = project.Resources.ToDictionary(r => r.Id.Path, r => r.Hash, StringComparer.Ordinal);
            bool skipUnchanged = baseVerified && project.Mode == "full";
            int unchanged = 0;
            Dictionary<string, List<string>> changed = new(StringComparer.Ordinal);
            foreach ((string type, string folder) in ResourceFolders)
            {
                foreach (string unit in ListUnits(Path.Join(root, folder)))
                {
                    string relative = $"{folder}/{Path.GetFileName(unit)}";
                    if (skipUnchanged && recordedHashes.TryGetValue(relative, out string hash) && hash == HashUnit(unit))
                    {
                        unchanged++;
                        continue;
                    }
                    if (!changed.TryGetValue(type, out List<string> units))
                    {
                        changed[type] = units = [];
                    }
                    units.Add(unit);
                }
            }
            HashSet<string> present = [.. ResourceFolders.SelectMany(f => ListUnits(Path.Join(root, f.Folder)).Select(u => $"{f.Folder}/{Path.GetFileName(u)}"))];
            foreach (string missing in recordedHashes.Keys.Where(k => !present.Contains(k)))
            {
                warnings.Add($"\"{missing}\" is in the project file but missing on disk; it stays in the data file.");
            }

            using GameMakerData data = ReadData(baseDataFilePath);
            int imported = 0;
            foreach (DeletedEntry deleted in project.Deleted)
            {
                if (PatchService.DeleteResource(data, deleted.Type, deleted.Name))
                {
                    imported++;
                }
                else
                {
                    warnings.Add($"{deleted.Type} \"{deleted.Name}\" is recorded as deleted but was not found in the data file.");
                }
            }
            // The room order still points at a deleted room, which the writer cannot resolve.
            if (data.GeneralInfo?.RoomOrder is { } roomOrder)
            {
                for (int i = roomOrder.Count - 1; i >= 0; i--)
                {
                    if (!data.Rooms.Contains(roomOrder[i].Resource))
                    {
                        roomOrder.RemoveAt(i);
                    }
                }
            }
            ResourceImportService.SetPatchFileSystem(null);
            ResourceImportService.SetDataPaths(baseDataFilePath, outputDataFilePath);
            // Strings first so strings added by other imports land after the project's own.
            foreach (string type in (string[])["Strings", .. ResourceTypeRegistry.ImportOrder, "UILayers", "Variables", "Functions"])
            {
                if (!changed.TryGetValue(type, out List<string> units))
                {
                    continue;
                }
                if (type == "CodeEntries")
                {
                    imported += ImportCode(data, units, warnings);
                    continue;
                }
                string typeDirectory = Path.Join(staging, type);
                Directory.CreateDirectory(typeDirectory);
                foreach (string unit in units)
                {
                    CopyUnit(unit, Path.Join(typeDirectory, Path.GetFileName(unit)));
                }
                if (ProjectDataTypes.Handles(type))
                {
                    ProjectDataTypes.Import(type, data, typeDirectory);
                    imported += units.Count;
                    continue;
                }
                if (!ResourceImportService.Import(type, data, typeDirectory))
                {
                    warnings.Add($"No importer for {type}.");
                    continue;
                }
                imported += units.Count;
            }

            // Code order is left to the integrity repair, which keeps child entries valid.
            ApplyResourceOrder(data, project.ResourceOrder);
            if (imported > 0)
            {
                DataIntegrityResult integrity = DataIntegrityService.RepairAndValidate(data);
                warnings.AddRange(integrity.Errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputDataFilePath)));
            using FileStream output = new(outputDataFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            GameMakerIO.Write(output, data);
            return new() { Path = outputDataFilePath, Resources = imported, Unchanged = unchanged, Warnings = warnings };
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    /// <summary>
    /// Opens a project for an editor: reads the project file and returns its resource index. Resource folders are not read,
    /// so the time depends on the size of the project file only. Progress ends with fraction 1.
    /// </summary>
    /// <param name="projectFile">Path to the <c>.g3mproj</c> file.</param>
    /// <param name="progress">Receives progress reports; may be null.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <exception cref="ProjectException">When the project cannot be read or is newer than this library.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task<G3MProject> OpenAsync(string projectFile, IProgress<GameMakerProgress> progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(projectFile);
            progress?.Report(new GameMakerProgress(0.0, "Project file"));
            ProjectFile file = ReadExisting(path) ?? throw new ProjectException($"Could not read project file \"{path}\"");
            if (file.FormatVersion > FormatVersion)
            {
                throw new ProjectException($"Project format version {file.FormatVersion} is newer than the supported version {FormatVersion}");
            }
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new GameMakerProgress(0.5, "Resource index"));
            G3MProject project = new(file.Name, file.FormatVersion, file.Mode, Path.GetDirectoryName(path),
                file.BaseFile is { } b ? new G3MProjectBaseFile(b.Name, b.Size, b.Md5) : null,
                [.. file.Resources.Select(r => new G3MProjectResource(r.Id.Name, r.Id.Path, r.Type, r.Hash))],
                file.Folders,
                file.ResourceOrder.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
                [.. file.AudioGroups.Select(g => g.Name)],
                [.. file.TextureGroups.Select(g => g.Name)]);
            progress?.Report(new GameMakerProgress(1.0, "Done"));
            return project;
        }, cancellationToken);
    }

    private static GameMakerData ReadData(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return GameMakerIO.Read(stream);
    }

    // Name of the folder ResourceExportService creates for a type.
    private static string TypeDirectoryName(string type) => type;

    private static void ExportType(GameMakerData data, string directory, string baseDataFilePath, string type, HashSet<string> filter)
    {
        if (ProjectDataTypes.Handles(type))
        {
            ProjectDataTypes.Export(type, data, Path.Join(directory, type), filter);
            return;
        }
        if (filter is null)
        {
            ResourceExportService.ExportTypes(data, directory, baseDataFilePath, [type]);
            return;
        }
        switch (type)
        {
            case "Sprites": ResourceExportService.ExportSprites(data, directory, filter); break;
            case "Backgrounds": ResourceExportService.ExportBackgrounds(data, directory, filter); break;
            case "Fonts": ResourceExportService.ExportFonts(data, directory, filter); break;
            case "Sounds": ResourceExportService.ExportSounds(data, directory, baseDataFilePath, filter); break;
            case "Paths": ResourceExportService.ExportPaths(data, directory, filter); break;
            case "Tilesets": ResourceExportService.ExportTilesets(data, directory, filter); break;
            case "Shaders": ResourceExportService.ExportShaders(data, directory, filter); break;
            case "Timelines": ResourceExportService.ExportTimelines(data, directory, filter); break;
            case "GameObjects": ResourceExportService.ExportGameObjects(data, directory, filter); break;
            case "AnimationCurves": ResourceExportService.ExportAnimationCurves(data, directory, filter); break;
            case "ParticleSystemEmitters": ResourceExportService.ExportParticleSystemEmitters(data, directory, filter); break;
            case "ParticleSystems": ResourceExportService.ExportParticleSystems(data, directory, filter); break;
            case "Sequences": ResourceExportService.ExportSequences(data, directory, filter); break;
            case "Rooms": ResourceExportService.ExportRooms(data, directory, filter); break;
            case "Scripts": ResourceExportService.ExportScripts(data, directory, filter); break;
            case "FilterEffects": ResourceExportService.ExportFilterEffects(data, directory, filter); break;
            case "AudioGroups": ResourceExportService.ExportAudioGroups(data, directory, filter); break;
            case "CodeEntries": ResourceExportService.ExportCodeEntriesSelective(data, directory, filter); break;
            default: ResourceExportService.ExportTypes(data, directory, baseDataFilePath, [type]); break;
        }
    }

    // Code is compiled from the .gml file of each changed entry; the .asm files are read-only companions.
    private static int ImportCode(GameMakerData data, List<string> units, List<string> warnings)
    {
        Dictionary<string, CodeArchiveEntryDescriptor> byKey = CodeEntryArchiveIdentity.DescribeEntriesByKey(data);
        CodeImportGroup group = new(data) { AutoCreateAssets = false };
        int queued = 0;
        foreach (string unit in units.OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string key = Path.GetFileName(unit);
            string gml = Path.Join(unit, key + ".gml");
            if (!File.Exists(gml))
            {
                continue;
            }
            string name = byKey.TryGetValue(key, out CodeArchiveEntryDescriptor descriptor) ? descriptor.LogicalName : key;
            if (descriptor is { Occurrence: > 1 })
            {
                warnings.Add($"Code entry \"{name}\" occurs more than once and was not imported.");
                continue;
            }
            group.QueueReplace(name, File.ReadAllText(gml));
            queued++;
        }
        if (queued > 0)
        {
            try
            {
                group.Import(true);
            }
            catch (Exception e)
            {
                throw new ProjectException(e.Message, e);
            }
        }
        return queued;
    }

    // A unit is one entry of a resource folder: a resource directory, or a single file for project-wide settings.
    private static List<string> ListUnits(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }
        return [.. Directory.EnumerateFileSystemEntries(folder).Order(StringComparer.Ordinal)];
    }

    // Normalizes the text files of every unit and hashes the result; units are processed in parallel, the list keeps its order.
    private static List<ResourceEntry> ListResources(string projectDirectory)
    {
        List<(string Type, string Folder, string Unit)> units = [];
        foreach ((string type, string folder) in ResourceFolders)
        {
            units.AddRange(ListUnits(Path.Join(projectDirectory, folder)).Select(unit => (type, folder, unit)));
        }
        string[] hashes = new string[units.Count];
        Parallel.For(0, units.Count, i => hashes[i] = HashUnit(units[i].Unit, true));
        return [.. units.Select((u, i) =>
        {
            string name = Path.GetFileName(u.Unit);
            return new ResourceEntry { Id = new(name, $"{u.Folder}/{name}"), Type = u.Type, Hash = hashes[i] };
        })];
    }

    private static string HashUnit(string unit, bool normalize = false)
    {
        string root = Directory.Exists(unit) ? unit : Path.GetDirectoryName(unit);
        IEnumerable<string> files = Directory.Exists(unit) ? Directory.EnumerateFiles(unit, "*", SearchOption.AllDirectories) : [unit];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files.OrderBy(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'), StringComparer.Ordinal))
        {
            byte[] name = Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
            byte[] content = normalize && TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                ? ProjectContext.NormalizeTextFile(file)
                : File.ReadAllBytes(file);
            hash.AppendData(BitConverter.GetBytes(name.Length));
            hash.AppendData(name);
            hash.AppendData(BitConverter.GetBytes(content.LongLength));
            hash.AppendData(content);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void CopyUnit(string unit, string destination)
    {
        if (File.Exists(unit))
        {
            File.Copy(unit, destination, true);
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(unit, "*", SearchOption.AllDirectories))
        {
            string target = Path.Join(destination, Path.GetRelativePath(unit, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(MD5.HashData(stream));
    }

    private static BaseFileInfo DescribeBaseFile(string path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }
        return new() { Name = Path.GetFileName(path), Size = new FileInfo(path).Length, Md5 = HashFile(path) };
    }

    private static ProjectFile ReadExisting(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            JsonObject node = JsonNode.Parse(File.ReadAllText(path), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                ?? throw new ProjectException($"Project file \"{path}\" is not valid: the root is not an object");
            return Migrate(node, path).Deserialize<ProjectFile>(JsonOptions);
        }
        catch (JsonException e)
        {
            throw new ProjectException($"Project file \"{path}\" is not valid: {e.Message}", e);
        }
    }

    // Upgrades an older project file one version at a time. Add an entry keyed by the version it upgrades from
    // when FormatVersion is raised; version 1 is the first, so there is nothing to migrate yet.
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> Migrations = [];

    private static JsonObject Migrate(JsonObject project, string path)
    {
        int version = project["formatVersion"]?.GetValue<int>() ?? 0;
        if (version > FormatVersion)
        {
            throw new ProjectException($"Project \"{path}\" uses format version {version}, which is newer than the version {FormatVersion} this library supports. Update G3MLib to open it.");
        }
        while (version < FormatVersion)
        {
            if (!Migrations.TryGetValue(version, out Func<JsonObject, JsonObject> migrate))
            {
                throw new ProjectException($"Project \"{path}\" has an unknown format version {version}; versions before {FormatVersion} cannot be migrated.");
            }
            project = migrate(project);
            version++;
            project["formatVersion"] = version;
        }
        return project;
    }

    private static void WriteProjectFile(string path, ProjectFile project)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(project, JsonOptions) + "\n", Utf8NoBom);
    }

    private sealed record ResourceList(string Type, Func<List<string>> Names, Action<IReadOnlyList<string>> Apply);

    private static ResourceList Entry<T>(string type, IList<T> list) where T : GameMakerNamedResource
    {
        return new(type, () => [.. list.Select(x => x?.Name?.Content)], order => ApplyOrder(list, order));
    }

    private static IEnumerable<ResourceList> ResourceLists(GameMakerData data)
    {
        if (data.Sprites is { } sprites) yield return Entry("Sprites", sprites);
        if (data.Backgrounds is { } backgrounds) yield return Entry("Backgrounds", backgrounds);
        if (data.Paths is { } paths) yield return Entry("Paths", paths);
        if (data.Scripts is { } scripts) yield return Entry("Scripts", scripts);
        if (data.Fonts is { } fonts) yield return Entry("Fonts", fonts);
        if (data.GameObjects is { } objects) yield return Entry("GameObjects", objects);
        if (data.Timelines is { } timelines) yield return Entry("Timelines", timelines);
        if (data.Rooms is { } rooms) yield return Entry("Rooms", rooms);
        if (data.Shaders is { } shaders) yield return Entry("Shaders", shaders);
        if (data.Extensions is { } extensions) yield return Entry("Extensions", extensions);
        if (data.AudioGroups is { } audioGroups) yield return Entry("AudioGroups", audioGroups);
        if (data.Sounds is { } sounds) yield return Entry("Sounds", sounds);
        if (data.AnimationCurves is { } curves) yield return Entry("AnimationCurves", curves);
        if (data.Sequences is { } sequences) yield return Entry("Sequences", sequences);
        if (data.ParticleSystems is { } particles) yield return Entry("ParticleSystems", particles);
        if (data.FORM.UILR?.List is { } uiLayers) yield return Entry("UILayers", uiLayers);
    }

    private static Dictionary<string, List<string>> CaptureResourceOrder(GameMakerData data)
    {
        Dictionary<string, List<string>> order = new(StringComparer.Ordinal);
        foreach (ResourceList list in ResourceLists(data))
        {
            List<string> names = list.Names();
            if (names.Count > 0)
            {
                order[list.Type] = names;
            }
        }
        return order;
    }

    private static void ApplyResourceOrder(GameMakerData data, Dictionary<string, List<string>> order)
    {
        foreach (ResourceList list in ResourceLists(data))
        {
            if (order.TryGetValue(list.Type, out List<string> names))
            {
                list.Apply(names);
            }
        }
    }

    // Moves named entries into the recorded order; entries not in the record keep their relative order at the end.
    // Lists with unnamed or duplicate names are left alone because their entries cannot be matched.
    private static void ApplyOrder<T>(IList<T> list, IReadOnlyList<string> order) where T : GameMakerNamedResource
    {
        Dictionary<string, T> byName = new(StringComparer.Ordinal);
        foreach (T item in list)
        {
            string name = item?.Name?.Content;
            if (name is null || !byName.TryAdd(name, item))
            {
                return;
            }
        }
        HashSet<string> placed = new(StringComparer.Ordinal);
        List<T> sorted = [];
        foreach (string name in order)
        {
            if (name is not null && byName.TryGetValue(name, out T item) && placed.Add(name))
            {
                sorted.Add(item);
            }
        }
        sorted.AddRange(list.Where(item => !placed.Contains(item.Name.Content)));
        if (sorted.SequenceEqual(list))
        {
            return;
        }
        list.Clear();
        foreach (T item in sorted)
        {
            list.Add(item);
        }
    }

    private sealed class ProjectFile
    {
        public string ResourceType { get; set; } = "G3MProject";

        public string Name { get; set; }

        public int FormatVersion { get; set; }

        public string Mode { get; set; } = "full";

        public BaseFileInfo BaseFile { get; set; }

        public List<ResourceEntry> Resources { get; set; } = [];

        [JsonPropertyName("Folders")]
        public List<string> Folders { get; set; } = [];

        public List<DeletedEntry> Deleted { get; set; } = [];

        public Dictionary<string, List<string>> ResourceOrder { get; set; } = [];

        [JsonPropertyName("AudioGroups")]
        public List<NamedEntry> AudioGroups { get; set; } = [];

        [JsonPropertyName("TextureGroups")]
        public List<NamedEntry> TextureGroups { get; set; } = [];
    }

    private sealed class BaseFileInfo
    {
        public string Name { get; set; }

        public long Size { get; set; }

        public string Md5 { get; set; }
    }

    private sealed class ResourceEntry
    {
        public ResourceId Id { get; set; }

        public string Type { get; set; }

        public string Hash { get; set; }
    }

    private sealed record ResourceId(string Name, string Path);

    private sealed record NamedEntry(string Name);

    private sealed record DeletedEntry(string Type, string Name);
}
