using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// What the Explorer can hide on request: code that has no text of its own (a child entry, or one the decompiler fails on) and the scripts
/// that point at it, and resources that point at nothing. Child entries and broken resources are known at once; the decompiler is tried in
/// the background, entry by entry.
/// </summary>
public sealed class ResourceHealth
{
    private readonly HashSet<string> _failed = [];
    private readonly Lock _gate = new();

    /// <summary>Raised from the scanning thread after more entries were found failing.</summary>
    public event Action? Changed;

    public bool HasFailed(string entry)
    {
        lock (_gate) return _failed.Contains(entry);
    }

    /// <summary>True for code without a text of its own: a child entry, or an entry the decompiler failed on.</summary>
    public bool IsUndecompilable(GameMakerCode code) => code.ParentEntry is not null || code.Name?.Content is { } name && HasFailed(name);

    // Both the entry itself and a script pointing at it: the script row would open nothing readable either.
    public bool Hides(object? item) => item switch
    {
        GameMakerCode code => IsUndecompilable(code),
        GameMakerScript { Code: { } code } => IsUndecompilable(code),
        _ => false,
    };

    /// <summary>A resource that points at nothing: a sprite frame, font or tile set without its picture, or a room instance of an object that is gone.</summary>
    public static bool IsBroken(object? item)
    {
        try
        {
            return item switch
            {
                GameMakerSprite sprite => sprite.Textures.Count > 0 && sprite.Textures.Any(t => t?.Texture is null),
                GameMakerFont font => font.Texture is null,
                GameMakerBackground background => background.Texture is null,
                GameMakerRoom room => room.GameObjects.Any(i => i.ObjectDefinition is null),
                _ => false,
            };
        }
        catch (Exception e) when (e is NullReferenceException or InvalidOperationException or ArgumentException)
        {
            return true; // a resource that cannot even be read is broken
        }
    }

    private volatile bool _stopped;
    private bool _started;
    private long _lastReport;

    /// <summary>Starts <see cref="ScanAsync"/> once in the background; <see cref="Stop"/> ends it.</summary>
    public void Start(Workspace workspace)
    {
        if (_started) return;
        _started = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await ScanAsync(workspace);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                // The file was closed or changed under the scan: what was found so far stays.
            }
            catch (Exception e) when (e is not OutOfMemoryException && FailureReport.Seen(e, "scan.decompile"))
            {
                throw; // never reached: the filter reports and answers false, so the exception goes on as before
            }
        });
    }

    /// <summary>Ends the scan; what it found stays readable.</summary>
    public void Stop() => _stopped = true;

    /// <summary>Tries the decompiler on every entry that has no parent, so <see cref="HasFailed"/> fills up; stops after <see cref="Stop"/>.</summary>
    public async Task ScanAsync(Workspace workspace)
    {
        string[] entries;
        using (workspace.Lock.Read())
            entries = [.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()];
        var context = await Task.Run(() => { using (workspace.Lock.Read()) return workspace.DecompileContext(); });
        var found = 0;
        await Parallel.ForEachAsync(entries, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, (entry, token) =>
        {
            if (_stopped) throw new OperationCanceledException();
            try
            {
                using (workspace.Lock.Read()) CodeText.Decompile(workspace.Data, entry, context);
            }
            catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
            {
                lock (_gate) _failed.Add(entry);
                Interlocked.Increment(ref found);
                // The list is built again for each report, so they are at most one per interval.
                var now = Environment.TickCount64;
                if (now - Interlocked.Read(ref _lastReport) >= 750)
                {
                    Interlocked.Exchange(ref _lastReport, now);
                    Changed?.Invoke();
                }
            }

            return ValueTask.CompletedTask;
        });
        if (found > 0) Changed?.Invoke();
    }
}
