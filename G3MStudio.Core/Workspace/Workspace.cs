using G3MLib.DataFile;
using G3MLib.DataFile.Decompiler;
using G3MStudio.Core.Projects;
using G3MLib.Modding.Hashing;

namespace G3MStudio.Core.Workspace;

/// <summary>Size, MD5 and path of a data file at open time; the key that later finds its project (ADR-0010).</summary>
public sealed record DataFileIdentity(string Path, long Size, string Md5)
{
    public static async Task<DataFileIdentity> ComputeAsync(string path, CancellationToken cancellationToken = default)
    {
        var full = System.IO.Path.GetFullPath(path);
        return new DataFileIdentity(full, new FileInfo(full).Length, await FileHash.ComputeMd5Async(full, cancellationToken));
    }
}

/// <summary>G3MLib declares its progress parameters non-nullable although null is allowed; this stands in for "no progress".</summary>
internal sealed class NoProgress : IProgress<GameMakerProgress>
{
    public static readonly NoProgress Instance = new();

    public void Report(GameMakerProgress value)
    {
    }
}

/// <summary>One open data file: the G3MLib model is the live representation.</summary>
public sealed class Workspace(string path, DataFileIdentity identity, GameMakerData data, IReadOnlyList<string> warnings) : IDisposable
{
    public string Path { get; private set; } = path;

    public DataFileIdentity Identity { get; private set; } = identity;

    public GameMakerData Data { get; } = data;

    /// <summary>Guards the model for background jobs, as G3MLib describes for hosts.</summary>
    public ModelLock Lock { get; } = new();

    /// <summary>Load warnings from the reader (design/shell.md 14.1 item 4).</summary>
    public IReadOnlyList<string> Warnings { get; } = warnings;

    public string Name => Data.GeneralInfo?.DisplayName?.Content is { Length: > 0 } name ? name : System.IO.Path.GetFileName(Path);

    public string Version => Data.GeneralInfo is { } info ? $"{info.Major}.{info.Minor}.{info.Release}.{info.Build}" : "";

    public EditHistory History { get; } = new(data);

    public ProblemsModel Problems { get; } = new();

    /// <summary>The project of this data file when Project mode is on (ADR-0010), else null.</summary>
    public ProjectLink? Project { get; internal set; }

    public bool IsDirty => History.IsDirty;

    /// <summary>Edits put back from the journal at open (the unsaved ones that were still applied).</summary>
    public int RecoveredEdits { get; private set; }

    /// <summary>Why the journal could only be replayed in part, or null.</summary>
    public string? RecoveryProblem { get; private set; }

    private Journal? _journal;

    /// <summary>Opens the journal and replays what an earlier session left in it, then journals every later edit.</summary>
    internal void StartJournal(UserData userData)
    {
        var (journal, records) = Journal.Open(userData, Identity);
        _journal = journal;
        var applied = new List<JournalRecord>();
        try
        {
            foreach (var record in records)
            {
                switch (record.Op)
                {
                    case "do": History.Execute(EditCommand.Create(record.Kind ?? "", record.Args ?? [])); break;
                    case "undo": History.Undo(); break;
                    case "redo": History.Redo(); break;
                    default: throw new InvalidDataException($"Unknown journal record '{record.Op}'.");
                }

                applied.Add(record);
            }
        }
        catch (Exception e)
        {
            RecoveryProblem = e.Message;
            journal.Reset(Identity, applied);
        }

        RecoveredEdits = History.Position;
        if (_journalHooked) return;
        _journalHooked = true;
        History.Recorded += (op, command) =>
        {
            // After a bulk change the later edits cannot be replayed onto the file alone, so nothing is journaled until the next save.
            if (!JournalSuspended) _journal?.Append(op == "do" ? new JournalRecord(op, command.Kind, command.Args.ToDictionary()) : new JournalRecord(op));
        };
    }

    private bool _journalHooked;

    /// <summary>True after a bulk change until the next save: crash recovery has nothing to offer for those edits.</summary>
    public bool JournalSuspended { get; private set; }

    /// <summary>
    /// Runs a change that cannot be undone (an import) under the write lock and then drops the undo stack, whether the change finished or not.
    /// Only on a workspace without unsaved edits, so the journal never has to mix them with a change it cannot replay.
    /// </summary>
    public void ApplyBulk(Action change)
    {
        if (History.IsDirty) throw new InvalidOperationException("Save or revert the unsaved changes first.");
        using (Lock.Write())
        {
            _decompileContext = null;
            try
            {
                change();
            }
            finally
            {
                History.Barrier();
                _journal?.Reset(Identity, []);
                JournalSuspended = true;
            }
        }

        BulkApplied?.Invoke();
    }

