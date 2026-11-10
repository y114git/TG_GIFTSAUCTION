using System.Globalization;
using System.Text.Json;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>One item of a copy as the command stores it: its type, the name it gets in the target, and whether it replaces an item there.</summary>
public sealed record CopyStep(string Type, string Name, bool Replace);

/// <summary>
/// Copies resources that came from another data file: the package holds them in the library's export layout, already made safe for this target
/// (see <see cref="CopyPackage"/>). One edit: undo takes out what was added and puts back what was replaced. The package travels in the arguments
/// (the journal moves it to a side file), so the copy replays without the source file.
/// </summary>
public sealed class CopyResourcesCommand : EditCommand
{
    public const string Name = "copyResources";

    private readonly byte[] _package;
    private readonly CopyVersion _source;
    private readonly uint _lastId;
    private ModelSnapshot? _snapshot;
    private ShallowMemento? _memento;

    public CopyResourcesCommand(byte[] package, CopyVersion source, IReadOnlyList<CopyStep> steps, uint lastInstanceId)
    {
        (_package, _source, Steps, _lastId) = (package, source, steps, lastInstanceId);
        Args = new Dictionary<string, string>
        {
            ["package"] = Convert.ToBase64String(package),
            ["source"] = JsonSerializer.Serialize(source),
            ["steps"] = JsonSerializer.Serialize(steps),
            ["lastId"] = lastInstanceId.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>What the copy adds or replaces, for the history list.</summary>
    public IReadOnlyList<CopyStep> Steps { get; }

    public override string Kind => Name;

    public override string Editor => "explorer";

    public override IReadOnlyDictionary<string, string> Args { get; }

    public override IReadOnlyList<ProjectTouch>? Touches => null; // every type file may change

    /// <summary>What the library reported for each item the last time the command ran.</summary>
    public IReadOnlyList<ItemResult> Results { get; private set; } = [];

    /// <summary>Set before the first Apply by a caller that shows progress or can cancel; a cancelled copy leaves the file as it was.</summary>
    public IProgress<BatchProgress>? Progress { get; set; }

    public CancellationToken Cancellation { get; set; }

    public static CopyResourcesCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(
        Convert.FromBase64String(a["package"]),
        JsonSerializer.Deserialize<CopyVersion>(a["source"]) ?? throw new InvalidDataException("The copy has no source version."),
        JsonSerializer.Deserialize<List<CopyStep>>(a["steps"]) ?? [],
        uint.Parse(a["lastId"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        if (_source.RefusalFor(CopyVersion.Of(data)) is { } refusal) throw new InvalidOperationException(refusal);
        Check(data);
        var snapshot = new ModelSnapshot(data);
        var memento = Remember(data);
        var folder = Directory.CreateTempSubdirectory("g3mstudio-copy-").FullName;
        try
        {
            CopyPackage.Unzip(_package, folder);
            var failed = CopyPackage.Import(data, folder, Selection(folder), Progress, Cancellation).FirstOrDefault(r => r.Kind == ResultKind.Failed);
            if (failed is not null) throw new InvalidOperationException($"{failed.Name} could not be copied: {failed.Message}");
            if (data.GeneralInfo is { } info && _lastId > info.LastObj) info.LastObj = _lastId;
            FillLayerLists(data);
        }
        catch
        {
            snapshot.Restore();
            memento.Restore();
            throw;
        }
        finally
        {
            CopyPackage.TryDelete(folder);
        }

        (_snapshot, _memento) = (snapshot, memento);
    }

    public override void Revert(GameMakerData data)
    {
        if (_snapshot is null || _memento is null) throw new InvalidOperationException("The copy was never applied.");
        _snapshot.Restore();
        _memento.Restore();
    }

    // A room from a file of an older version has no list for what newer layers hold (particle systems, text items). The writer of a newer
    // target writes a pointer for each, and a null list leaves a pointer that nothing reads back.
    private void FillLayerLists(GameMakerData data)
    {
        var rooms = Steps.Where(s => s.Type == "rooms").Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var room in data.Rooms.Where(r => rooms.Contains(r.Name?.Content ?? "")))
            foreach (var assets in (room.Layers ?? []).Select(l => l.Data).OfType<GameMakerRoom.Layer.LayerAssetsData>())
            {
                assets.LegacyTiles ??= [];
                assets.Sprites ??= [];
                assets.Sequences ??= [];
                assets.NineSlices ??= [];
                assets.ParticleSystems ??= [];
                assets.TextItems ??= [];
            }
    }

    // Every added name must be free and every replaced item must exist, before anything changes.
    private void Check(GameMakerData data)
    {
        foreach (var step in Steps)
        {
            if (step.Replace && !CopyPlanner.Exists(data, step.Type, step.Name)) throw new InvalidOperationException($"'{step.Name}' to replace was not found in the target.");
            if (!step.Replace && CopyPlanner.NameTaken(data, step.Type, step.Name)) throw new InvalidOperationException($"The target already has something named '{step.Name}'.");
        }
    }

    private Dictionary<string, HashSet<string>> Selection(string folder)
    {
        var selection = CopyPackage.Selection(Steps.Select(s => (s.Type, s.Name)), []);
        // Code entries and particle emitters travel with their owners under names of their own.
        foreach (var extra in new[] { "CodeEntries", "ParticleSystemEmitters" })
        {
            var root = Path.Combine(folder, extra);
            if (!Directory.Exists(root)) continue;
            selection[extra] = [.. Directory.GetDirectories(root).Select(Path.GetFileName).OfType<string>()];
        }

        return selection;
    }

    // The items the import changes in place: the ones it replaces and the code entries it writes over.
    private ShallowMemento Remember(GameMakerData data)
    {
        var memento = new ShallowMemento();
        foreach (var step in Steps.Where(s => s.Replace))
        {
            memento.Capture(step.Type == "code" ? data.Code.ByName(step.Name) : ResourceTypes.Find(data, ResourceTypes.KeyOf(step.Type, step.Name)), 3);
            if (step.Type == "sounds") memento.Capture(data.Sounds.ByName(step.Name)?.AudioFile, 1);
        }

        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(_package));
        foreach (var code in zip.Entries.Select(e => e.FullName.Split('/')).Where(p => p.Length > 2 && p[0] == "CodeEntries").Select(p => p[1]).Distinct())
            memento.Capture(data.Code.ByName(code), 2);
        return memento;
    }
}
