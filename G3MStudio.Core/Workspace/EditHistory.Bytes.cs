namespace G3MStudio.Core.Workspace;

/// <summary>Trims the oldest undo steps once their retained size passes the byte limit.</summary>
public sealed partial class EditHistory
{
    /// <summary>The limit every history uses unless it is given its own: the Undo memory limit of the settings.</summary>
    public static long DefaultByteLimit { get; set; } = 256L * 1024 * 1024;

    private long? _byteLimit;

    /// <summary>The most memory the undo steps may hold (kept pictures and audio); past it the oldest steps are dropped. The newest step always stays.</summary>
    public long ByteLimit
    {
        get => _byteLimit ?? DefaultByteLimit;
        init => _byteLimit = value;
    }

    // Every step is applied here (a commit cuts the redo steps first). A save point before the dropped step cannot be reached any more, so the file stays dirty.
    private void Trim()
    {
        while (_bytes > ByteLimit && _items.Count > 1)
        {
            _bytes -= _sizes[0];
            _sizes.RemoveAt(0);
            _items.RemoveAt(0);
            Position--;
            if (_saved >= 0) _saved = _saved == 0 ? -1 : _saved - 1;
        }
    }
}
