using System.Runtime.CompilerServices;
using G3MLib.DataFile;
using G3MLib.DataFile.References;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The reverse reference index of one model, built once and kept until an edit can change what refers to what (about 200 to 300 ms to build on a large game).
/// <see cref="EditHistory"/> drops it after every edit except one that says it keeps references (a rename only changes the text of strings, never which object holds which).
/// Callers hold the model lock: reading for <see cref="Get"/>, writing for an edit.
/// </summary>
public sealed class ReferenceCache
{
    private static readonly ConditionalWeakTable<GameMakerData, ReferenceCache> Caches = [];

    private readonly Lock _gate = new();
    private ReferenceIndex? _index;

    public static ReferenceCache Of(GameMakerData data) => Caches.GetValue(data, _ => new ReferenceCache());

    /// <summary>True when an index is ready, so <see cref="Get"/> returns at once.</summary>
    public bool IsBuilt
    {
        get
        {
            lock (_gate) return _index is not null;
        }
    }

    public ReferenceIndex Get(GameMakerData data, CancellationToken cancellationToken = default)
    {
        lock (_gate) return _index ??= ReferenceIndex.Build(data, cancellationToken);
    }

    public void Invalidate()
    {
        lock (_gate) _index = null;
    }
}