    /// <summary>
    /// Like <see cref="ApplyBulk"/>, but the change runs on a pool thread and every event (the history changing, the project refreshing) is raised on the
    /// thread that called this, which a window needs because its controls belong to its own thread.
    /// </summary>
    public async Task ApplyBulkAsync(Action change)
    {
        if (History.IsDirty) throw new InvalidOperationException("Save or revert the unsaved changes first.");
        try
        {
            await Task.Run(() =>
            {
                using (Lock.Write())
                {
                    _decompileContext = null;
                    try
                    {
                        change();
                    }
                    finally
                    {
                        _journal?.Reset(Identity, []);
                    }
                }
            }, CancellationToken.None);
        }
        finally
        {
            History.Barrier();
            JournalSuspended = true;
        }

        BulkApplied?.Invoke();
    }

    /// <summary>A bulk change finished: a project has to refresh every resource.</summary>
    public event Action? BulkApplied;
    private readonly List<EditCommand> _queued = [];
    private bool _hold;

    /// <summary>A queued edit could not be applied when its turn came.</summary>
    public event Action<EditCommand, Exception>? QueuedEditFailed;

    /// <summary>Edits made from now on wait (Project export, save) and run in order when <see cref="ReleaseEdits"/> is called.</summary>
    internal void HoldEdits()
    {
        _hold = true;
        History.Frozen = true;
    }

    internal void ReleaseEdits()
    {
        _hold = false;
        History.Frozen = false;
        ApplyQueued();
    }

    private void ApplyQueued()
    {
        var queued = _queued.ToList();
        _queued.Clear();
        foreach (var command in queued)
        {
            try
            {
                using (Lock.Write()) History.Execute(command);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                QueuedEditFailed?.Invoke(command, e);
            }
        }
    }

    /// <summary>After a sync or a move of the data file: the same workspace at another path, with a journal of its own.</summary>
    internal async Task RelocateAsync(string newPath, UserData userData)
    {
        var identity = await DataFileIdentity.ComputeAsync(newPath);
        _journal?.Dispose();
        Path = identity.Path;
        Identity = identity;
        _journal = Journal.Open(userData, identity).Journal;
    }

    /// <summary>
    /// The way UI code makes an edit: under the model write lock, so background readers (decompiling) never see it half done.
    /// False when the edit was queued because the workspace is saving or exporting.
    /// </summary>
    public bool Edit(EditCommand command)
    {
        if (_hold || History.Frozen)
        {
            _queued.Add(command);
            return false;
        }

        using (Lock.Write())
        {
            _decompileContext = null;
            History.Execute(command);
        }

        return true;
    }

    private GlobalDecompileContext? _decompileContext;
    private readonly Lock _contextLock = new();

    /// <summary>
    /// The context decompiling needs, made once and shared until the next edit: building it reads every function and variable, which costs
    /// far more than decompiling one entry. Call with <see cref="Lock"/> held for reading.
    /// </summary>
    public GlobalDecompileContext DecompileContext()
    {
        lock (_contextLock) return _decompileContext ??= new GlobalDecompileContext(Data);
    }

    public void Undo()
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.Undo();
        }
    }

    public void Redo()
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.Redo();
        }
    }

    /// <summary>Undo or redo to any point of the history list.</summary>
    public void MoveTo(int position)
    {
        using (Lock.Write())
        {
            _decompileContext = null;
            History.MoveTo(position);
        }
    }

    /// <summary>Saves through the pipeline. Edits wait while it runs; on success the journal starts over from the saved file.</summary>
    public async Task<SaveResult> SaveAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        History.Frozen = true;
        try
        {
            var result = await pipeline.SaveAsync(Data, Path, progress, cancellationToken);
            if (!result.Saved) return result;

            // No cancellation from here: the file is already replaced, so the journal must follow.
            Identity = await DataFileIdentity.ComputeAsync(Path, CancellationToken.None);
            _journal?.Reset(Identity, []);
            History.MarkSaved();
            JournalSuspended = false;
            return result;
        }
        finally
        {
            History.Frozen = false;
            ApplyQueued();
        }
    }

    public void Dispose()
    {
        _journal?.Dispose();
        Lock.Dispose();
        Data.Dispose();
    }
}
