using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The player every sound shows (the sound tab, the generator and the recorder): the <see cref="AudioTransport"/> and, under it, the fields that hold the region, so the region can be typed as well as dragged.
/// The fields are the region: <see cref="Region"/> reads them, and a change of either, by the user or by code, plays through to the sound that is playing (see <see cref="AudioTransport.Redraw"/>).
/// </summary>
public sealed class RegionPlayer : StackPanel
{
    private readonly NumericUpDown _start, _end;
    private bool _syncing;

    /// <param name="duration">Seconds of the sound, for the bar before the audio is decoded.</param>
    /// <param name="decode">Decodes the audio; runs on a pool thread.</param>
    /// <param name="options">The playback options, read at each play.</param>
    /// <param name="playName">Automation name of the play button.</param>
    /// <param name="regionName">Prefix of the automation names of the two fields ("SoundRegion" gives SoundRegionStart and SoundRegionEnd).</param>
    /// <param name="beforePlay">Runs before a sound starts, so the owner can finish a change that is still waiting.</param>
    public RegionPlayer(double duration, Func<PcmAudio> decode, Func<AudioOptions> options, string playName, string regionName = "SoundRegion", Action? beforePlay = null)
    {
        Spacing = 4;
        (_start, _end) = (Field(regionName + "Start"), Field(regionName + "End"));
        Transport = new AudioTransport(duration, decode, options, () => (Seconds(_start), Seconds(_end)), playName) { BeforePlay = beforePlay };
        foreach (var box in new[] { _start, _end }) box.ValueChanged += (_, _) => Changed();
        Transport.RegionChanged += (from, to) => Set(to > from ? from : 0, to > from ? to : 0);
        Children.Add(Transport);
        Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { new TextBlock { Text = Loc.Current["audio.region"], VerticalAlignment = VerticalAlignment.Center }, _start, _end } });
    }

    public AudioTransport Transport { get; }

    /// <summary>The region in seconds, or null while there is none (the end is not after the start).</summary>
    public (double Start, double End)? Region => Seconds(_end) > Seconds(_start) ? (Seconds(_start), Seconds(_end)) : null;

    /// <summary>The region changed (a drag, a field, <see cref="Set"/>); the owner shows what it keeps.</summary>
    public event Action? RegionChanged;

    /// <summary>Sets both fields at once, so the player never sees a start from one region and an end from another; 0 and 0 clears.</summary>
    public void Set(double start, double end)
    {
        _syncing = true;
        (_start.Value, _end.Value) = ((decimal)start, (decimal)end);
        _syncing = false;
        Changed();
    }

    public void Clear() => Set(0, 0);

    /// <summary>The part of <paramref name="clip"/> the region marks, or the whole clip when there is no region or it lies past the end of this clip.</summary>
    public PcmAudio Cut(PcmAudio clip)
    {
        if (Region is not var (start, end)) return clip;
        try
        {
            return AudioCodec.Edit(clip, start, end);
        }
        catch (InvalidOperationException) // the region would leave not one sample
        {
            return clip;
        }
    }

    private void Changed()
    {
        if (_syncing) return;
        Transport.Redraw();
        RegionChanged?.Invoke();
    }

    private static double Seconds(NumericUpDown box) => (double)(box.Value ?? 0);

    private static NumericUpDown Field(string name)
    {
        var box = new NumericUpDown { Name = name, Minimum = 0, Maximum = 100000, Increment = 0.05m, FormatString = "0.00", Value = 0, Width = 120 };
        ToolTip.SetTip(box, Loc.Current["audio.regionFieldTip"]);
        return box;
    }
}
