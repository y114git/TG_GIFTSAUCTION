using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The audio editor of a WAV or OGG sound: shows its waveform and cuts it to a start and end, scales its volume and fades its ends. Apply hands the new WAV
/// back; the caller makes the one undoable edit of the sound.
/// </summary>
public sealed class AudioEditorWindow : Window
{
    private readonly PcmAudio _audio;
    private readonly string _kind;
    private readonly NumericUpDown _start = Field("AudioStart", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _end = Field("AudioEnd", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _gain = Field("AudioGain", 100, 0, 1600, 5, "0");
    private readonly NumericUpDown _fadeIn = Field("AudioFadeIn", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _fadeOut = Field("AudioFadeOut", 0, 0, 100000, 0.05m);
    /// <summary>Plays the audio, or the range between Start and End as a preview of the cut.</summary>
    public AudioTransport Transport { get; }

    private readonly TextBlock _info = new() { Name = "AudioInfo", Classes = { "muted" } };
    private readonly TextBlock _message = new() { Name = "AudioMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public AudioEditorWindow(string title, byte[] wav, Action<byte[]> apply, Func<G3MStudio.Core.Settings.AudioOptions>? audio = null)
    {
        _audio = AudioCodec.Decode(wav);
        _kind = SoundAudio.Format(wav) is "ogg" ? "ogg" : "wav";
        Title = title;
        Width = 560;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _end.Value = (decimal)_audio.Seconds; // exact: a rounded value would cut the last milliseconds when nothing was changed
        _end.Maximum = (decimal)Math.Ceiling(_audio.Seconds * 100) / 100;
        _start.Maximum = _end.Maximum;
        foreach (var box in new[] { _start, _end, _gain, _fadeIn, _fadeOut }) box.ValueChanged += (_, _) => ShowLength();
        var ok = DialogFrame.Action(this, Loc.Current["audio.apply"], "AudioApply", () =>
        {
            try
            {
                apply(Result());
                Close(true);
            }
            catch (InvalidOperationException e)
            {
                _message.Text = e.Message;
            }
        }, DialogRole.Primary);
        var rows = new StackPanel { Spacing = 8 };
        Transport = new AudioTransport(_audio.Seconds, () => _audio, WaveformView.Peaks(_audio), audio ?? (() => new()), () => (Seconds(_start), Seconds(_end)), "AudioPlay") { NoDevice = () => _message.Text = Loc.Current["audio.noDevice"] };
        foreach (var box in new[] { _start, _end }) box.ValueChanged += (_, _) => Transport.Redraw();
        Closed += (_, _) => Transport.Close();
        rows.Children.Add(Transport);
        rows.Children.Add(DialogFrame.Form((Loc.Current["audio.start"], _start), (Loc.Current["audio.end"], _end), (Loc.Current["audio.gain"], _gain), (Loc.Current["audio.fadeIn"], _fadeIn), (Loc.Current["audio.fadeOut"], _fadeOut)));
        rows.Children.Add(_info);
        _info.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        ShowLength();
        DialogFrame.Show(this, rows, [ok, DialogFrame.Cancel(this, () => Close(false), "AudioCancel")], _message, DialogScroll.Vertical, DialogFrame.Snapshot(_start, _end, _gain, _fadeIn, _fadeOut));
    }

    private static NumericUpDown Field(string name, decimal value, decimal min, decimal max, decimal step, string format = "0.00") =>
        new() { Name = name, Value = value, Minimum = min, Maximum = max, Increment = step, FormatString = format, Width = 140 };

    private static double Seconds(NumericUpDown box) => (double)(box.Value ?? 0);

    // The edited audio is stored as the kind the sound already has (WAV or OGG); a WAV keeps its 16 bit samples, an OGG is encoded again.
    private byte[] Result() => AudioCodec.Encode(AudioCodec.Edit(_audio, Seconds(_start), Seconds(_end), Seconds(_gain) / 100, Seconds(_fadeIn), Seconds(_fadeOut)), _kind);

    private void ShowLength()
    {
        var length = Math.Max(0, Seconds(_end) - Seconds(_start));
        _info.Text = Loc.Current.Format("audio.length", ("seconds", length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        _message.Text = "";
    }
}
