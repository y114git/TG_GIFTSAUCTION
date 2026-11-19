namespace G3MStudio.Editor;

/// <summary>The region of the player: what the owner is told, how it follows the playing sound, and the arrow keys that move its edge.</summary>
public sealed partial class AudioTransport
{
    /// <summary>The user selected, resized or cleared the region on the waveform: its start and end in seconds, or 0 and 0 for cleared. The owner stores them and calls <see cref="Redraw"/> (or changes what it returns).</summary>
    public event Action<double, double>? RegionChanged;

    /// <summary>The region changed: draw it again, and when a sound is playing or paused, change its range to the new region (or to the whole sound) without restarting it.</summary>
    public void Redraw()
    {
        if (_output is { IsActive: true })
        {
            var region = ActiveRegion();
            _output.SetRange(region?.Start ?? 0, region?.End ?? 0);
        }

        Refresh();
    }

    /// <summary>Removes the region: the owner decides what that means (nothing selected, or the whole sound).</summary>
    public void ClearRegion() => RegionChanged?.Invoke(0, 0);

    // The ends are rounded to hundredths like the fields that show them, except the ends of the sound, which stay exact.
    private void OnRegionDragged((double Start, double End)? dragged)
    {
        if (dragged is not var (from, to) || to <= from)
        {
            ClearRegion();
            return;
        }

        var (start, end) = (from <= 0 ? 0 : Math.Round(from * _duration, 2), to >= 1 ? _duration : Math.Round(to * _duration, 2));
        if (end > start) RegionChanged?.Invoke(start, end);
        else Refresh(); // too short to survive the rounding: the region stays as the owner has it
    }

    private (double Start, double End)? ActiveRegion()
    {
        if (_region() is not var (start, end)) return null;
        end = Math.Min(end, _duration);
        return end > start ? (start, end) : null;
    }

    // The arrow keys on the waveform move the edge of the region that was last used: 0.01 s a press, 0.1 s with Shift.
    private bool NudgeEdge(int direction, bool big)
    {
        if (ActiveRegion() is not var (start, end)) return false;
        var step = direction * (big ? 0.1 : 0.01);
        if (_wave.EndEdge) end = Math.Clamp(end + step, Math.Min(start + 0.01, _duration), _duration);
        else start = Math.Clamp(start + step, 0, Math.Max(0, end - 0.01));
        RegionChanged?.Invoke(Math.Round(start, 2), Math.Round(end, 2));
        return true;
    }
}
