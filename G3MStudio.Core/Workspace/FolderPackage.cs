using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>Exports the entries of a virtual folder, collection or tag as one ZIP in the layout of the copy package (with the code the entries own).</summary>
public static class FolderPackage
{
    /// <summary>The entries of <paramref name="keys"/> that a package can hold, in order; the rest cannot be exported as files.</summary>
    public static IReadOnlyList<(string Type, string Name)> Packable(IEnumerable<string> keys) =>
        [.. BulkResources.Split(keys).Where(i => CopyPlanner.Supported.Contains(i.Type))];

    /// <summary>Writes the ZIP under a temporary name and moves it into place. Returns the number of entries packed.</summary>
    public static Task<int> ExportAsync(Workspace source, IReadOnlyList<(string Type, string Name)> items, string zipPath, IProgress<BatchProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            if (items.Count == 0) throw new InvalidOperationException("There is nothing to pack.");
            var folder = Directory.CreateTempSubdirectory("g3mstudio-pack-").FullName;
            var temp = $"{zipPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                List<PlanEntry> entries = [.. items.Select(i => new PlanEntry(i.Type, i.Name, i.Name, CopyAction.Add, null, new CopyFacts(false, false, true, [])))];
                using (source.Lock.Read()) ResourceCopier.ExportFolder(source.Data, source.Path, entries, folder, progress, cancellationToken);
                File.WriteAllBytes(temp, CopyPackage.Zip(folder, true));
                File.Move(temp, zipPath, true);
                return entries.Count;
            }
            finally
            {
                CopyPackage.TryDelete(folder);
                if (File.Exists(temp)) File.Delete(temp);
            }
        }, cancellationToken);
}
