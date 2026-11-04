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
            using (workspace.Lock.Read())
            {
                var sounds = workspace.Data.Sounds.Where(s => !string.IsNullOrEmpty(s?.Name?.Content)).Select(s => (Sound: s, Name: s.Name.Content ?? "")).ToList();
                using var reader = new AudioGroupReader(workspace.Data, workspace.Path);
                foreach (var ((sound, name), index) in sounds.Select((s, i) => (s, i)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new BatchProgress(index, sounds.Count, name));
                    try
                    {
                        // The data file's own audio first; the reader then finds the entry in the audiogroupN.dat files next to it.
                        if ((sound.AudioFile?.Data is { Length: > 0 } own ? own : reader.GetAudio(sound, out _)?.ToArray()) is not { Length: > 0 } bytes)
                        {
                            results.Add(new ItemResult(name, ResultKind.Skipped, "No audio found."));
                            continue;
                        }

                        var file = string.Concat(name.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
                        File.WriteAllBytes(Path.Combine(directory, file + "." + SoundAudio.Format(bytes)), bytes);
                        results.Add(new ItemResult(name, ResultKind.Written, null));
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        results.Add(new ItemResult(name, ResultKind.Failed, e.Message));
                    }
                }

                progress?.Report(new BatchProgress(sounds.Count, sounds.Count, ""));
            }

            return (IReadOnlyList<ItemResult>)results;
        }, cancellationToken);
}
