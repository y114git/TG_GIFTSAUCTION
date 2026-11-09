using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

public sealed record RunLine(DateTimeOffset Time, string Text, bool Error);

/// <summary>A code entry and line named in game output, such as <c>gml_Object_obj_player_Step_0 (line 12)</c>.</summary>
public sealed partial record GmlLocation(string Entry, int Line)
{
    [GeneratedRegex(@"\b(gml_[A-Za-z0-9_]+)\s*\(line\s+(\d+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static GmlLocation? Find(string text) => Pattern().Match(text) is { Success: true } m ? new GmlLocation(m.Groups[1].Value, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : null;
}

/// <summary>What the game wrote while it ran: kept for the Output panel, newest last, oldest dropped past <see cref="Max"/>. Safe to fill from any thread.</summary>
public sealed class RunLog
{
    public const int Max = 5000;

    private readonly List<RunLine> _lines = [];
    private readonly Lock _gate = new();

    public event Action? Changed;

    public IReadOnlyList<RunLine> Lines
    {
        get { lock (_gate) return [.. _lines]; }
    }

    public void Add(string text, bool error, DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            _lines.Add(new RunLine(now ?? DateTimeOffset.Now, text, error));
            if (_lines.Count > Max) _lines.RemoveRange(0, _lines.Count - Max);
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate) _lines.Clear();
        Changed?.Invoke();
    }
}
