using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;

namespace G3MStudio.Core.Workspace;

/// <summary>Where a sound keeps its audio when the data file does not: an audio group file (audiogroupN.dat) or a streamed file, both beside the data file.</summary>
public enum ExternalKind
{
    Group,
    Streamed,
}

/// <param name="Kind">Which kind of file it is.</param>
/// <param name="Path">The file the audio is expected in, whether or not it is there.</param>
/// <param name="Exists">The file is there.</param>
public sealed record ExternalSource(ExternalKind Kind, string Path, bool Exists);

/// <summary>
/// Finds and reads the audio of sounds that the data file does not hold, for the player. Read only: nothing here writes to those files. The audio group reader of G3MLib is kept open per workspace
/// (each group file is opened once and read entry by entry) and is let go when the workspace closes. <see cref="Load"/> blocks on file reads, so callers run it off the UI thread.
/// </summary>
public sealed class ExternalAudio(Workspace workspace) : IDisposable
{
    private readonly Lock _gate = new();
    private AudioGroupReader? _reader;
    private string? _readerFile;

    /// <summary>Where the audio of a sound that has none in the data file is, or null when the sound keeps its audio inside the data file or has no audio to look for.</summary>
    public ExternalSource? Locate(GameMakerSound sound)
    {
        if (sound.AudioFile?.Data is { Length: > 0 } || Path.GetDirectoryName(workspace.Path) is not { } folder) return null;
        if (sound.GroupID > 0 && sound.AudioID >= 0)
        {
            var name = workspace.Data.AudioGroups is { } groups && sound.GroupID < groups.Count && groups[sound.GroupID]?.Path?.Content is { Length: > 0 } path ? path : $"audiogroup{sound.GroupID}.dat";
            return Beside(folder, name) is { } group ? new ExternalSource(ExternalKind.Group, group, File.Exists(group)) : null;
        }

        if (sound.File?.Content is not { Length: > 0 } file || Beside(folder, file) is not { } streamed) return null;
        if (!File.Exists(streamed) && Path.GetExtension(file).Length == 0 && File.Exists(streamed + ".ogg")) streamed += ".ogg"; // a streamed sound can be named without its ending
        return new ExternalSource(ExternalKind.Streamed, streamed, File.Exists(streamed));
    }

    /// <summary>The bytes of the audio (WAV, OGG or MP3), or null when the file or the entry is not there.</summary>
    public byte[]? Load(GameMakerSound sound)
    {
        if (Locate(sound) is not { Exists: true } source) return null;
        if (source.Kind == ExternalKind.Streamed) return File.ReadAllBytes(source.Path);
        using (workspace.Lock.Read())
        {
            AudioGroupReader reader;
            lock (_gate)
            {
                var file = workspace.Path;
                if (_reader is null || _readerFile != file)
                {
                    _reader?.Dispose();
#pragma warning disable CA2000 // kept in the field, and let go in Dispose
                    (_reader, _readerFile) = (new AudioGroupReader(workspace.Data, file), file); // the model may have moved with a Save As
#pragma warning restore CA2000
                }

                reader = _reader;
            }

            return reader.GetAudio(sound, out _)?.ToArray();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _reader?.Dispose();
            _reader = null;
        }
    }

    // A path inside the folder of the data file, or null (a hand-edited name must not reach outside it).
    private static string? Beside(string folder, string name)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(folder, name));
            var relative = Path.GetRelativePath(Path.GetFullPath(folder), full);
            return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : full;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
