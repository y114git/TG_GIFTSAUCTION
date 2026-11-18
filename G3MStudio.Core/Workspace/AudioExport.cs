using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>Writes the audio of every sound as a file named like the sound, from the data file and from the audio group files next to it.</summary>
public static class AudioExport
{
    public static Task<IReadOnlyList<ItemResult>> ExportAllAsync(Workspace workspace, string directory, IProgress<BatchProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var results = new List<ItemResult>();
            Directory.CreateDirectory(directory);
            List<(G3MLib.DataFile.Models.GameMakerSound Sound, string Name)> sounds;
            using (workspace.Lock.Read())
                sounds = [.. workspace.Data.Sounds.Where(s => !string.IsNullOrEmpty(s?.Name?.Content)).Select(s => (Sound: s, Name: s.Name.Content ?? ""))];
            using var reader = new AudioGroupReader(workspace.Data, workspace.Path);
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ((sound, name), index) in sounds.Select((s, i) => (s, i)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new BatchProgress(index, sounds.Count, name));
                try
                {
                    // The lock is held only while the bytes are copied out, so edits are not blocked by the disk writes.
                    byte[]? bytes;
                    using (workspace.Lock.Read())
                        // The data file's own audio first; the reader then finds the entry in the audiogroupN.dat files next to it.
                        bytes = sound.AudioFile?.Data is { Length: > 0 } own ? [.. own] : reader.GetAudio(sound, out _)?.ToArray();
                    if (bytes is not { Length: > 0 })
                    {
                        results.Add(new ItemResult(name, ResultKind.Skipped, "No audio found."));
                        continue;
                    }

                    var file = string.Concat(name.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
                    var full = file + "." + SoundAudio.Format(bytes);
                    // Names that differ only by case or by an invalid character land on one file: the later one gets a number.
                    for (var n = 2; !taken.Add(full); n++) full = file + "_" + n + "." + SoundAudio.Format(bytes);
                    File.WriteAllBytes(Path.Combine(directory, full), bytes);
                    results.Add(new ItemResult(name, ResultKind.Written, null));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    results.Add(new ItemResult(name, ResultKind.Failed, e.Message));
                }
            }

            progress?.Report(new BatchProgress(sounds.Count, sounds.Count, ""));
            return (IReadOnlyList<ItemResult>)results;
        }, cancellationToken);
}
