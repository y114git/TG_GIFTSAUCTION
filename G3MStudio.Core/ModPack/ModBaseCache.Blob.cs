using System.IO.Compression;
using System.Security.Cryptography;

namespace G3MStudio.Core.ModPack;

public sealed partial class ModBaseCache
{
    // Writes the gzip copy to a temporary file next to the others, hashing the bytes on the way; a copy over the cap, or of a file that changed while it was read, is deleted and the hash is null.
    private (string? Hash, long Size) Compress(string source, long maxBytes, out string part)
    {
        part = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".part");
        try
        {
            using var input = File.OpenRead(source);
            var before = (new FileInfo(source).Length, new FileInfo(source).LastWriteTimeUtc);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var output = new GZipStream(File.Create(part), CompressionLevel.Fastest))
            {
                var buffer = new byte[1 << 16];
                int read;
                while ((read = input.Read(buffer)) > 0)
                {
                    sha.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                }
            }

            var after = new FileInfo(source);
            if ((after.Length, after.LastWriteTimeUtc) != before)
            {
                File.Delete(part);
                return (null, 0);
            }

            if (new FileInfo(part).Length <= maxBytes) return (Convert.ToHexString(sha.GetHashAndReset()), input.Length);
            File.Delete(part);
            return (null, 0);
        }
        catch
        {
            File.Delete(part);
            throw;
        }
    }

    // Drops the oldest entries (never the one just written) until the copies fit, then deletes copies no entry names.
    private void Evict(Dictionary<string, ModBaseEntry> index, long maxBytes, string keep)
    {
        long Total() => index.Values.Select(e => e.Hash).Distinct().Sum(h => File.Exists(BlobFile(h)) ? new FileInfo(BlobFile(h)).Length : 0);
        while (Total() > maxBytes && index.Where(p => p.Key != keep).OrderBy(p => p.Value.Saved).Select(p => p.Key).FirstOrDefault() is { } oldest) index.Remove(oldest);
        var used = index.Values.Select(e => e.Hash).ToHashSet();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.gz"))
            if (!used.Contains(System.IO.Path.GetFileNameWithoutExtension(file))) File.Delete(file);
    }

    // Leftovers of a copy that was interrupted.
    private void SweepParts()
    {
        foreach (var part in Directory.EnumerateFiles(_folder, "*.part"))
        {
            try
            {
                File.Delete(part);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // still in use; the next sweep gets it
            }
        }
    }

    /// <summary>True when the file on disk is not the remembered original (other size, else other hash).</summary>
    public static bool Differs(ModBaseEntry entry, string file) => new FileInfo(file).Length != entry.Size || ModPackBuilder.HashFile(file) != entry.Hash;

    /// <summary>Shrinks the cache to the cap (the setting was lowered). Does nothing when the index cannot be read.</summary>
    public void Trim(long maxBytes)
    {
        lock (_lock)
        {
            if (!Directory.Exists(_folder) || ReadIndex() is not { } index) return;
            Evict(index, maxBytes, "");
            WriteIndex(index);
        }
    }

    /// <summary>
    /// Unpacks the copy into <paramref name="folder"/> under the original file name and returns the path. Null when the copy is missing, cannot be read right now (it stays),
    /// or is damaged or does not match its hash (it is then dropped).
    /// </summary>
    public string? TryExtract(ModBaseEntry entry, string folder)
    {
        lock (_lock)
        {
            var blob = BlobFile(entry.Hash);
            if (!File.Exists(blob)) return null;
            var target = Path.Combine(folder, Path.GetFileName(entry.Path));
            var damaged = false;
            try
            {
                using (var input = new GZipStream(File.OpenRead(blob), CompressionMode.Decompress))
                using (var output = File.Create(target))
                    input.CopyTo(output);
                if (ModPackBuilder.HashFile(target) == entry.Hash) return target;
                damaged = true;
            }
            catch (InvalidDataException)
            {
                damaged = true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // a transient failure is not damage: the copy stays
            }

            File.Delete(target);
            if (damaged && ReadIndex() is { } index)
            {
                foreach (var key in index.Where(p => p.Value.Hash == entry.Hash).Select(p => p.Key).ToList()) index.Remove(key);
                WriteIndex(index);
                File.Delete(blob);
            }

            return null;
        }
    }

    /// <summary>Deletes the whole cache; false when something is locked and the cache is left as it was or partly.</summary>
    public bool Clear()
    {
        lock (_lock)
        {
            if (!Directory.Exists(_folder)) return true;
            try
            {
                SweepParts();
                Directory.Delete(_folder, true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
