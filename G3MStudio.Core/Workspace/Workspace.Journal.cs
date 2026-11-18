namespace G3MStudio.Core.Workspace;

public sealed partial class Workspace
{
    /// <summary>Edits put back from the journal at open (the unsaved ones that were still applied).</summary>
    public int RecoveredEdits { get; private set; }

    /// <summary>Why the journal could only be replayed in part, or null.</summary>
    public string? RecoveryProblem { get; private set; }

    private Journal? _journal;

    /// <summary>Opens the journal and replays what an earlier session left in it, then journals every later edit.</summary>
    internal void StartJournal(UserData userData)
    {
        Journal journal;
        IReadOnlyList<JournalRecord> records;
        try
        {
            (journal, records) = Journal.Open(userData, Identity);
        }
        catch (Exception e) when (e is (IOException and not DataFileInUseException) or UnauthorizedAccessException)
        {
            // The file opens without a journal (a full disk, a read-only data folder); the window is told once it shows the file.
            JournalOpenFailure = e;
            HookJournal();
            return;
        }

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
        HookJournal();
    }

    /// <summary>Why the journal could not be created at open, or null; edits are then not journaled.</summary>
    internal Exception? JournalOpenFailure { get; private set; }

    private void HookJournal()
    {
        if (_journalHooked) return;
        _journalHooked = true;
        History.Recorded += (op, command) =>
        {
            // After a bulk change the later edits cannot be replayed onto the file alone, so nothing is journaled until the next save.
            if (JournalSuspended) return;
            try
            {
                _journal?.Append(op == "do" ? new JournalRecord(op, command.Kind, command.Args.ToDictionary()) : new JournalRecord(op));
                _dataWriteFailed = false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The edit is applied and stays in memory; only crash recovery misses it.
                ReportDataWriteFailure(e);
            }
        };
    }

    /// <summary>A write into the G3MStudio data folder (journal, recent list, settings) failed while the user's work is intact in memory. Raised once until a write works again.</summary>
    public event Action<Exception>? DataWriteFailed;

    private bool _dataWriteFailed;

    internal void ReportDataWriteFailure(Exception e)
    {
        if (_dataWriteFailed) return;
        _dataWriteFailed = true;
        DataWriteFailed?.Invoke(e);
    }

    private bool _journalHooked;

    internal void LoadLocks(UserData userData)
    {
        History.Locks = Locks = new ResourceLocks(userData, Path);
        Nicknames = new Nicknames(userData, Path) { IsOwner = key => _owners.Contains(key) };
        LoadOwners();
        if (_ownersHooked) return;
        _ownersHooked = true;
        // Every add, rename, delete, undo, redo and import ends in a history change, raised on the thread that made it and under its write lock; the labels only read the finished set.
        History.Changed += LoadOwners;
    }

    private volatile HashSet<string> _owners = [];
    private bool _ownersHooked;

    // The objects, scripts and rooms of the file as Explorer keys. Read here, where no other thread writes the model, so a label never touches the model itself.
    private void LoadOwners()
    {
        try
        {
            _owners = [.. ResourceTypes.All.Where(t => t.Key is "objects" or "scripts" or "rooms").SelectMany(t => (t.Items(Data) ?? []).Select(i => ResourceTypes.KeyOf(t.Key, ResourceTypes.NameOf(i))))];
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
        {
            // a model that cannot be read right now keeps the last set; the next change reads it again
        }
    }

    /// <summary>True after a bulk change until the next save: crash recovery has nothing to offer for those edits.</summary>
    public bool JournalSuspended { get; private set; }

    /// <summary>True when unsaved edits are not safe in the journal: a bulk change suspended it, or a write to it failed.</summary>
    public bool JournalLost => JournalSuspended || _dataWriteFailed || JournalOpenFailure is not null;

    /// <summary>After a sync or a move of the data file: the same workspace at another path, with a journal of its own.</summary>
    internal async Task RelocateAsync(string newPath, UserData userData)
    {
        var identity = await DataFileIdentity.ComputeAsync(newPath);
        _journal?.Dispose();
        // Layout, folders, locks, nicknames and run options follow the file to its new path and replace what an earlier file of that path left, so the live
        // stores and the saved ones agree.
        userData.AdoptFileData(Path, identity.Path, overwrite: true);
        Locks.MoveTo(userData, identity.Path);
        Nicknames.MoveTo(userData, identity.Path);
        Path = identity.Path;
        Identity = identity;
        RememberStamp();
        _journal = Journal.Open(userData, identity).Journal;
        IdentityChanged?.Invoke(identity);
    }
}
