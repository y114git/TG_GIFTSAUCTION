namespace G3MStudio.Core.Workspace;

/// <summary>
/// The tabs in the order they were brought to the front, with a position in it: Previous Tab and Next Tab go back and forward through it like a browser.
/// Bringing a new tab to the front drops what lay ahead.
/// </summary>
public sealed class ActivationHistory
{
    public const int Max = 100;
    private readonly List<string> _ids = [];
    private int _at = -1;

    /// <summary>Notes that a tab came to the front by the user's own doing (not by <see cref="Step"/>).</summary>
    public void Record(string id)
    {
        if (_at >= 0 && _ids[_at] == id) return;
        _ids.RemoveRange(_at + 1, _ids.Count - _at - 1);
        _ids.Add(id);
        if (_ids.Count > Max) _ids.RemoveAt(0);
        _at = _ids.Count - 1;
    }

    /// <summary>A tab is gone: it leaves the history and neighbours that become equal merge.</summary>
    public void Forget(string id)
    {
        var kept = new List<string>();
        var at = -1;
        for (var i = 0; i < _ids.Count; i++)
        {
            if (_ids[i] != id && (kept.Count == 0 || kept[^1] != _ids[i])) kept.Add(_ids[i]);
            if (i == _at) at = kept.Count - 1;
        }

        _ids.Clear();
        _ids.AddRange(kept);
        _at = kept.Count == 0 ? -1 : Math.Max(at, 0);
    }

    /// <summary>Moves back (-1) or forward (1) to the nearest tab that <paramref name="exists"/>; null when there is none.</summary>
    public string? Step(int direction, Func<string, bool> exists)
    {
        for (var i = _at + direction; i >= 0 && i < _ids.Count; i += direction)
        {
            if (!exists(_ids[i])) continue;
            _at = i;
            return _ids[i];
        }

        return null;
    }

    /// <summary>True when a step in the direction would find a tab that <paramref name="exists"/>.</summary>
    public bool Can(int direction, Func<string, bool> exists)
    {
        for (var i = _at + direction; i >= 0 && i < _ids.Count; i += direction)
            if (exists(_ids[i])) return true;
        return false;
    }
}
