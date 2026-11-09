using System.Security.Cryptography;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.ModPack;

/// <summary>A loaded data file and where it lives on disk (the export needs both).</summary>
public sealed record Side(GameMakerData Data, string Path);

/// <summary>A built pack and the items left out of it, each with the reason.</summary>
/// <summary>What a pack says about itself: name, version, authors.</summary>
public sealed record PackInfo(string Name, string Version, IReadOnlyList<string> Authors);

public sealed record PackBuild(ModPackContent Pack, IReadOnlyList<string> Warnings);

/// <summary>Makes pack entries from the differences between a base file (left) and a changed file (right). Hold the read locks of both files when called off the UI thread.</summary>
public static class ModPackBuilder
{
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static PackBuild Build(Side left, Side right, IEnumerable<DiffItem> items, PackInfo info, CancellationToken cancellationToken = default)
    {
        var entries = new List<PackEntry>();
        var warnings = new List<string>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Entry(left, right, item, cancellationToken, out var warning) is { } entry) entries.Add(entry);
            if (warning is not null) warnings.Add(warning);
        }

        var game = right.Data.GeneralInfo?.Name?.Content ?? System.IO.Path.GetFileNameWithoutExtension(right.Path);
        var manifest = ModPackFile.NewManifest(info.Name, info.Version, game, HashFile(left.Path), CopyVersion.Of(right.Data), info.Authors);
        return new PackBuild(new ModPackContent(manifest, entries), warnings);
    }

    /// <summary>The pack entry for one difference, or null (with the reason in <paramref name="warning"/>) when it cannot travel.</summary>
    public static PackEntry? Entry(Side left, Side right, DiffItem item, CancellationToken cancellationToken, out string? warning)
    {
        warning = null;
        var change = new PackChange(item.Type, item.Name, item.Kind switch { ChangeKind.Added => "add", ChangeKind.Removed => "remove", _ => "replace" }, item.LeftHash, item.RightHash);
        if (item.Type == "code") return CodeEntry(left, right, item, change, out warning);
        if (item.Kind == ChangeKind.Removed) return new PackEntry(change);
        try
        {
            return new PackEntry(change, Payload(right, item, cancellationToken));
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException)
        {
            warning = $"{item.Type} {item.Name}: {e.Message}";
            return null;
        }
    }

    // A code entry that is new or gone belongs to an object, script or room that carries it; only a changed one travels as text.
    private static PackEntry? CodeEntry(Side left, Side right, DiffItem item, PackChange change, out string? warning)
    {
        warning = null;
        if (item.Kind != ChangeKind.Changed)
        {
            warning = $"code {item.Name}: {(item.Kind == ChangeKind.Added ? "new" : "removed")} code travels with its owner";
            return null;
        }

        var (before, after) = (DataDiff.CodeOf(left.Data, item.Name), DataDiff.CodeOf(right.Data, item.Name));
        if (before is null || after is null)
        {
            warning = $"code {item.Name}: not readable";
            return null;
        }

        return new PackEntry(change with { Op = "code" }, null, after, before);
    }

    private static byte[] Payload(Side right, DiffItem item, CancellationToken cancellationToken)
    {
        var folder = Directory.CreateTempSubdirectory("g3mstudio-pack-").FullName;
        try
        {
            var entry = new PlanEntry(item.Type, item.Name, item.Name, CopyAction.Add, null, new CopyFacts(false, false, false, []));
            ResourceCopier.ExportFolder(right.Data, right.Path, [entry], folder, null, cancellationToken);
            return CopyPackage.Zip(folder);
        }
        finally
        {
            CopyPackage.TryDelete(folder);
        }
    }
}
