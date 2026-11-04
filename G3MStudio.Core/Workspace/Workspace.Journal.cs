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
    /// <summary>After a sync or a move of the data file: the same workspace at another path, with a journal of its own.</summary>
    internal async Task RelocateAsync(string newPath, UserData userData)
    {
        var identity = await DataFileIdentity.ComputeAsync(newPath);
        _journal?.Dispose();
        Path = identity.Path;
        Identity = identity;
        _journal = Journal.Open(userData, identity).Journal;
    }
}
