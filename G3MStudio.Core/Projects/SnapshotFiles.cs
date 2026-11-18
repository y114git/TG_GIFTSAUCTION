namespace G3MStudio.Core.Projects;

/// <summary>File and folder helpers of <see cref="SnapshotStore"/>: a resource is a file or a folder.</summary>
internal static class SnapshotFiles
{
    internal static void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
    }

    internal static void Copy(string source, string target)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            File.Copy(source, target, true);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to) ?? ".");
            File.Copy(file, to, true);
        }
    }

    /// <summary>
    /// Puts <paramref name="fresh"/> in place of <paramref name="target"/>. The current resource is renamed aside, not deleted, and goes back
    /// if the new one cannot be moved in, so a failure leaves the current resource as it was.
    /// </summary>
    internal static void Swap(string fresh, string target)
    {
        var aside = target + ".replaced";
        Delete(aside);
        var movedAside = false;
        try
        {
            if (Directory.Exists(target)) { Directory.Move(target, aside); movedAside = true; }
            else if (File.Exists(target)) { File.Move(target, aside); movedAside = true; }
            if (Directory.Exists(fresh)) Directory.Move(fresh, target); else File.Move(fresh, target);
        }
        catch
        {
            if (movedAside && !Directory.Exists(target) && !File.Exists(target))
            {
                if (Directory.Exists(aside)) Directory.Move(aside, target); else File.Move(aside, target);
            }

            throw;
        }

        try
        {
            Delete(aside);
        }
        catch (IOException)
        {
            // the swap worked; a leftover backup name is cleared by the next one
        }
    }

    internal static Snapshot? ReadManifest(string file)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file));
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or IOException)
        {
            return null;
        }
    }
}
